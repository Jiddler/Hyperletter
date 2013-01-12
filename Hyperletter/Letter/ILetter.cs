using System;

namespace Hyperletter.Letter {
    public interface ILetter {
        Guid UniqueId { get; }
        LetterType Type { get; }
        LetterOptions Options { get; }
        byte[][] Parts { get; }
    }
}