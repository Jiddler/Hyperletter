using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public interface IAnswerable<out TMessage> {
        TMessage Message { get; }
        void Answer<T>(T value);
        void Answer<T>(T value, LetterOptions options);
        void Answer<TValue, TReply>(TValue value, Action<IAnswerable<TReply>> callback);
        void Answer<TValue, TReply>(TValue value, LetterOptions options, Action<IAnswerable<TReply>> callback);
        IAnswerable<TReply> Answer<TValue, TReply>(TValue value);
        IAnswerable<TReply> Answer<TValue, TReply>(TValue value, LetterOptions options);
    }
}