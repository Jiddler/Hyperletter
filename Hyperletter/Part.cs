namespace Hyperletter {
    public class Part : IPart {
        public PartType PartType { get; set; }
        public byte[] Data { get; set; }
    }
}