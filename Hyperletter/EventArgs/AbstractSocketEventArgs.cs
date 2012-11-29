namespace Hyperletter.EventArgs {
    internal class AbstractSocketEventArgs : ISocketEventArgs {
        public IHyperSocket Socket { get; internal set; }
    }
}