namespace Hyperletter.EventArgs {
    internal abstract class AbstractChannelEventArgs : AbstractSocketEventArgs, IChannelEventArgs {
        public Binding Binding { get; internal set; }
    }
}