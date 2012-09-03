using System.Linq;
using System.Reflection;

namespace Hyperletter.Dispatcher {
    public class DefaultHandlerFactory : IHandlerFactory {
        public IHandler<TMessage> CreateHandler<THandler, TMessage>(TMessage message) {
            ConstructorInfo constructorInfo = GetConstructor<THandler>();
            return (IHandler<TMessage>) constructorInfo.Invoke(new object[0]);
        }

        private ConstructorInfo GetConstructor<THandler>() {
            ConstructorInfo constructor =
                typeof(THandler).GetConstructors().FirstOrDefault(ci => !ci.GetParameters().Any());

            if(constructor == null)
                throw new NoMatchingConstructorException();

            return constructor;
        }
    }
}