using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Registration {
        public abstract void Invoke(TypedHyperSocket socket, ILetter letter, Type concreteType);
    }
}