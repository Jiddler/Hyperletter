using System;

namespace Hyperletter.Letter {
    public class Letter : ILetter {
        public Guid RemoteNodeId { get; internal set; }

        public LetterType Type { get; set; }
        public LetterOptions Options { get; set; }

        public byte[][] Parts { get; set; }

        public Letter() {
        }

        public Letter(LetterOptions options) {
            Options = options;
        }

        public Letter(LetterOptions options, byte[] userPart) : this() {
            Type = LetterType.User;
            Options = options;
            Parts = new[] {userPart};
        }
    }
}