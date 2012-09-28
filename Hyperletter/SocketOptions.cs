using System;
using Hyperletter.Batch;

namespace Hyperletter {
    public class SocketOptions {
        public SocketOptions() {
            Batch = new BatchOptions {Enabled = true, Extend = TimeSpan.FromMilliseconds(100), MaxExtend = TimeSpan.FromSeconds(1), MaxLetters = 4000};
            Id = Guid.NewGuid();
        }

        public BatchOptions Batch { get; private set; }
        public Guid Id { get; set; }
    }
}