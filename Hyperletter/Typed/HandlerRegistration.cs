using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class HandlerRegistration<THandler, TMessage> : Registration {
        private readonly TypedSocket _socket;
        private readonly ITypedHandlerFactory _handlerFactory;
        private readonly ITransportSerializer _serializer;

        public HandlerRegistration(TypedSocket socket, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) {
            _socket = socket;
            _handlerFactory = handlerFactory;
            _serializer = serializer;
        }

        public override void Invoke(TypedSocket socket, ILetter letter, Type concreteType) {
            var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
            var answerable = new Answerable<TMessage>(_socket, letter, message);
            var handler = _handlerFactory.CreateHandler<THandler, TMessage>(message);

            handler.Execute(_socket, answerable);
        }
    }
}