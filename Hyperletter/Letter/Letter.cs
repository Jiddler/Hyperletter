using System;

namespace Hyperletter.Letter {
    public class Letter : ILetter {
        private static readonly Guid[] EmptyAddress = new Guid[0];

        private Guid? _id;

        public Letter() {
            Address = EmptyAddress;
        }

        public Letter(LetterOptions options) {
            Address = EmptyAddress;
            Options = options;
        }

        public Letter(LetterOptions options, byte[] userPart) : this() {
            Type = LetterType.User;
            Options = options;
            Parts = new[] {userPart};
        }

        public Guid Id {
            get {
                if(_id == null)
                    _id = Guid.NewGuid();
                return _id.Value;
            }
            set { _id = value; }
        }

        public LetterType Type { get; set; }
        public LetterOptions Options { get; set; }

        public Guid[] Address { get; set; }
        public byte[][] Parts { get; set; }
    }
}