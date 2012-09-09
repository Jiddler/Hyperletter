namespace Hyperletter.Typed {
    public interface ITypedHandler<in TMessage> {
        void Execute(ITypedSocket socket, IAnswerable<TMessage> message);
    }
}