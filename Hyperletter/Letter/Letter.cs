using System;

namespace Hyperletter.Letter {
    public class Letter : ILetter {
        private Guid? _id;

        public Letter() {
            Type = LetterType.User;
        }

        public Letter(LetterOptions options) : this() {
            Options = options;
        }

        public Letter(LetterOptions options, byte[] userPart) : this() {
            Type = LetterType.User;
            Options = options;
            Parts = new[] {userPart};
        }

        public Guid UniqueId {
            get { return (_id ?? (_id = Guid.NewGuid())).Value; }
            set { _id = value; }
        }

        public LetterType Type { get; set; }
        public LetterOptions Options { get; set; }

        public byte[][] Parts { get; set; }
    }
}