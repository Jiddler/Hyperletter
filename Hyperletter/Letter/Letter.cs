namespace Hyperletter.Letter {
    public class Letter : ILetter {
        public LetterType Type { get; set; }
        public LetterOptions Options { get; set; }

        public byte[][] Parts { get; set; }

        public Letter() {
            Type = LetterType.User;
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