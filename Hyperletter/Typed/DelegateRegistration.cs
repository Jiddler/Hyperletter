using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class DelegateRegistration<TMessage> : Registration {
        private readonly Action<TypedSocket, IAnswerable<TMessage>> _callback;
        private readonly ITransportSerializer _serializer;
        private readonly TypedSocket _socket;

        public DelegateRegistration(Action<TypedSocket, IAnswerable<TMessage>> callback, TypedSocket socket, ITransportSerializer serializer) {
            _callback = callback;
            _socket = socket;
            _serializer = serializer;
        }

        public override void Invoke(TypedSocket socket, ILetter letter, Type concreteType) {
            var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
            var answerable = new Answerable<TMessage>(_socket, letter, message);
            _callback(socket, answerable);
        }
    }
}