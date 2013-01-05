using System;

namespace Hyperletter.EventArgs.Letter {
    public interface IDiscardedEventArgs : IChannelEventArgs {
        Guid RemoteNodeId { get; }
    }
}