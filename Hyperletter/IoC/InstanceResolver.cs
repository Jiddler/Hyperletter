namespace Hyperletter.IoC {
    public class InstanceResolver<TService> : Resolver {
        private readonly TService _instance;

        public InstanceResolver(TService instance) {
            _instance = instance;
        }

        public override object Resolve(params object[] parameters) {
            return _instance;
        }
    }
}