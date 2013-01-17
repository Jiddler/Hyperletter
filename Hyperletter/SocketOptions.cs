using System;
using Hyperletter.Batch;

namespace Hyperletter {
    public class SocketOptions {
        public SocketOptions() {
            Batch = new BatchOptions {Enabled = true, Extend = TimeSpan.FromMilliseconds(100), MaxExtend = TimeSpan.FromSeconds(1), MaxLetters = 4000};
            Heartbeat = new HeartbeatOptions { Intervall = 1000 };
            Notification = new NotificationOptions { ReceivedNotifyOnAllAckStates = false };
            NodeId = Guid.NewGuid();
            ReconnectIntervall = 1000;
            ShutdownWait = 1500;
            MaximumInitializeTime = 4000;
        }

        public BatchOptions Batch { get; private set; }
        public Guid NodeId { get; set; }
        public HeartbeatOptions Heartbeat { get; private set; }
        public NotificationOptions Notification { get; private set; }
        
        public int ReconnectIntervall { get; set; }
        public int ShutdownWait { get; set; }
        public int MaximumInitializeTime { get; set; }

        public TimeSpan ShutdownGrace { get; set; }
    }
}