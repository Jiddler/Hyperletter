using System;

namespace Hyperletter.Abstraction {
    [Flags]
    public enum LetterOptions : byte {
        SilentDiscard,
        NoRequeue,
        NoAck
    }
}
