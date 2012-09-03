namespace Hyperletter.Dispatcher {
    public interface IHandlerFactory {
        IHandler<TMessage> CreateHandler<THandler, TMessage>();
    }
}