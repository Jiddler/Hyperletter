using System;
using Hyperletter.Core.Letter;

namespace Hyperletter.Core.Dispatcher {
    public interface IDelegateSocket {
        void Register<TMessage>(Action<TMessage> handler);
        void Send<T>(T value);
        void Send<T>(T value, LetterOptions options);
    }
}