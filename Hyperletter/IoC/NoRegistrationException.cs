using System;

namespace Hyperletter.IoC {
    public class NoRegistrationException : Exception {
        public NoRegistrationException(string message) : base(message) {
        }
    }
}