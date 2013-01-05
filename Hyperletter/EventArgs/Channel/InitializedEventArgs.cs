using System;

namespace Hyperletter.EventArgs.Channel {
    internal class InitializedEventArgs : AbstractChannelEventArgs, IInitializedEventArgs {
        public Guid RemoteNodeId { get; internal set; }
    }
}