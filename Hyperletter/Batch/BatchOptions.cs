using System;

namespace Hyperletter.Batch {
    public class BatchOptions {
        public bool Enabled { get; set; }
        public TimeSpan Extend { get; set; }
        public TimeSpan MaxExtend { get; set; }
        public int MaxLetters { get; set; }
    }
}