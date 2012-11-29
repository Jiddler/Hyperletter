using System;

namespace Hyperletter.EventArgs.Letter {
    public interface IReceivedEventArgs : IChannelEventArgs {
        Guid RemoteNodeId { get; }
        bool Acked { get; }
    }
}