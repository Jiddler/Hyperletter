namespace Hyperletter.Dispatcher {
    public interface IHandler<TMessage> {
        void Execute(TMessage message);
    }
}