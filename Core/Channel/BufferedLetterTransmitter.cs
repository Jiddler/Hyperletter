using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;
using Hyperletter.Core.Stream;

namespace Hyperletter.Core.Channel {
    internal class BufferedLetterTransmitter {
        private readonly LetterSerializer _letterSerializer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentQueue<TransmitContext> _queue = new ConcurrentQueue<TransmitContext>();
        private readonly ConcurrentQueue<TransmitContext> _buffered = new ConcurrentQueue<TransmitContext>();
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private Task _transmitTask;
        private readonly SizeBufferedStream _bufferedWriter;

        public event Action<TransmitContext> Sent;
        public event Action SocketError;
        public event Action CanSendMore;

        public BufferedLetterTransmitter(TcpClient client, CancellationTokenSource cancellationTokenSource) {
            _bufferedWriter = new SizeBufferedStream(client);
            _bufferedWriter.Flushed += BufferedWriterOnFlushed;
            _cancellationTokenSource = cancellationTokenSource;

            _letterSerializer = new LetterSerializer();
        }

        private void BufferedWriterOnFlushed(int flushed) {
            for (int i = 0; i < flushed && _buffered.Count > 0; i++) {
                TransmitContext context;
                _buffered.TryDequeue(out context);
                Sent(context);
            }
        }

        public void Start() {
            _transmitTask = new Task(Transmit, _cancellationTokenSource.Token);
            _transmitTask.Start();
        }

        public void Enqueue(TransmitContext transmitContext) {
            _queue.Enqueue(transmitContext);
            _resetEvent.Set();
        }

        private void Transmit() {
            try {
                while (true) {
                    _resetEvent.WaitOne();

                    TransmitContext transmitContext;
                    while (_queue.TryDequeue(out transmitContext)) {
                        var serializedLetter = _letterSerializer.Serialize(transmitContext.Letter);

                        _buffered.Enqueue(transmitContext);

                        if (!Send(serializedLetter)) {
                            SocketError();
                            return;
                        }
                       
                        if (transmitContext.Letter.Type != LetterType.Heartbeat) {
                            CanSendMore();
                        }
                    }

                    try {
                        _bufferedWriter.Flush();
                    } catch (Exception) {
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
