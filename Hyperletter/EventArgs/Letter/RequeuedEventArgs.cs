using System;

namespace Hyperletter.EventArgs.Letter {
    internal class RequeuedEventArgs : AbstractChannelEventArgs, IRequeuedEventArgs {
        public Guid RemoteNodeId { get; internal set; }
    }
}