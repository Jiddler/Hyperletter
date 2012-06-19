using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;

namespace Hyperletter.Core.Channel {
    internal class LetterReceiver {
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly LetterSerializer _letterSerializer;

        private readonly byte[] _tcpReceiveBuffer = new byte[512];
        private readonly MemoryStream _receiveBuffer = new MemoryStream(512);

        private Task _receiveTask;
        private int _currentLength;

        public event Action<ILetter> Received;
        public event Action SocketError;

        public LetterReceiver(Socket socket, CancellationTokenSource cancellationTokenSource) {
            _socket = socket;
            _cancellationTokenSource = cancellationTokenSource;
            _letterSerializer = new LetterSerializer();
        }

        public void Start() {
            _receiveTask = new Task(Receive);
            _receiveTask.Start();
        }

        private void Receive() {
            while (true) {
                if (_cancellationTokenSource.IsCancellationRequested)
                    return;

                try {
                    SocketError status;
                    var read = _socket.Receive(_tcpReceiveBuffer, 0, _tcpReceiveBuffer.Length, SocketFlags.None, out status);
                    if (status != System.Net.Sockets.SocketError.Success || read == 0)
                        SocketError();
                    else
                        HandleReceived(_tcpReceiveBuffer, read);
                } catch (SocketException) {
                    SocketError();
                }
            }
        }

        private void HandleReceived(byte[] buffer, int length) {
            int bufferPosition = 0;
            while (bufferPosition < length) {
                if (IsNewMessage()) {
                    _currentLength = BitConverter.ToInt32(buffer, bufferPosition);
                }

                var write = (int)Math.Min(_currentLength - _receiveBuffer.Length, length - bufferPosition);
                _receiveBuffer.Write(buffer, bufferPosition, write);
                bufferPosition += write;

                if (!ReceivedFullLetter())
                    return;

                var letter = _letterSerializer.Deserialize(_receiveBuffer.ToArray());
                _receiveBuffer.SetLength(0);

                if(letter.Type != LetterType.Heartbeat)
                    Received(letter);
            }
        }
        
        private bool ReceivedFullLetter() {
            return _receiveBuffer.Length == _currentLength;
        }

        private bool IsNewMessage() {
            return _receiveBuffer.Length == 0;
        }
    }
}