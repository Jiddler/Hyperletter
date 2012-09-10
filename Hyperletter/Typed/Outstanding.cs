using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Outstanding {
        public DateTime Created { get; private set; }

        protected Outstanding() {
            Created = DateTime.UtcNow;
        }

        public abstract void SetResult(Metadata metadata, ILetter letter);
    }
}