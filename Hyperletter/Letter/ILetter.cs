using System;

namespace Hyperletter.Letter {
    public interface ILetter {
        Guid Id { get; }
        LetterType Type { get; }
        LetterOptions Options { get; }
        byte[][] Parts { get; }
        Guid[] Address { get; }
    }
}
