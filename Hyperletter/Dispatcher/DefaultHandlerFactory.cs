using System.Linq;
using System.Reflection;

namespace Hyperletter.Dispatcher {
    public class DefaultHandlerFactory : IHandlerFactory {
        public IHandler<TMessage> CreateHandler<THandler, TMessage>() {
            var constructorInfo = GetConstructor<THandler>();
            return (IHandler<TMessage>)constructorInfo.Invoke(new object[0]);
        }

        private ConstructorInfo GetConstructor<THandler>()
        {
            var constructor = typeof(THandler).GetConstructors().FirstOrDefault(ci => !ci.GetParameters().Any());

            if (constructor == null)
                throw new NoMatchingConstructorException();

            return constructor;
        }
    }
}