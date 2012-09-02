using System;
using Hyperletter.Abstraction;

namespace Hyperletter.Dispatcher {
    public interface IDelegateDispatcher {
        void Register<TMessage>(Action<TMessage> handler);
        void Send<T>(T value);
        void Send<T>(T value, LetterOptions options);
    }
}