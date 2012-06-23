using System;

namespace Hyperletter.Abstraction {
    [Flags]
    public enum LetterOptions : byte {
        None          = 0,
        SilentDiscard = 1,
        NoRequeue     = 2,
        NoAck         = 4,
        UniqueId      = 8
    }
}
