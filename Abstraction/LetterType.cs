namespace Hyperletter.Abstraction {
    public enum LetterType : byte {
        Ack         = 0x01,
        Initialize  = 0x02,
        Heartbeat   = 0x03,
        User        = 0x64
    }
}