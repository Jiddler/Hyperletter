using System;
using System.IO;

namespace Hyperletter.Letter {
    internal class LetterSerializer {
        private readonly Guid _address;

        public LetterSerializer(Guid address) {
            _address = address;
        }

        public byte[] Serialize(ILetter letter) {
            var ms = new MemoryStream();
            WriteMetadata(letter, ms);
            WriteAddress(letter, ms);
            WriteParts(letter, ms);
            WriteTotalLength(ms);

            return ms.ToArray();
        }

        private void WriteAddress(ILetter letter, Stream ms) {
            ms.Write(BitConverter.GetBytes(letter.Address.Length + 1), 0, 2);

            ms.Write(_address.ToByteArray(), 0, 16);

            for(int i = 0; i < letter.Address.Length; i++)
                ms.Write(letter.Address[i].ToByteArray(), 0, 16);
        }

        private static void WriteTotalLength(Stream ms) {
            ms.Position = 0;
            ms.Write(BitConverter.GetBytes(ms.Length), 0, 4);
        }

        private static void WriteMetadata(ILetter letter, Stream ms) {
            ms.Position = 4;
            ms.WriteByte((byte) letter.Type);
            ms.WriteByte((byte) letter.Options);
            if(letter.Options.HasFlag(LetterOptions.UniqueId))
                ms.Write(letter.Id.ToByteArray(), 0, 16);
        }

        private static void WriteParts(ILetter letter, MemoryStream ms) {
            ms.Write(BitConverter.GetBytes(letter.Parts == null ? 0x000000 : letter.Parts.Length), 0, 4);

            for(int i = 0; letter.Parts != null && i < letter.Parts.Length; i++)
                WritePart(letter.Parts[i], ms);
        }

        private static void WritePart(byte[] address, MemoryStream ms) {
            ms.Write(BitConverter.GetBytes(address.Length), 0, 4);
            ms.Write(address, 0, address.Length);
        }

        public ILetter Deserialize(byte[] serializedLetter) {
            var letter = new Letter();

            int position = 4;
            letter.Type = (LetterType) serializedLetter[position++];
            letter.Options = (LetterOptions) serializedLetter[position++];

            if(letter.Options.HasFlag(LetterOptions.UniqueId)) {
                letter.Id = new Guid(GetByteRange(serializedLetter, 6, 16));
                position += 16;
            }

            letter.Address = ReadAddress(serializedLetter, ref position);
            letter.Parts = ReadParts(serializedLetter, ref position);

            return letter;
        }

        private Guid[] ReadAddress(byte[] serializedLetter, ref int position) {
            short addressCount = BitConverter.ToInt16(serializedLetter, position);
            position += 2;

            var address = new Guid[addressCount];
            for(int i = 0; i < addressCount; i++) {
                address[i] = new Guid(GetByteRange(serializedLetter, position, 16));
                position += 16;
            }

            return address;
        }

        private byte[][] ReadParts(byte[] serializedLetter, ref int position) {
            int partCount = BitConverter.ToInt32(serializedLetter, position);
            var parts = new byte[partCount][];

            if(partCount == 0)
                return parts;

            position += 4;
            int i = 0;
            while(position < serializedLetter.Length) {
                int partLength = GetLength(serializedLetter, position);
                position += 4;

                parts[i++] = GetByteRange(serializedLetter, position, partLength);
                position += partLength;
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