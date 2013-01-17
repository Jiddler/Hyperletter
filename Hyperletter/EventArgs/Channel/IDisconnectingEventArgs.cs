using System;

namespace Hyperletter.EventArgs.Channel {
    public interface IDisconnectingEventArgs : IChannelEventArgs {
        ShutdownReason Reason { get; }
        Guid RemoteNodeId { get; }
    }
}