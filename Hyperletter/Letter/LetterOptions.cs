using System;

namespace Hyperletter.Letter {
    [Flags]
    public enum LetterOptions : byte {
        None = 0,
        SilentDiscard = 1,
        Requeue = 2,
        Ack = 4,
        UniqueId = 8,
        Routed = 16,
        Answer = 32,
        Multicast = 64
    }
}