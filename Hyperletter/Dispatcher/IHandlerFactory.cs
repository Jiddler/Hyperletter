namespace Hyperletter.Core.Dispatcher {
    public interface IHandlerFactory {
        IHandler<TMessage> CreateHandler<THandler, TMessage>();
    }
}