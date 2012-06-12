namespace Hyperletter {
    public interface IPart {
        PartType PartType { get; }
        byte[] Data { get; }
    }
}