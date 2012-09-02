using System;
using System.Linq;
using System.Reflection;

namespace Hyperletter.Dispatcher {
    public class DefaultHandlerFactory : IHandlerFactory {
        public THandler CreateHandler<THandler>(object message) where THandler : IHandler {
            var constructorInfo = GetConstructor<THandler>(message.GetType());
            return (THandler)constructorInfo.Invoke(new[] { message });
        }

        private ConstructorInfo GetConstructor<THandler>(Type messageType)
        {
            var constructor = typeof(THandler).GetConstructors().FirstOrDefault(ci => ci.GetParameters().Count() == 1 && ci.GetParameters().All(p => p.ParameterType.IsAssignableFrom(messageType)));

            if (constructor == null)
                throw new NoMatchingConstructorException();

            return constructor;
        }
    }
}