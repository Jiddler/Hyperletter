using System;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Registration {
        public abstract void Invoke(TypedHyperSocket socket, ILetter letter, Metadata metadata, Type concreteType, IReceivedEventArgs receivedEventArgs);
    }
}