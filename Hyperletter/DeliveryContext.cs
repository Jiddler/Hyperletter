namespace Hyperletter {
    internal class DeliveryContext {
        public ILetter Send { get; private set; }
        public ILetter Context { get; private set; }

        public DeliveryContext(ILetter send, ILetter context) {
            Send = send;
            Context = context;
        }
    }
}