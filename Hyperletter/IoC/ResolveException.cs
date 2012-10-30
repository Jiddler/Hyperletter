using System;

namespace Hyperletter.IoC {
    public class ResolveException : Exception {
        public ResolveException(string message) : base(message) {
        }
    }
}