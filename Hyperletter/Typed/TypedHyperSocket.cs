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
            _socket.Send(CreateLetter(value, options));
        }

        public IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options = LetterOptions.None) {
            Letter.Letter letter = CreateLetter(value, options | LetterOptions.UniqueId);
            var outstanding = new BlockingOutstanding<TReply>(this);
            _outstandings.Add(letter.Id, outstanding);
            _socket.Send(letter);

            try {
                outstanding.Wait();
            } finally {
                _outstandings.Remove(letter.Id);
            }

            return outstanding.Result;
        }

        public void Send<TRequest, TReply>(TRequest request, AnswerCallback<TRequest, TReply> callback, LetterOptions options = LetterOptions.None) {
            Letter.Letter letter = CreateLetter(request, options | LetterOptions.UniqueId);
            var outstanding = new DelegateOutstanding<TRequest, TReply>(this, request, callback);
            _outstandings.Add(letter.Id, outstanding);

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
            Type messageType = Type.GetType(metadata.Type);
            if(messageType == null)
                return;

            TriggerOutstanding(metadata, letter);
            TriggerRegistrations(messageType, letter);
        }

        private void TriggerRegistrations(Type type, ILetter letter) {
            IEnumerable<Registration> registrations = GetMatchingRegistrations(type);

            foreach(Registration registration in registrations)
                registration.Invoke(this, letter, type);
        }

        private IEnumerable<Registration> GetMatchingRegistrations(Type type) {
            foreach(Registration registration in _registry.Get(type))
                yield return registration;

            foreach(Type interfaceType in type.GetInterfaces()) {
                foreach(Registration registration in _registry.Get(interfaceType))
                    yield return registration;
            }
        }

        private void TriggerOutstanding(Metadata metadata, ILetter letter) {
            if(letter.Id == Guid.Empty)
                return;

            Outstanding outstanding;
            if(_outstandings.TryGetValue(letter.Id, out outstanding)) {
                outstanding.SetResult(metadata, letter);
                _outstandings.Remove(letter.Id);
            }
        }

        internal void Answer<T>(T value, ILetter answeringTo, LetterOptions options) {
            _socket.Answer(CreateAnswer(value, answeringTo, options), answeringTo);
        }

        internal void Answer<TRequest, TReply>(TRequest value, ILetter answeringTo, LetterOptions options, AnswerCallback<TRequest, TReply> callback) {
            ILetter letter = CreateAnswer(value, answeringTo, options | LetterOptions.Answer | LetterOptions.UniqueId | LetterOptions.Ack);
            var outstanding = new DelegateOutstanding<TRequest, TReply>(this, value, callback);
            _outstandings.Add(letter.Id, outstanding);

            _socket.Answer(letter, answeringTo);
        }

        internal IAnswerable<TReply> Answer<TValue, TReply>(TValue value, ILetter answeringTo, LetterOptions options) {
            ILetter letter = CreateAnswer(value, answeringTo, options);
            var outstanding = new BlockingOutstanding<TReply>(this);
            _outstandings.Add(letter.Id, outstanding);

            _socket.Answer(letter, answeringTo);

            try {
                outstanding.Wait();
            } finally {
                _outstandings.Remove(letter.Id);
            }

            return outstanding.Result;
        }

        internal ILetter CreateAnswer<TAnswer>(TAnswer answer, ILetter answeringTo, LetterOptions options) {
            Letter.Letter letter = CreateLetter(answer, options);
            letter.Id = answeringTo.Id;
            return letter;
        }

        private Letter.Letter CreateLetter<T>(T value, LetterOptions options) {
            var letter = new Letter.Letter(options) { Type = LetterType.User };
            var metadata = new Metadata(value.GetType());
            letter.Parts = new byte[2][];
            letter.Parts[0] = Serializer.Serialize(metadata);
            letter.Parts[1] = Serializer.Serialize(value);

            return letter;
        }
    }
}