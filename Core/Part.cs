using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class Part : IPart {
        public PartType PartType { get; set; }
        public byte[] Data { get; set; }
    }
}