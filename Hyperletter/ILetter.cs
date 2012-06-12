using System;

namespace Hyperletter {
    public interface ILetter {
        Guid Id { get; }
        LetterType LetterType { get; }
        IPart[] Parts { get; }
    }
}