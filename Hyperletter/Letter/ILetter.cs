namespace Hyperletter.Letter {
    public interface ILetter {
        LetterType Type { get; }
        LetterOptions Options { get; }
        byte[][] Parts { get; }
    }
}