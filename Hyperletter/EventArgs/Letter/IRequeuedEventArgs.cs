using System;

namespace Hyperletter.EventArgs.Letter {
    public interface IRequeuedEventArgs : IChannelEventArgs {
        Guid RemoteNodeId { get; }
    }
}
