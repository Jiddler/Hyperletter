using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class LetterTransmitter : IDisposable {
        private readonly LetterSerializer _letterSerializer;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly Socket _socket;
        private ILetter _currentLetter;
        private bool _shutdownRequested;
        private SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();

        public bool Sending { get; private set; }

        public LetterTransmitter(Socket socket,  LetterSerializer letterSerializer) {
            _socket = socket;
            _letterSerializer = letterSerializer;
        }

        public event Action<ILetter> Sent;
        public event Action<ShutdownReason> SocketError;

        public void Start() {
            _sendEventArgs.Completed += SendEventArgsOnCompleted;
        }

        public void Stop() {
            _shutdownRequested = true;
        }

        public void Enqueue(ILetter letter) {
            TrySend(letter);
        }

        private void TrySend(ILetter letter = null) {
            if (_shutdownRequested)
                return;

            lock(this) {
                if(Sending) {
                    if(letter != null)
                        _queue.Enqueue(letter);

                    return;
                }

                if(_queue.TryDequeue(out _currentLetter)) {
                    if(letter != null)
                        _queue.Enqueue(letter);
                } else if(letter == null) {
                    return;
                } else {
                    _currentLetter = letter;
                }

                Sending = true;
                BeginSend(_currentLetter);
            }
        }

        private void HandleSocketError(ShutdownReason reason) {
            Sending = false;
            if(SocketError != null) SocketError(reason);
        }

        private void BeginSend(ILetter letter) {
            _currentLetter = letter;
            byte[] serializedLetter = _letterSerializer.Serialize(letter);
            _sendEventArgs.SetBuffer(serializedLetter, 0, serializedLetter.Length);

            try {
                var pending = _socket.SendAsync(_sendEventArgs);
                if(!pending)
                    EndSend(_sendEventArgs);
            } catch(Exception) {
                HandleSocketError(ShutdownReason.Socket);
            }
        }

        private void SendEventArgsOnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs) {
            EndSend(socketAsyncEventArgs);
        }

        private void EndSend(SocketAsyncEventArgs socketAsyncEvent) {
            SocketError status = socketAsyncEvent.SocketError;
            int sent = socketAsyncEvent.BytesTransferred;
            
            if(status != System.Net.Sockets.SocketError.Success || sent == 0) {
                HandleSocketError(ShutdownReason.Socket);
            } else {
                var sentLetter = _currentLetter;
                Sent(sentLetter);
                Sending = false;
                TrySend();
            }
        }

        public void Dispose() {
            _sendEventArgs.Completed -= SendEventArgsOnCompleted;
            _sendEventArgs = null;
        }
    }
}