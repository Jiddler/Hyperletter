namespace Hyperletter.EventArgs {
    public interface IChannelEventArgs : ISocketEventArgs {
        Binding Binding { get; }
    }
}