using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class Answerable<TMessage> : IAnswerable<TMessage> {
        public static LetterOptions AnswerDefaultOptions = LetterOptions.Answer | LetterOptions.Ack | LetterOptions.UniqueId;

        private readonly ILetter _letter;
        private readonly TypedSocket _socket;

        public Answerable(TypedSocket socket, ILetter letter, TMessage value) {
            _socket = socket;
            _letter = letter;
            Message = value;
        }

        public TMessage Message { get; private set; }

        public void Answer<T>(T value) {
            _socket.Answer(value, _letter, AnswerDefaultOptions);
        }

        public void Answer<T>(T value, LetterOptions options) {
            _socket.Answer(value, _letter, options | AnswerDefaultOptions);
        }

        public void Answer<TValue, TReply>(TValue value, Action<IAnswerable<TReply>> callback) {
            _socket.Answer(value, _letter, AnswerDefaultOptions);
        }

        public void Answer<TValue, TReply>(TValue value, LetterOptions options, Action<IAnswerable<TReply>> callback) {
            _socket.Answer(value, _letter, options | AnswerDefaultOptions);
        }

        public IAnswerable<TReply> Answer<TValue, TReply>(TValue value) {
            return _socket.Answer<TValue, TReply>(value, _letter, AnswerDefaultOptions);
        }

        public IAnswerable<TReply> Answer<TValue, TReply>(TValue value, LetterOptions options) {
            return _socket.Answer<TValue, TReply>(value, _letter, options | AnswerDefaultOptions);
        }
    }
}