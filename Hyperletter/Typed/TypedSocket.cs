using System;
using System.Collections.Generic;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public class TypedSocket : ITypedSocket {
        private readonly ITypedHandlerFactory _handlerFactory;

        private readonly Dictionary<Guid, Outstanding> _outstandings = new Dictionary<Guid, Outstanding>();
        private readonly DictionaryList<Type, Registration> _registry = new DictionaryList<Type, Registration>();
        private readonly IHyperSocket _socket;

        public TypedSocket(IHyperSocket socket, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) {
            _socket = socket;
            _socket.Received += SocketOnReceived;
            _handlerFactory = handlerFactory;
            Serializer = serializer;
        }

        public ITransportSerializer Serializer { get; private set; }

        #region ITypedSocket Members

        public void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage> {
            _registry.Add(typeof(TMessage),
                          new HandlerRegistration<THandler, TMessage>(this, _handlerFactory, Serializer));
        }

        public void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler) {
            _registry.Add(typeof(TMessage), new DelegateRegistration<TMessage>(handler, this, Serializer));
        }

        public void Send<T>(T value) {
            _socket.Send(CreateLetter(value, LetterOptions.None));
        }

        public void Send<T>(T value, LetterOptions options) {
            _socket.Send(CreateLetter(value, options));
        }

        public IAnswerable<TReply> Send<TValue, TReply>(TValue value) {
            return Send<TValue, TReply>(value, LetterOptions.None);
        }

        public IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options) {
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

        public void Send<TValue, TReply>(TValue value, Action<ITypedSocket, IAnswerable<TReply>> callback) {
            Send(value, LetterOptions.None, callback);
        }

        public void Send<TValue, TReply>(TValue value, LetterOptions options,
                                         Action<ITypedSocket, IAnswerable<TReply>> callback) {
            Letter.Letter letter = CreateLetter(value, options | LetterOptions.UniqueId);
            var outstanding = new DelegateOutstanding<TReply>(this, callback);
            _outstandings.Add(letter.Id, outstanding);

            _socket.Send(letter);
        }

        #endregion

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

        public void Answer<T>(T value, ILetter answeringTo, LetterOptions options) {
            _socket.Send(CreateAnswer(value, answeringTo, options));
        }

        public void Answer<TValue, TReply>(TValue value, ILetter answeringTo, LetterOptions options, Action<ITypedSocket, IAnswerable<TReply>> callback) {
            ILetter letter = CreateAnswer(value, answeringTo, options | LetterOptions.Answer | LetterOptions.UniqueId | LetterOptions.Ack);
            var outstanding = new DelegateOutstanding<TReply>(this, callback);
            _outstandings.Add(letter.Id, outstanding);

            _socket.Send(letter);
        }

        public IAnswerable<TReply> Answer<TValue, TReply>(TValue value, ILetter answeringTo, LetterOptions options) {
            ILetter letter = CreateAnswer(value, answeringTo, options);
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

        internal ILetter CreateAnswer<TAnswer>(TAnswer answer, ILetter answeringTo, LetterOptions options) {
            Letter.Letter letter = CreateLetter(answer, options);
            letter.Id = answeringTo.Id;
            return letter;
        }

        private Letter.Letter CreateLetter<T>(T value, LetterOptions options) {
            var letter = new Letter.Letter(options);
            var metadata = new Metadata(value.GetType());
            letter.Parts = new byte[2][];
            letter.Parts[0] = Serializer.Serialize(metadata);
            letter.Parts[1] = Serializer.Serialize(value);

            return letter;
        }
    }
}