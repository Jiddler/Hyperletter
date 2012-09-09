using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class LetterTransmitter {
        private readonly Socket _socket;
        private readonly LetterSerializer _letterSerializer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private Task _transmitTask;

        public event Action<ILetter> Sent;
        public event Action SocketError;

        public LetterTransmitter(LetterSerializer letterSerializer, TcpClient client, CancellationTokenSource cancellationTokenSource) {
            _socket = client.Client;
            _cancellationTokenSource = cancellationTokenSource;
            _letterSerializer = letterSerializer;
        }

        public void Start() {
            _transmitTask = new Task(Transmit, _cancellationTokenSource.Token);
            _transmitTask.Start();
        }

        public void Enqueue(ILetter letter) {
            _queue.Enqueue(letter);
            _resetEvent.Set();
        }
        
        private void Transmit() {
            try {
                while (true) {
                    _resetEvent.WaitOne();
                    ILetter letter;
                    while(_queue.TryDequeue(out letter)) {
                        var serializedLetter = _letterSerializer.Serialize(letter);
                        
                        if (!Send(serializedLetter)) {
                            SocketError();
                            return;
                        }

                        Sent(letter);
                    }
                }
            } catch (OperationCanceledException) {
            }
        }

        private bool Send(byte[] serializedLetter) {
            SocketError status;
            try {
                _socket.Send(serializedLetter, 0, serializedLetter.Length, SocketFlags.None, out status);
            } catch (Exception) {
                return false;
            }
            return status == System.Net.Sockets.SocketError.Success;
        }

        
    }
}
