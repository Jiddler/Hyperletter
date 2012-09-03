using System;

namespace Hyperletter.Core.Letter {
    public interface ILetter {
        Guid Id { get; }
        LetterType Type { get; }
        LetterOptions Options { get; }
        byte[][] Parts { get; }
        Guid[] Address { get; }
    }
}
