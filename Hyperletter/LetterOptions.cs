using System;

namespace Hyperletter {
    [Flags]
    public enum LetterOptions : byte {
        SilentDiscard,
        NoRequeue,
        NoAck
    }
}
