using System;
using System.IO;
using System.Net.Sockets;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class LetterReceiver {
        private readonly byte[] _lengthBuffer = new byte[4];
        private readonly LetterDeserializer _letterDeserializer;

        private readonly Socket _socket;
        private readonly byte[] _tcpReceiveBuffer = new byte[4096];
        private Guid _connectedTo;
        private SocketAsyncEventArgs _receiveEventArgs = new SocketAsyncEventArgs();

        private MemoryStream _receiveBuffer = new MemoryStream();

        private int _currentLength;
        private int _lengthPosition;
        private bool _initalized;
        private bool _shutdownRequested;

        public event Action<ILetter> Received;
        public event Action<ShutdownReason> SocketError;

        public LetterReceiver(Socket socket, LetterDeserializer letterDeserializer) {
            _socket = socket;
            _letterDeserializer = letterDeserializer;
        }

        public bool Receiving { get; private set; }

        public void Start() {
            _receiveEventArgs.SetBuffer(_tcpReceiveBuffer, 0, _tcpReceiveBuffer.Length);
            _receiveEventArgs.Completed += ReceiveEventArgsOnCompleted;

            BeginReceive();
        }

        public void Stop() {
            _shutdownRequested = true;
        }

        private void ReceiveEventArgsOnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs) {
            EndReceived(socketAsyncEventArgs);
        }

        private void BeginReceive() {
            if (_shutdownRequested)
                return;

            try {
                Receiving = true;
                bool pending = _socket.ReceiveAsync(_receiveEventArgs);
                if(!pending)
                    EndReceived(_receiveEventArgs);
            } catch(Exception) {
                HandleSocketError(ShutdownReason.Socket);
            }
        }

        private void EndReceived(SocketAsyncEventArgs socketAsyncEvent) {
            SocketError status = socketAsyncEvent.SocketError;
            int read = socketAsyncEvent.BytesTransferred;
            if(status != System.Net.Sockets.SocketError.Success || read == 0) {
                HandleSocketError(ShutdownReason.Socket);
            } else {
                try {
                    HandleReceived(_tcpReceiveBuffer, read);
                    Receiving = false;
                    BeginReceive();
                } catch(Exception) {
                    HandleSocketError(ShutdownReason.Incompatible);
                }
            }
        }

        private void HandleSocketError(ShutdownReason reason) {
            Receiving = false;
            if(SocketError != null) SocketError(reason);
        }

        private void HandleReceived(byte[] buffer, int length) {
            int bufferPosition = 0;
            while(bufferPosition < length) {
                if(IsNewMessage()) {
                    int lengthPositionBefore = _lengthPosition;
                    int read = ReadNewLetterLength(buffer, bufferPosition);
                    if(read < 4) {
                        _receiveBuffer.Write(_lengthBuffer, lengthPositionBefore, read);
                    }

                    if(_currentLength == 0)
                        return;
                }

                if(!_initalized && (_currentLength != 30 && _currentLength != 10)) {
                    HandleSocketError(ShutdownReason.Incompatible);
                    return;
                }

                var write = (int) Math.Min(_currentLength - _receiveBuffer.Length, length - bufferPosition);
                _receiveBuffer.Write(buffer, bufferPosition, write);
                bufferPosition += write;

                if(!ReceivedFullLetter())
                    return;

                var letter = _letterDeserializer.Deserialize(_connectedTo, _receiveBuffer.ToArray());
                _receiveBuffer = new MemoryStream();
                _currentLength = 0;

                if (letter.Type == LetterType.Initialize) {
                    _initalized = true;
                    _connectedTo = letter.RemoteNodeId;
                }

                if(letter.Type != LetterType.Heartbeat)
                    Received(letter);
            }
        }

        private int ReadNewLetterLength(byte[] buffer, int bufferPosition) {
            int bytesToRead = (buffer.Length - bufferPosition);
            if(bytesToRead < 4 || _lengthPosition != 0) {
                for(; bufferPosition < buffer.Length && _lengthPosition < 4; bufferPosition++, _lengthPosition++)
                    _lengthBuffer[_lengthPosition] = buffer[bufferPosition];

                if(_lengthPosition != 4)
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

        public void Dispose()
        {
            _receiveEventArgs.Completed -= ReceiveEventArgsOnCompleted;
            _receiveEventArgs = null;
        }
    }
}