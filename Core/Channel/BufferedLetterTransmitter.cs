using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;

namespace Hyperletter.Core.Channel {
    internal class BufferedLetterTransmitter {
        private readonly LetterSerializer _letterSerializer;
        private readonly TcpClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentQueue<TransmitContext> _queue = new ConcurrentQueue<TransmitContext>();
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private Task _transmitTask;
        private readonly BufferedStream _bufferedWriter;

        public event Action<TransmitContext> Sent;
        public event Action SocketError;
        public event Action CanSendMore;

        public BufferedLetterTransmitter(TcpClient client, CancellationTokenSource cancellationTokenSource) {
            _bufferedWriter = new BufferedStream(client.GetStream());
            _client = client;
            _cancellationTokenSource = cancellationTokenSource;

            _letterSerializer = new LetterSerializer();
        }

        public void Start() {
            _transmitTask = new Task(Transmit, _cancellationTokenSource.Token);
            _transmitTask.Start();
        }

        public void Enqueue(TransmitContext transmitContext) {
            _queue.Enqueue(transmitContext);
            _resetEvent.Set();
        }

        public void TransmitHeartbeat() {
            var letter = new Letter { Type = LetterType.Heartbeat, Options = LetterOptions.NoAck | LetterOptions.SilentDiscard | LetterOptions.NoRequeue };
            Enqueue(new TransmitContext(letter));
        }

        private void Transmit() {
            try {
                while (true) {
                    _resetEvent.WaitOne();
                    TransmitContext transmitContext;
                    
                    var buffered = new Queue<TransmitContext>();
                    while (_queue.TryDequeue(out transmitContext)) {
                        var serializedLetter = _letterSerializer.Serialize(transmitContext.Letter);

                        if (!Send(serializedLetter))
                            SocketError();
                        else if (transmitContext.Letter.Type != LetterType.Heartbeat) {
                            CanSendMore();
                            buffered.Enqueue(transmitContext);
                        }
                    }

                    try {
                        _bufferedWriter.Flush();
                        while (buffered.Count > 0) {
                            Sent(buffered.Dequeue());
                        }
                    } catch (IOException) {
                        SocketError();
                    }
                }
            } catch (OperationCanceledException) {
            }
        }

        private bool Send(byte[] serializedLetter) {
            try {
                _bufferedWriter.Write(serializedLetter, 0, serializedLetter.Length);
                return true;
            } catch (Exception) {
            }
            return false;
        }
    }
}
