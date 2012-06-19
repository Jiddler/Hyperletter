namespace Hyperletter.Abstraction {
    public enum LetterType : byte {
        Ack = 0x01,
        User = 0x02,
        Initialize = 0x03,
        StopSending = 0x04,
        Heartbeat = 0x05
    }
}