using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class DelegateRegistration<TMessage> : Registration {
        private readonly Action<TypedHyperSocket, IAnswerable<TMessage>> _callback;
        private readonly ITransportSerializer _serializer;
        private readonly TypedHyperSocket _socket;

        public DelegateRegistration(Action<TypedHyperSocket, IAnswerable<TMessage>> callback, TypedHyperSocket socket, ITransportSerializer serializer) {
            _callback = callback;
            _socket = socket;
            _serializer = serializer;
        }

        public override void Invoke(TypedHyperSocket socket, ILetter letter, Metadata metadata, Type concreteType) {
            var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
            var answerable = new Answerable<TMessage>(_socket, message, letter.RemoteNodeId, metadata.ConversationId);
            _callback(socket, answerable);
        }
    }
}