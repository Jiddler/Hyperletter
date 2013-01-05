using System;

namespace Hyperletter.EventArgs.Letter {
    internal class DiscardedEventArgs : AbstractChannelEventArgs, IDiscardedEventArgs {
        public Guid RemoteNodeId { get; internal set; }
    }
}