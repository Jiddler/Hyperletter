using Hyperletter.Letter;

namespace Hyperletter.Dispatcher {
    public interface IHandlerSocket {
        void Register<TMessage, THandler>() where THandler : IHandler<TMessage>;
        void Send<T>(T value);
        void Send<T>(T value, LetterOptions options);
    }
}