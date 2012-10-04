using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class AbstractAnswerable {
        public static LetterOptions AnswerDefaultOptions = LetterOptions.Answer | LetterOptions.Ack | LetterOptions.UniqueId;
    }
}