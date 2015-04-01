using System;
using Hyperletter.Batch;

namespace Hyperletter {
    public class SocketOptions {
        public SocketOptions() {
            Batch = new BatchOptions {Enabled = true, Extend = TimeSpan.FromMilliseconds(100), MaxExtend = TimeSpan.FromSeconds(1), MaxLetters = 4000};
            Heartbeat = new HeartbeatOptions {Interval = 1000};
            Notification = new NotificationOptions {ReceivedNotifyOnAllAckStates = false};
            NodeId = Guid.NewGuid();
            ReconnectInterval = TimeSpan.FromMilliseconds(1000);
            ShutdownWait = TimeSpan.FromMilliseconds(1500);
            MaximumInitializeTime = TimeSpan.FromMilliseconds(4000);
        }

        public BatchOptions Batch { get; private set; }
        public HeartbeatOptions Heartbeat { get; private set; }
        public NotificationOptions Notification { get; private set; }

        public Guid NodeId { get; set; }

        public TimeSpan ReconnectInterval { get; set; }
        public TimeSpan ShutdownWait { get; set; }
        public TimeSpan MaximumInitializeTime { get; set; }

        public TimeSpan ShutdownGrace { get; set; }
    }
}