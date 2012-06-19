using Hyperletter.Abstraction;

namespace Hyperletter.Core.Channel {
    internal class TransmitContext {
        public ILetter Letter { get; private set; }
        public ILetter Context { get; private set; }
        
        public TransmitContext(ILetter toSend) {
            Letter = toSend;
        }

        public TransmitContext(ILetter toSend, ILetter context) {
            Letter = toSend;
            Context = context;
        }
    }
}