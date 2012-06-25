using System;
using System.Net.Sockets;
using System.Threading;

namespace Hyperletter.Core.Stream {
    public sealed class SizeBufferedStream {
        private readonly TcpClient _client;         // Underlying stream.  Close sets _s to null.
        private readonly byte[] _buffer;    // Shared read/write buffer.  Alloc on first use.
        private readonly int _bufferSize;   // Length of internal buffer, if it's allocated.

        private int _writePos;     // Write pointer within shared buffer.
        private int _writesSinceFlush;

        private const int DefaultBufferSize = 4096;

        public event Action<int> Flushed;
 
        public SizeBufferedStream(TcpClient client) : this(client, DefaultBufferSize)
        {
        }

        public SizeBufferedStream(TcpClient client, int bufferSize) {
            _client = client;
            _bufferSize = bufferSize;
            _buffer = new byte[bufferSize];
        }
  
        public void Flush() {
            if (_writePos > 0) {
                _client.Client.Send(_buffer, 0, _writePos, SocketFlags.None);
                _writePos = 0;

                TriggerFlushed();
            }
        }

        private void TriggerFlushed() {
            var writes = _writesSinceFlush;
            _writesSinceFlush = 0;
            ThreadPool.QueueUserWorkItem(s => Flushed(writes));
        }

        public void Write(byte[] array, int offset, int count) {
            if (HasDataInBuffer()) {
                int numBytes = _bufferSize - _writePos;   // space left in buffer
                if (numBytes > 0) {
                    if (numBytes > count)
                        numBytes = count;
                    Buffer.BlockCopy(array, offset, _buffer, _writePos, numBytes);
                    _writePos += numBytes;

                    if (count == numBytes) {
                        _writesSinceFlush++;
                        return;
                    }

                    offset += numBytes;
                    count -= numBytes;
                }

                Flush();
            }

            // If the buffer would slow writes down, avoid buffer completely.
            if (count >= _bufferSize) {
                _client.Client.Send(array, offset, count, SocketFlags.None);

                _writesSinceFlush++;
                TriggerFlushed();

                return;
            }
            
            if (count == 0)
                return;  // Don't allocate a buffer then call memcpy for 0 bytes.
            
            // Copy remaining bytes into buffer, to write at a later date.
            Buffer.BlockCopy(array, offset, _buffer, 0, count);
            _writePos = count;
            
            _writesSinceFlush++;
        }

        private bool HasDataInBuffer() {
            return _writePos > 0;
        }
    }
}
