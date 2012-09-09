using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class Registration {
        public abstract void Invoke(TypedSocket socket, ILetter letter, Type concreteType);
    }
}