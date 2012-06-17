using System;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class Letter : ILetter {
        private Guid? _id;
        public Guid Id {
            get {
                if (_id == null)
                    _id = Guid.NewGuid();
                return _id.Value;
            }
            set { _id = value; }
        }

        public LetterType Type { get; set; }
        public LetterOptions Options { get; set; }
        public IPart[] Parts { get; set; }
    }
}