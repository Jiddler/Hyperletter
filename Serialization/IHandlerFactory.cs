namespace Hyperletter.Dispatcher {
    public interface IHandlerFactory {
        T CreateHandler<T>(object message) where T : IHandler;
    }
}