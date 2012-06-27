using System.Collections.Generic;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core.Buffered {
    internal class BatchLetterBuilder {
        private readonly List<ILetter> _letters = new List<ILetter>();
        private readonly LetterSerializer _serializer;

        public const int MaxLettersInOneBatch = 1000;
        
        public int Count {
            get { return _letters.Count; }
        }

        private LetterOptions _batchOptions = LetterOptions.None;

        public BatchLetterBuilder() {
            _serializer = new LetterSerializer();
        }

        public void Add(ILetter letter) {
            if(letter.Options.IsSet(LetterOptions.Ack))
                _batchOptions = LetterOptions.Ack;

            _letters.Add(letter);
        }

        public Letter Build() {
            var letter = new Letter { Type = LetterType.Batch, Options = _batchOptions };

            letter.Parts = new Part[_letters.Count];
            for (int i = 0; i < _letters.Count; i++)
                letter.Parts[i] = new Part { PartType = PartType.Letter, Data = _serializer.Serialize(_letters[i]) };

            _letters.Clear();

            return letter;
        }
    }
}
