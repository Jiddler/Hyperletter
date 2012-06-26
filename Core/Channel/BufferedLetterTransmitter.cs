using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;
using Hyperletter.Core.Stream;

namespace Hyperletter.Core.Channel {
    internal class BufferedLetterTransmitter {
        private readonly LetterSerializer _letterSerializer;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _buffered = new ConcurrentQueue<ILetter>();
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private Task _transmitTask;
        private readonly SizeBufferedStream _bufferedWriter;

        public event Action<ILetter> Sent;
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
                Sent(_buffered.Dequeue());
            }
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
                    while (_queue.TryDequeue(out letter)) {
                        var serializedLetter = _letterSerializer.Serialize(letter);

                        _buffered.Enqueue(letter);

                        if (!Send(serializedLetter)) {
                            SocketError();
                            return;
                        }
                       
                        if (letter.Type != LetterType.Heartbeat)
                            CanSendMore();
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
