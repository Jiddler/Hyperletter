using System;
using System.IO;

namespace Hyperletter.Letter {
    internal class LetterDeserializer {
        public ILetter Deserialize(byte[] serializedLetter) {
            var letter = new Letter();

            int position = 4;
            letter.Type = (LetterType) serializedLetter[position++];
            letter.Options = (LetterOptions) serializedLetter[position++];

            if ((letter.Options & LetterOptions.UniqueId) == LetterOptions.UniqueId) {
                letter.UniqueId = new Guid(GetByteRange(serializedLetter, position, 16));
                position += 16;
            }

            letter.Parts = ReadParts(serializedLetter, ref position);

            return letter;
        }

        private byte[][] ReadParts(byte[] serializedLetter, ref int position) {
            int partCount = GetLength(serializedLetter, position);
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
            return buffer[position] | (buffer[position + 1] << 8) | (buffer[position + 2] << 16) | (buffer[position + 3] << 24);
        }

        private byte[] GetByteRange(byte[] buffer, int startIndex, int length) {
            var result = new byte[length];
            Buffer.BlockCopy(buffer, startIndex, result, 0, length);
            return result;
        }
    }

    internal class LetterSerializer {
        public byte[] Serialize(ILetter letter) {
            var ms = new MemoryStream();

            WriteMetadata(letter, ms);
            WriteParts(letter, ms);
            WriteTotalLength(ms);

            return ms.ToArray();
        }

        private static void WriteTotalLength(Stream ms) {
            ms.Position = 0;
            ms.Write(Bytes((int)ms.Length), 0, 4);
        }

        private static void WriteMetadata(ILetter letter, Stream ms) {
            ms.Position = 4;
            ms.WriteByte((byte) letter.Type);
            ms.WriteByte((byte) letter.Options);
            if((letter.Options & LetterOptions.UniqueId) == LetterOptions.UniqueId) {
                ms.Write(letter.UniqueId.ToByteArray(), 0, 16);
            }
        }

        private static void WriteParts(ILetter letter, MemoryStream ms) {
            ms.Write(Bytes(letter.Parts == null ? 0x000000 : letter.Parts.Length), 0, 4);

            for(int i = 0; letter.Parts != null && i < letter.Parts.Length; i++)
                WritePart(letter.Parts[i], ms);
        }

        private static void WritePart(byte[] address, MemoryStream ms) {
            ms.Write(Bytes(address.Length), 0, 4);
            ms.Write(address, 0, address.Length);
        }

        private static byte[] Bytes(int parts) {
            var bytes = new byte[4];
            bytes[3] = (byte)(parts >> 24);
            bytes[2] = (byte)(parts >> 16);
            bytes[1] = (byte)(parts >> 8);
            bytes[0] = (byte)parts;
            return bytes;
        }
    }
}