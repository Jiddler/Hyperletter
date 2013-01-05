using System;

namespace Hyperletter.EventArgs.Channel {
    public interface IInitializedEventArgs : IChannelEventArgs {
        Guid RemoteNodeId { get; }
    }
}