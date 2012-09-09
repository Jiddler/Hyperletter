using System.Linq;
using System.Reflection;

namespace Hyperletter.Typed {
    public class DefaultTypedHandlerFactory : ITypedHandlerFactory {
        #region ITypedHandlerFactory Members

        public ITypedHandler<TMessage> CreateHandler<THandler, TMessage>(TMessage message) {
            ConstructorInfo constructorInfo = GetConstructor<THandler>();
            return (ITypedHandler<TMessage>) constructorInfo.Invoke(new object[0]);
        }

        #endregion

        private ConstructorInfo GetConstructor<THandler>() {
            ConstructorInfo constructor =
                typeof(THandler).GetConstructors().FirstOrDefault(ci => !ci.GetParameters().Any());

            if(constructor == null)
                throw new NoMatchingConstructorException();

            return constructor;
        }
    }
}