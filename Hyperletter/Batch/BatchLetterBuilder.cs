using System.Collections.Concurrent;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Batch {
    internal class BatchLetterBuilder {
        private readonly ConcurrentQueue<ILetter> _letters = new ConcurrentQueue<ILetter>();
        private readonly int _maxLetters;
        private readonly LetterSerializer _serializer;

        private LetterOptions _batchOptions = LetterOptions.None;

        public BatchLetterBuilder(SocketOptions socketOptions, LetterSerializer serializer) {
            _maxLetters = socketOptions.Batch.MaxLetters;
            _serializer = serializer;
        }

        public bool IsFull {
            get { return _letters.Count >= _maxLetters; }
        }

        public bool IsEmpty {
            get { return _letters.Count == 0; }
        }

        public void Add(ILetter letter) {
            if((letter.Options & LetterOptions.Ack) == LetterOptions.Ack)
                _batchOptions = LetterOptions.Ack;

            _letters.Enqueue(letter);
        }

        public Letter.Letter Build() {
            var letter = new Letter.Letter {Type = LetterType.Batch, Options = _batchOptions};

            int lettersInBatch = _letters.Count;
            letter.Parts = new byte[lettersInBatch][];
            for(int i = 0; i < lettersInBatch; i++)
                letter.Parts[i] = _serializer.Serialize(_letters.Dequeue());

            return letter;
        }

        public void Clear() {
            _letters.Clear();
        }
    }
}