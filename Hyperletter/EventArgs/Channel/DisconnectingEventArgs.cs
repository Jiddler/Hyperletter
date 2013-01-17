using System;

namespace Hyperletter.EventArgs.Channel {
    internal class DisconnectingEventArgs : AbstractChannelEventArgs, IDisconnectingEventArgs {
        public ShutdownReason Reason { get; internal set; }
        public Guid RemoteNodeId { get; internal set; }
    }
}