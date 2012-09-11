using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class LetterTransmitter {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly LetterSerializer _letterSerializer;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly Socket _socket;
        private ILetter _currentLetter;
        private bool _sending;

        public LetterTransmitter(LetterSerializer letterSerializer, TcpClient client, CancellationTokenSource cancellationTokenSource) {
            _socket = client.Client;
            _cancellationTokenSource = cancellationTokenSource;
            _letterSerializer = letterSerializer;
        }

        public event Action<ILetter> Sent;
        public event Action SocketError;

        public void Start() {
            _sendEventArgs.Completed += SendEventArgsOnCompleted;
        }

        public void Enqueue(ILetter letter) {
            TrySend(letter);
        }

        private void TrySend(ILetter letter = null) {
            if(_cancellationTokenSource.IsCancellationRequested)
                return;

            lock(this) {
                if(_sending) {
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

                _sending = true;

                BeginSend(_currentLetter);
            }
        }

        private void BeginSend(ILetter letter) {
            _currentLetter = letter;
            byte[] serializedLetter = _letterSerializer.Serialize(letter);
            _sendEventArgs.SetBuffer(serializedLetter, 0, serializedLetter.Length);
            try {
                bool pending = _socket.SendAsync(_sendEventArgs);
                if(!pending)
                    EndSend(_sendEventArgs);
            } catch(Exception) {
                SocketError();
            }
        }

        private void SendEventArgsOnCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs) {
            EndSend(socketAsyncEventArgs);
        }

        private void EndSend(SocketAsyncEventArgs socketAsyncEvent) {
            SocketError status = socketAsyncEvent.SocketError;
            int sent = socketAsyncEvent.BytesTransferred;
            if(status != System.Net.Sockets.SocketError.Success || sent == 0) {
                SocketError();
            } else {
                Sent(_currentLetter);
                _sending = false;
                TrySend();
            }
        }
    }
}