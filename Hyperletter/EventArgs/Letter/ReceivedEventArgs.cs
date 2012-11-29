using System;

namespace Hyperletter.EventArgs.Letter {
    internal class ReceivedEventArgs : AbstractChannelEventArgs, IReceivedEventArgs {
        public Guid RemoteNodeId { get; internal set; }
        public bool Acked { get; internal set; }
    }
}