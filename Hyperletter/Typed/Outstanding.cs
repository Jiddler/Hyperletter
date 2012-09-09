using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Outstanding {
        public abstract void SetResult(Metadata metadata, ILetter letter);
    }
}