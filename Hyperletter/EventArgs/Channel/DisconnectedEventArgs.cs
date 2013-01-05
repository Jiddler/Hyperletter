using System;

namespace Hyperletter.EventArgs.Channel {
    internal class DisconnectedEventArgs : AbstractChannelEventArgs, IDisconnectedEventArgs {
        public ShutdownReason Reason { get; internal set; }
        public Guid RemoteNodeId { get; internal set; }
    }
}