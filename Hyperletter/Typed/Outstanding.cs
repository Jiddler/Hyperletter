using System;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Outstanding {
        protected Outstanding() {
            Created = DateTime.UtcNow;
        }

        public DateTime Created { get; private set; }

        public abstract void SetResult(Metadata metadata, ILetter letter, IReceivedEventArgs receivedEventArgs);
        public abstract void SetResult(Exception exception);
    }
}