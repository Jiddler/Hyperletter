using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Core.Letter;

namespace Hyperletter.Core.Channel {
    internal class LetterReceiver {
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly LetterSerializer _letterSerializer;

        private readonly byte[] _tcpReceiveBuffer = new byte[4096];
        private readonly MemoryStream _receiveBuffer = new MemoryStream();

        private readonly SocketAsyncEventArgs _receiveEventArgs = new SocketAsyncEventArgs();
        
        private readonly byte[] _lengthBuffer = new byte[4];
        private int _lengthPosition;
        private int _currentLength;

        public event Action<ILetter> Received;
        public event Action SocketError;

        public LetterReceiver(Socket socket, CancellationTokenSource cancellationTokenSource) {
            _socket = socket;
            _cancellationTokenSource = cancellationTokenSource;
            _letterSerializer = new LetterSerializer();
        }

        public void Start() {
            _receiveEventArgs.SetBuffer(_tcpReceiveBuffer, 0, _tcpReceiveBuffer.Length);
            _receiveEventArgs.Completed += ReceiveEventArgsOnCompleted;

            BeginReceive();
        }

        private void ReceiveEventArgsOnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs) {
            EndReceived(socketAsyncEventArgs);
        }

        private void BeginReceive() {
            if (_cancellationTokenSource.IsCancellationRequested)
                return;

            try {
                var pending =_socket.ReceiveAsync(_receiveEventArgs);
                if(!pending)
                    EndReceived(_receiveEventArgs);
            } catch (Exception) {
                SocketError();
            }
        }

        private void EndReceived(SocketAsyncEventArgs socketAsyncEvent) {
            SocketError status = socketAsyncEvent.SocketError;
            var read = socketAsyncEvent.BytesTransferred;
            if (status != System.Net.Sockets.SocketError.Success || read == 0) {
                SocketError();
            } else {
                HandleReceived(_tcpReceiveBuffer, read);
                BeginReceive();
            }
        }

        private void HandleReceived(byte[] buffer, int length) {
            int bufferPosition = 0;
            while (bufferPosition < length) {
                if (IsNewMessage()) {
                    var lengthPositionBefore = _lengthPosition;
                    var read = ReadNewLetterLength(buffer, bufferPosition);
                    if(read < 4) {
                        _receiveBuffer.Write(_lengthBuffer, lengthPositionBefore, read);
                    }

                    if (_currentLength == 0)
                        return;
                }

                var write = (int)Math.Min(_currentLength - _receiveBuffer.Length, length - bufferPosition);
                _receiveBuffer.Write(buffer, bufferPosition, write);
                bufferPosition += write;

                if (!ReceivedFullLetter())
                    return;

                var letter = _letterSerializer.Deserialize(_receiveBuffer.ToArray());
                _receiveBuffer.SetLength(0);
                _currentLength = 0;

                if (letter.Type != LetterType.Heartbeat)
                    Received(letter);
            }
        }

        private int ReadNewLetterLength(byte[] buffer, int bufferPosition) {
            var bytesToRead = (buffer.Length - bufferPosition);
            if (bytesToRead < 4 || _lengthPosition != 0) {
                for (; bufferPosition < buffer.Length && _lengthPosition < 4; bufferPosition++, _lengthPosition++)
                    _lengthBuffer[_lengthPosition] = buffer[bufferPosition];

                if (_lengthPosition != 4)
                    return bytesToRead;

                _lengthPosition = 0;
                _currentLength = BitConverter.ToInt32(_lengthBuffer, 0);
            } else
                _currentLength = BitConverter.ToInt32(buffer, bufferPosition);

            return 4;
        }

        private bool ReceivedFullLetter() {
            return _receiveBuffer.Length == _currentLength;
        }

        private bool IsNewMessage() {
            return _currentLength == 0;
        }
    }
}