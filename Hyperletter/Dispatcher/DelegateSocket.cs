using System;
using System.Collections.Generic;
using Hyperletter.Core.Letter;

namespace Hyperletter.Core.Dispatcher {
    public class DelegateSocket : AbstractDispatcher, IDelegateSocket {
        private readonly DictionaryList<Type, Registration> _registry = new DictionaryList<Type, Registration>();

        public DelegateSocket(IHyperSocket hyperSocket, ITransportSerializer serializer) : base(hyperSocket, serializer) {
        }

        public void Register<TMessage>(Action<TMessage> handler) {
            _registry.Add(typeof(TMessage), new Registration<TMessage>(handler, Serializer));
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

            foreach(Type interfaceType in type.GetInterfaces()) {
                foreach(Registration registration in _registry.Get(interfaceType))
                    yield return registration;
            }
        }

        #region Nested type: Registration

        private abstract class Registration {
            public abstract void Invoke(ILetter letter, Type concreteType);
        }

        private class Registration<TMessage> : Registration {
            private readonly Action<TMessage> _handler;
            private readonly ITransportSerializer _serializer;

            public Registration(Action<TMessage> handler, ITransportSerializer serializer) {
                _handler = handler;
                _serializer = serializer;
            }

            public override void Invoke(ILetter letter, Type concreteType) {
                var message = _serializer.Deserialize<TMessage>(letter.Parts[1], concreteType);
                _handler(message);
            }
        }

        #endregion
    }
}