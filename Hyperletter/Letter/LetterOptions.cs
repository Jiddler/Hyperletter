using System;

namespace Hyperletter.Letter {
    [Flags]
    public enum LetterOptions : byte {
        None = 0,
        SilentDiscard = 1,
        Requeue = 2,
        Ack = 4,
        UniqueId = 8,
        Multicast = 64
    }
}