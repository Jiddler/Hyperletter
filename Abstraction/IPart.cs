namespace Hyperletter.Abstraction {
    public interface IPart {
        PartType PartType { get; }
        byte[] Data { get; }
    }
}