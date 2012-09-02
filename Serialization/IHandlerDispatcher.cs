using Hyperletter.Abstraction;

namespace Hyperletter.Dispatcher {
    public interface IHandlerDispatcher {
        void Register<TMessage, THandler>() where THandler : IHandler;
        void Send<T>(T value);
        void Send<T>(T value, LetterOptions options);
    }
}