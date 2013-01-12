using System;

namespace Hyperletter.EventArgs.Letter {
    public interface ISentEventArgs : IChannelEventArgs {
        Guid RemoteNodeId { get; }
    }
}