using System;
using Hyperletter.Batch;

namespace Hyperletter {
    public class SocketOptions {
        public SocketOptions() {
            Batch = new BatchOptions {Enabled = true, Extend = TimeSpan.FromMilliseconds(100), MaxExtend = TimeSpan.FromSeconds(1), MaxLetters = 4000};
            Heartbeat = new HeartbeatOptions { Intervall = 1000 };
            NodeId = Guid.NewGuid();
        }

        public BatchOptions Batch { get; private set; }
        public Guid NodeId { get; set; }
        public HeartbeatOptions Heartbeat { get; private set; }
    }
}