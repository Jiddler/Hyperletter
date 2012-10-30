using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class HandlerRegistration<THandler, TMessage> : Registration {
        private readonly ITypedHandlerFactory _handlerFactory;
        private readonly ITransportSerializer _serializer;
        private readonly TypedHyperSocket _socket;

        public HandlerRegistration(TypedHyperSocket socket, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) {
            _socket = socket;
            _handlerFactory = handlerFactory;
            _serializer = serializer;
        }

        public override void Invoke(TypedHyperSocket socket, ILetter letter, Metadata metadata, Type concreteType) {
            var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
            var answerable = new Answerable<TMessage>(_socket, message, letter.RemoteNodeId, metadata.ConversationId);
            ITypedHandler<TMessage> handler = _handlerFactory.CreateHandler<THandler, TMessage>(message);

            handler.Execute(_socket, answerable);
        }
    }
}