using System;

namespace Hyperletter.EventArgs.Letter {
    internal class SentEventArgs : AbstractChannelEventArgs, ISentEventArgs {
        public Guid RemoteNodeId { get; internal set; }
    }
}