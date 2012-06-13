using System;

namespace Hyperletter {
    public interface ILetter {
        Guid Id { get; }
        LetterType LetterType { get; }
        LetterOptions Options { get; }
        IPart[] Parts { get; }
    }
}