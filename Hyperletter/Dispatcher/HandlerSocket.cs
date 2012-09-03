using System;
using System.Collections.Generic;
using Hyperletter.Letter;

namespace Hyperletter.Dispatcher {
    public class HandlerSocket : AbstractDispatcher, IHandlerSocket {
        private readonly IHandlerFactory _handlerFactory;
        private readonly DictionaryList<Type, Registration> _registry = new DictionaryList<Type, Registration>();

        public HandlerSocket(IHyperSocket hyperSocket, IHandlerFactory handlerFactory, ITransportSerializer serializer) : base(hyperSocket, serializer) {
            _handlerFactory = handlerFactory;
        }

        public void Register<TMessage, THandler>() where THandler : IHandler<TMessage> {
            _registry.Add(typeof(TMessage), new Registration<TMessage, THandler>(_handlerFactory, Serializer));
        }

        protected override void Received(ILetter letter) {
            var metadata = Serializer.Deserialize<Metadata>(letter.Parts[0]);
            var messageType = Type.GetType(metadata.Type);
            if (messageType == null)
                return;

            IEnumerable<Registration> handlers = GetMatchingRegistrations(messageType);
            foreach(Registration handler in handlers)
                handler.Invoke(letter, messageType);
        }

        private IEnumerable<Registration> GetMatchingRegistrations(Type type) {
            foreach(Registration registration in _registry.Get(type))
                yield return registration;

            foreach(Type interfaceType in type.GetInterfaces())
                foreach(Registration registration in _registry.Get(interfaceType))
                    yield return registration;
        }

        #region Nested concreteType: Registration

        private abstract class Registration {
            public abstract void Invoke(ILetter letter, Type concreteType);
        }

        private class Registration<TMessage, THandler> : Registration where THandler : IHandler<TMessage> {
            private readonly IHandlerFactory _handlerFactory;
            private readonly ITransportSerializer _serializer;

            public Registration(IHandlerFactory handlerFactory, ITransportSerializer serializer) {
                _handlerFactory = handlerFactory;
                _serializer = serializer;
            }

            public override void Invoke(ILetter letter, Type concreteType) {
                var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
                var handler = _handlerFactory.CreateHandler<THandler, TMessage>();
                handler.Execute(message);
            }
        }

        #endregion
    }
}