namespace Hyperletter.EventArgs.Channel {
    public interface IDisconnectedEventArgs : IChannelEventArgs {
        ShutdownReason Reason { get; }
    }
}