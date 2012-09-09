using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public interface ITypedSocket {
        void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage>;
        void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler);

        void Send<T>(T value);
        void Send<T>(T value, LetterOptions options);
        void Send<TValue, TReply>(TValue value, Action<ITypedSocket, IAnswerable<TReply>> callback);
        void Send<TValue, TReply>(TValue value, LetterOptions options, Action<ITypedSocket, IAnswerable<TReply>> callback);
        IAnswerable<TReply> Send<TValue, TReply>(TValue value);
        IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options);
    }
}