using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public interface ITypedSocket {
        void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage>;
        void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler);

        void Send<T>(T value, LetterOptions options = LetterOptions.None);
        IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options = LetterOptions.None);
        void Send<TRequest, TReply>(TRequest value, AnswerCallback<TRequest, TReply> callback, LetterOptions options = LetterOptions.None);
    }
}