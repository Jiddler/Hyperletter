using System.Collections.Concurrent;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Batch {
    internal class BatchLetterBuilder {
        public const int MaxLettersInOneBatch = 1000;
        private readonly ConcurrentQueue<ILetter> _letters = new ConcurrentQueue<ILetter>();
        private readonly int _maxLetters;
        private readonly LetterSerializer _serializer;

        private LetterOptions _batchOptions = LetterOptions.None;

        public BatchLetterBuilder(int maxLetters, LetterSerializer serializer) {
            _maxLetters = maxLetters;
            _serializer = serializer;
        }

        public int Count {
            get { return _letters.Count; }
        }

        public void Add(ILetter letter) {
            if(letter.Options.IsSet(LetterOptions.Ack))
                _batchOptions = LetterOptions.Ack;

            _letters.Enqueue(letter);
        }

        public Letter.Letter Build() {
            var letter = new Letter.Letter {Type = LetterType.Batch, Options = _batchOptions};

            int lettersInBatch = _letters.Count < _maxLetters ? _letters.Count : _maxLetters;
            letter.Parts = new byte[lettersInBatch][];
            for(int i = 0; i < lettersInBatch; i++)
                letter.Parts[i] = _serializer.Serialize(_letters.Dequeue());

            return letter;
        }
    }
}