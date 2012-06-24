using System;
using System.IO;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    internal class LetterSerializer {
        public byte[] Serialize(ILetter letter) {
            var ms = new MemoryStream();
            WriteMetadata(letter, ms);
            if (letter.Type != LetterType.Ack && letter.Type != LetterType.Heartbeat)
                WriteParts(letter, ms);

            WriteTotalLength(ms);
            return ms.ToArray();
        }

        private static void WriteTotalLength(MemoryStream ms) {
            ms.Position = 0;
            ms.Write(BitConverter.GetBytes(ms.Length), 0, 4);
        }

        private static void WriteMetadata(ILetter letter, MemoryStream ms) {
            ms.Position = 4;
            ms.WriteByte((byte)letter.Type);
            ms.WriteByte((byte)letter.Options);
            if(letter.Options.IsSet(LetterOptions.UniqueId))
                ms.Write(letter.Id.ToByteArray(), 0, 16);
        }

        private static void WriteParts(ILetter letter, MemoryStream ms) {
            ms.Write(BitConverter.GetBytes(letter.Parts == null ? 0x000000 : letter.Parts.Length), 0, 4);
            for (int i = 0; letter.Parts != null && i < letter.Parts.Length; i++)
                WritePart(letter.Parts[i].PartType, letter.Parts[i].Data, ms);
        }

        private static void WritePart(PartType partType, byte[] address, MemoryStream ms) {
            ms.WriteByte((byte)partType);
            ms.Write(BitConverter.GetBytes(address.Length), 0, 4);
            ms.Write(address, 0, address.Length);
        }

        public ILetter Deserialize(byte[] serializedLetter) {
            var letter = new Letter();
            letter.Type = (LetterType) serializedLetter[4];
            letter.Options = (LetterOptions)serializedLetter[5];

            if(letter.Options.IsSet(LetterOptions.UniqueId))
                letter.Id = new Guid(GetByteRange(serializedLetter, 6, 16));

            if (letter.Type == LetterType.Ack || letter.Type == LetterType.Heartbeat)
                return letter;

            letter.Parts = GetParts(serializedLetter, letter.Options.IsSet(LetterOptions.UniqueId) ? 22 : 6);
            return letter;
        }

        private IPart[] GetParts(byte[] serializedLetter, int position) {
            var partCount = BitConverter.ToInt32(serializedLetter, position);
            var parts = new IPart[partCount];

            if (partCount == 0)
                return parts;

            position += 4;
            int i = 0;
            while (position < serializedLetter.Length) {
                var part = new Part();
                
                part.PartType = (PartType) serializedLetter[position];
                position += 1;
                var partLength = GetLength(serializedLetter, position);
                position += 4;
                part.Data = GetByteRange(serializedLetter, position, partLength);
                position += partLength;

                parts[i++] = part;
            }

            return parts;
        }

        private int GetLength(byte[] buffer, int position) {
            return BitConverter.ToInt32(buffer, position);
        }

        private byte[] GetByteRange(byte[] buffer, int startIndex, int length) {
            var result = new byte[length];
            Buffer.BlockCopy(buffer, startIndex, result, 0, length);
            return result;
        }
    }
}
