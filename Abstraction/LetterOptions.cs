using System;

namespace Hyperletter.Abstraction {
    [Flags]
    public enum LetterOptions : byte {
        None          = 0,
        SilentDiscard = 1,
        Requeue     = 2,
        Ack         = 4,
        UniqueId      = 8,
        Routed        = 16,
        Answer        = 32
    }
}
