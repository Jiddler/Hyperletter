using System;

namespace Hyperletter.Abstraction {
    public interface ILetter {
        Guid Id { get; }
        LetterType Type { get; }
        LetterOptions Options { get; }
        IPart[] Parts { get; }
    }
}