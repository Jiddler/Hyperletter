namespace Hyperletter.Core.Dispatcher {
    public interface IHandler<TMessage> {
        void Execute(TMessage message);
    }
}