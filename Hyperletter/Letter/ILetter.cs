using System;

namespace Hyperletter.Letter {
    public interface ILetter {
        LetterType Type { get; }
        LetterOptions Options { get; }
        Guid RemoteNodeId { get; }
        byte[][] Parts { get; }
    }
}