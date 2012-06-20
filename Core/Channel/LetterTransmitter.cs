using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;
using Hyperletter.Core.Collection;

namespace Hyperletter.Core.Channel {
    internal class LetterTransmitter {
        private readonly Socket _socket;
        private readonly LetterSerializer _letterSerializer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly BlockingCollection<TransmitContext> _queue = new BlockingCollection<TransmitContext>();
        private Task _transmitTask;

        public event Action<TransmitContext> Sent;
        public event Action SocketError;

        public LetterTransmitter(Socket socket, CancellationTokenSource cancellationTokenSource) {
            _socket = socket;
            _cancellationTokenSource = cancellationTokenSource;

            _letterSerializer = new LetterSerializer();
        }

        public void Start() {
            _transmitTask = new Task(Transmit, _cancellationTokenSource.Token);
            _transmitTask.Start();
        }

        public void Enqueue(TransmitContext transmitContext) {
            _queue.Add(transmitContext);
        }

        public void TransmitHeartbeat() {
            var letter = new Letter { Type = LetterType.Heartbeat, Options = LetterOptions.NoAck | LetterOptions.SilentDiscard | LetterOptions.NoRequeue };
            Enqueue(new TransmitContext(letter));
        }

        private void Transmit() {
            try {
                while (true) {
                    var transmitContext = _queue.Take(_cancellationTokenSource.Token);
                    var serializedLetter = _letterSerializer.Serialize(transmitContext.Letter);

                    if (Send(serializedLetter) != System.Net.Sockets.SocketError.Success)
                        SocketError();
                    else if (transmitContext.Letter.Type != LetterType.Heartbeat)
                        Sent(transmitContext);
                }
            } catch (OperationCanceledException) {
            }
        }

        private SocketError Send(byte[] serializedLetter) {
            var status = System.Net.Sockets.SocketError.Success;
            try {
                _socket.Send(serializedLetter, 0, serializedLetter.Length, SocketFlags.None, out status);
            } catch (SocketException) {
            }
            return status;
        }
    }
}
