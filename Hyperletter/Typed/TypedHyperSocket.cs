using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Timers;
using Hyperletter.Letter;
using Hyperletter.Extension;

namespace Hyperletter.Typed {
    public delegate void AnswerCallback<TRequest, TReply>(ITypedSocket socket, AnswerCallbackEventArgs<TRequest, TReply> args);

    public class TypedHyperSocket : ITypedSocket {
        private readonly ITypedHandlerFactory _handlerFactory;

        private readonly ConcurrentDictionary<Guid, Outstanding> _outstandings = new ConcurrentDictionary<Guid, Outstanding>();
        private readonly DictionaryList<Type, Registration> _registry = new DictionaryList<Type, Registration>();
        private readonly Timer _cleanUpTimer;
        private readonly TypedSocketOptions _options;
        private readonly IHyperSocket _socket;

        public TypedSocketOptions Options { get { return _options; } }
        public IHyperSocket Socket { get { return _socket; } }

        public TypedHyperSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : this(new TypedSocketOptions(), handlerFactory, serializer) {
        }

        public TypedHyperSocket(TypedSocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) {
            _options = options;
            _socket = new HyperSocket(options.Socket);
            _socket.Received += SocketOnReceived;
            _handlerFactory = handlerFactory;

            _cleanUpTimer = new Timer(options.AnswerTimeout.TotalMilliseconds/4);
            _cleanUpTimer.Elapsed += CleanUp;
            _cleanUpTimer.Start();

            Serializer = serializer;
        }

        private void CleanUp(object sender, ElapsedEventArgs elapsedEventArgs) {
            _cleanUpTimer.Stop();

            foreach (var pair in _outstandings.Where(x => (x.Value.Created + _options.AnswerTimeout) < DateTime.UtcNow)) {
                var outstanding = pair.Value as DelegateOutstanding;
                if(outstanding == null)
                    continue;

                outstanding.SetResult(new TimeoutException());
                _outstandings.Remove(pair.Key);
            }

            _cleanUpTimer.Start();
        }

        internal ITransportSerializer Serializer { get; private set; }

        public void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage> {
            _registry.Add(typeof(TMessage), new HandlerRegistration<THandler, TMessage>(this, _handlerFactory, Serializer));
        }

        public void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler) {
            _registry.Add(typeof(TMessage), new DelegateRegistration<TMessage>(handler, this, Serializer));
        }

        public void Send<T>(T value, LetterOptions options = LetterOptions.None) {
            _socket.Send(CreateLetter(value, options, Guid.NewGuid()));
        }

        public IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options = LetterOptions.None) {
            var conversationId = Guid.NewGuid();
            var letter = CreateLetter(value, options, conversationId);
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
            var conversationId = Guid.NewGuid();
            var letter = CreateLetter(request, options, conversationId);
            var outstanding = new DelegateOutstanding<TRequest, TReply>(this, request, callback);
            _outstandings.Add(conversationId, outstanding);

            _socket.Send(letter);
        }

        public void Bind(IPAddress ipAddress, int port) {
            _socket.Bind(ipAddress, port);
        }

        public void Connect(IPAddress ipAddress, int port) {
            _socket.Connect(ipAddress, port);
        }

        private void SocketOnReceived(IHyperSocket hyperSocket, ILetter letter) {
            if(letter.Parts.Length != 2)
                return;

            var metadata = Serializer.Deserialize<Metadata>(letter.Parts[0]);
            var messageType = Type.GetType(metadata.Type);
            if(messageType == null)
                return;

            TriggerOutstanding(metadata, letter);
            TriggerRegistrations(messageType, metadata, letter);
        }

        private void TriggerRegistrations(Type type, Metadata metadata, ILetter letter) {
            var registrations = GetMatchingRegistrations(type);

            foreach(var registration in registrations)
                registration.Invoke(this, letter, metadata, type);
        }

        private IEnumerable<Registration> GetMatchingRegistrations(Type type) {
            foreach(var registration in _registry.Get(type))
                yield return registration;

            foreach(var interfaceType in type.GetInterfaces()) {
                foreach(var registration in _registry.Get(interfaceType))
                    yield return registration;
            }
        }

        private void TriggerOutstanding(Metadata metadata, ILetter letter) {
            Outstanding outstanding;
            if (_outstandings.TryGetValue(metadata.ConversationId, out outstanding)) {
                outstanding.SetResult(metadata, letter);
                _outstandings.Remove(metadata.ConversationId);
            }
        }

        internal void Answer<T>(T value, AbstractAnswerable answerable, LetterOptions options) {
            _socket.SendTo(CreateLetter(value, options, answerable.ConversationId), answerable.ReceivedFrom);
        }

        internal void Answer<TRequest, TReply>(TRequest value, AbstractAnswerable answerable, LetterOptions options, AnswerCallback<TRequest, TReply> callback) {
            var letter = CreateLetter(value, options | LetterOptions.Ack, answerable.ConversationId);
            var outstanding = new DelegateOutstanding<TRequest, TReply>(this, value, callback);
            _outstandings.Add(answerable.ConversationId, outstanding);

            _socket.SendTo(letter, answerable.ReceivedFrom);
        }

        internal IAnswerable<TReply> Answer<TValue, TReply>(TValue value, AbstractAnswerable answerable, LetterOptions options) {
            var letter = CreateLetter(value, options, answerable.ConversationId);
            var outstanding = new BlockingOutstanding<TReply>(this);
            _outstandings.Add(answerable.ConversationId, outstanding);

            _socket.SendTo(letter, answerable.ReceivedFrom);

            try {
                outstanding.Wait();
            } finally {
                _outstandings.Remove(answerable.ConversationId);
            }

            return outstanding.Result;
        }

        private ILetter CreateLetter<T>(T value, LetterOptions options, Guid conversationId) {
            var metadata = new Metadata(value.GetType()) { ConversationId = conversationId };

            var parts = new byte[2][];
            parts[0] = Serializer.Serialize(metadata);
            parts[1] = Serializer.Serialize(value);
            
            var letter = new Letter.Letter(options) { Type = LetterType.User, Parts = parts };

            return letter;
        }
    }
}