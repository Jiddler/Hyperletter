using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Timers;
using Hyperletter.EventArgs.Channel;
using Hyperletter.EventArgs.Letter;
using Hyperletter.EventArgs.Socket;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public delegate void AnswerCallback<TRequest, TReply>(ITypedSocket socket, AnswerCallbackEventArgs<TRequest, TReply> args);

    public class TypedHyperSocket : ITypedSocket {
        private readonly Timer _cleanUpTimer;
        private readonly ITypedHandlerFactory _handlerFactory;
        private readonly TypedSocketOptions _options;

        private readonly ConcurrentDictionary<Guid, Outstanding> _outstandings = new ConcurrentDictionary<Guid, Outstanding>();
        private readonly DictionaryList<Type, Registration> _registry = new DictionaryList<Type, Registration>();
        private readonly IHyperSocket _socket;
        private bool _disposing;

        public TypedHyperSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : this(new TypedSocketOptions(), handlerFactory, serializer) {
        }

        public TypedHyperSocket(TypedSocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) {
            _options = options;
            _socket = new HyperSocket(options.Socket);
            HookEvents();

            _handlerFactory = handlerFactory;

            _cleanUpTimer = new Timer(options.AnswerTimeout.TotalMilliseconds/4);
            _cleanUpTimer.Elapsed += CleanUp;
            _cleanUpTimer.Start();

            Serializer = serializer;
        }

        public TypedSocketOptions Options {
            get { return _options; }
        }

        public IHyperSocket Socket {
            get { return _socket; }
        }

        internal ITransportSerializer Serializer { get; private set; }

        public event Action<ITypedSocket, IConnectingEventArgs> Connecting;
        public event Action<ITypedSocket, IConnectedEventArgs> Connected;
        public event Action<ITypedSocket, IInitializedEventArgs> Initialized;
        public event Action<ITypedSocket, IDisconnectedEventArgs> Disconnected;
        public event Action<ITypedSocket, IDisposedEventArgs> Disposed;

        private void HookEvents() {
            _socket.Received += SocketOnReceived;
            _socket.Connecting += OnSocketOnConnecting;
            _socket.Connected += OnSocketOnConnected;
            _socket.Initialized += OnSocketOnInitialized;
            _socket.Disconnected += OnSocketOnDisconnected;
            _socket.Disposed += OnSocketOnDisposed;
        }

        private void UnhookEvents() {
            _socket.Received -= SocketOnReceived;
            _socket.Connecting -= OnSocketOnConnecting;
            _socket.Connected -= OnSocketOnConnected;
            _socket.Initialized -= OnSocketOnInitialized;
            _socket.Disconnected -= OnSocketOnDisconnected;
            _socket.Disposed -= OnSocketOnDisposed;
        }

        public void Bind(IPAddress ipAddress, int port) {
            _socket.Bind(ipAddress, port);
        }

        public void Connect(IPAddress ipAddress, int port) {
            _socket.Connect(ipAddress, port);
        }

        private void OnSocketOnConnecting(IHyperSocket socket, IConnectingEventArgs args) {
            Action<ITypedSocket, IConnectingEventArgs> evnt = Connecting;
            if(evnt != null) evnt(this, args);
        }

        private void OnSocketOnConnected(IHyperSocket socket, IConnectedEventArgs args) {
            Action<ITypedSocket, IConnectedEventArgs> evnt = Connected;
            if(evnt != null) evnt(this, args);
        }

        private void OnSocketOnInitialized(IHyperSocket socket, IInitializedEventArgs args) {
            Action<ITypedSocket, IInitializedEventArgs> evnt = Initialized;
            if(evnt != null) evnt(this, args);
        }

        private void OnSocketOnDisconnected(IHyperSocket socket, IDisconnectedEventArgs args) {
            Action<ITypedSocket, IDisconnectedEventArgs> evnt = Disconnected;
            if(evnt != null) evnt(this, args);
        }

        private void OnSocketOnDisposed(IHyperSocket socket, IDisposedEventArgs args) {
            UnhookEvents();
            _cleanUpTimer.Dispose();

            foreach(Outstanding outstanding in _outstandings.Values)
                outstanding.SetResult(new SocketDisposedException());

            Action<ITypedSocket, IDisposedEventArgs> evnt = Disposed;
            if(evnt != null) evnt(this, args);
        }

        public void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage> {
            _registry.Add(typeof(TMessage), new HandlerRegistration<THandler, TMessage>(this, _handlerFactory, Serializer));
        }

        public void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler) {
            _registry.Add(typeof(TMessage), new DelegateRegistration<TMessage>(handler, this, Serializer));
        }

        public void Send<T>(T value, LetterOptions options = LetterOptions.None) {
            _socket.Send(CreateLetter(value, options, Guid.NewGuid()));
        }

        public IAnswerable<TReply> Send<TRequest, TReply>(TRequest value, LetterOptions options = LetterOptions.None) {
            Guid conversationId = Guid.NewGuid();
            ILetter letter = CreateLetter(value, options, conversationId);
            var outstanding = new BlockingOutstanding<TReply>(this);
            _outstandings.Add(conversationId, outstanding);
            _socket.Send(letter);

            try {
                outstanding.Wait();
            } finally {
                _outstandings.Remove(conversationId);
            }

            return outstanding.Result;
        }

        public void Send<TRequest, TReply>(TRequest request, AnswerCallback<TRequest, TReply> callback, LetterOptions options = LetterOptions.None) {
            Guid conversationId = Guid.NewGuid();
            ILetter letter = CreateLetter(request, options, conversationId);
            var outstanding = new DelegateOutstanding<TRequest, TReply>(this, request, callback);
            _outstandings.Add(conversationId, outstanding);

            _socket.Send(letter);
        }

        private void SocketOnReceived(ILetter letter, IReceivedEventArgs receivedEventArgs) {
            if(letter.Parts.Length != 2)
                return;

            var metadata = Serializer.Deserialize<Metadata>(letter.Parts[0]);
            Type messageType = Type.GetType(metadata.Type);
            if(messageType == null)
                return;

            TriggerOutstanding(metadata, letter, receivedEventArgs);
            TriggerRegistrations(messageType, metadata, letter, receivedEventArgs);
        }

        private void TriggerRegistrations(Type type, Metadata metadata, ILetter letter, IReceivedEventArgs receivedEventArgs) {
            IEnumerable<Registration> registrations = GetMatchingRegistrations(type);

            foreach(Registration registration in registrations)
                registration.Invoke(this, letter, metadata, type, receivedEventArgs);
        }

        private IEnumerable<Registration> GetMatchingRegistrations(Type type) {
            foreach(Registration registration in _registry.Get(type).ToList())
                yield return registration;

            foreach(Type interfaceType in type.GetInterfaces()) {
                foreach(Registration registration in _registry.Get(interfaceType).ToList())
                    yield return registration;
            }
        }

        private void TriggerOutstanding(Metadata metadata, ILetter letter, IReceivedEventArgs receivedEventArgs) {
            Outstanding outstanding;
            if(_outstandings.TryGetValue(metadata.ConversationId, out outstanding)) {
                outstanding.SetResult(metadata, letter, receivedEventArgs);
                _outstandings.Remove(metadata.ConversationId);
            }
        }

        internal void Answer<T>(T value, AbstractAnswerable answerable, LetterOptions options) {
            _socket.SendTo(CreateLetter(value, options, answerable.ConversationId), answerable.RemoteNodeId);
        }

        internal IAnswerable<TReply> Answer<TValue, TReply>(TValue value, AbstractAnswerable answerable, LetterOptions options) {
            ILetter letter = CreateLetter(value, options, answerable.ConversationId);
            var outstanding = new BlockingOutstanding<TReply>(this);
            _outstandings.Add(answerable.ConversationId, outstanding);

            _socket.SendTo(letter, answerable.RemoteNodeId);

            try {
                outstanding.Wait();
            } finally {
                _outstandings.Remove(answerable.ConversationId);
            }

            return outstanding.Result;
        }

        private ILetter CreateLetter<T>(T value, LetterOptions options, Guid conversationId) {
            var metadata = new Metadata(value.GetType()) {ConversationId = conversationId};

            var parts = new byte[2][];
            parts[0] = Serializer.Serialize(metadata);
            parts[1] = Serializer.Serialize(value);

            var letter = new Letter.Letter(options) {Type = LetterType.User, Parts = parts};

            return letter;
        }

        private void CleanUp(object sender, ElapsedEventArgs elapsedEventArgs) {
            _cleanUpTimer.Stop();

            foreach(var pair in _outstandings.Where(x => (x.Value.Created + _options.AnswerTimeout) < DateTime.UtcNow)) {
                var outstanding = pair.Value as DelegateOutstanding;
                if(outstanding == null)
                    continue;

                outstanding.SetResult(new TimeoutException());
                _outstandings.Remove(pair.Key);
            }

            if(!_disposing)
                _cleanUpTimer.Start();
        }

        public void Dispose() {
            _disposing = true;
            _socket.Dispose();
        }
    }
}