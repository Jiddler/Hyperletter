namespace Hyperletter.IoC {
    public abstract class Resolver {
        public abstract object Resolve(params object[] parameters);
        public virtual void Build() {
        }
    }
}