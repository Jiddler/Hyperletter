using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Hyperletter.Channel;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Batch {
    internal class BatchChannel : IChannel {
        private readonly BatchLetterBuilder _batchBuilder;
        private readonly IChannel _channel;
        private readonly BatchOptions _options;
        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();

        private readonly Timer _slidingTimeoutTimer;

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly object _syncRoot = new object();
        private bool _sentBatch;

        public BatchChannel(SocketOptions options, IChannel channel, BatchLetterBuilder batchBuilder) {
            _channel = channel;
            _options = options.Batch;
            _batchBuilder = batchBuilder;

            _channel.ChannelConnected += abstractChannel => ChannelConnected?.Invoke(this);
            _channel.ChannelDisconnected += ChannelOnDisconnected;
            _channel.ChannelQueueEmpty += abstractChannel => {
                                              /* NOOP */
                                          };
            _channel.ChannelInitialized += ChannelOnInitialized;
            _channel.ChannelConnecting += abstractChannel => ChannelConnecting?.Invoke(this);
            _channel.ChannelDisconnecting += (abstractChannel, reason) => ChannelDisconnecting?.Invoke(this, reason);

            _channel.Received += ChannelOnReceived;
            _channel.Sent += ChannelOnSent;
            _channel.FailedToSend += ChannelOnFailedToSend;

            _slidingTimeoutTimer = new Timer(SlidingTimeoutTimerOnElapsed, null, -1, -1);
        }

        public bool IsConnected => _channel.IsConnected;

        public bool ShutdownRequested => _channel.ShutdownRequested;

        public Guid RemoteNodeId => _channel.RemoteNodeId;

        public Binding Binding => _channel.Binding;

        public Direction Direction => _channel.Direction;

        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel> ChannelConnecting;
        public event Action<IChannel, ShutdownReason> ChannelDisconnected;
        public event Action<IChannel, ShutdownReason> ChannelDisconnecting;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<ILetter, ReceivedEventArgs> Received;
        public event Action<IChannel, ILetter> Sent;
        public event Action<IChannel, ILetter> FailedToSend;

        public void Initialize() {
            _channel.Initialize();
        }

        public EnqueueResult Enqueue(ILetter letter) {
            ChangeTimerState(false);
            ChangeTimerState(true);

            if(!_sentBatch)
                _stopwatch.Restart();

            _queue.Enqueue(letter);
            _batchBuilder.Add(letter);

            return TrySendBatch(false);
        }

        public void Heartbeat() {
            _channel.Heartbeat();
        }

        public void Disconnect() {
            _channel.Disconnect();
        }

        private void ChannelOnInitialized(IChannel channel) {
            ChannelInitialized?.Invoke(this);
        }

        private void ChannelOnDisconnected(IChannel channel, ShutdownReason reason) {
            ChangeTimerState(false);
            _sentBatch = false;
            FailedQueuedLetters();
            ChannelDisconnected?.Invoke(this, reason);
        }

        private void SlidingTimeoutTimerOnElapsed(object sender) {
            ChangeTimerState(false);
            TrySendBatch(true);
        }

        private void ChangeTimerState(bool enabled) {
            lock(_slidingTimeoutTimer) {
                _slidingTimeoutTimer.Change(enabled ? _options.Extend : TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            }
        }

        private EnqueueResult TrySendBatch(bool timeout) {
            lock(_syncRoot) {
                if(_sentBatch)
                    return EnqueueResult.CantEnqueueMore;

                if(!_batchBuilder.IsEmpty && (timeout || _batchBuilder.IsFull || _stopwatch.ElapsedMilliseconds >= _options.MaxExtend.TotalMilliseconds)) {
                    _sentBatch = true;
                    _channel.Enqueue(_batchBuilder.Build());

                    return EnqueueResult.CantEnqueueMore;
                }

                return EnqueueResult.CanEnqueueMore;
            }
        }

        private void ChannelOnSent(IChannel channel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                _sentBatch = false;

                for(var i = 0; i < letter.Parts.Length; i++)
                    Sent?.Invoke(this, _queue.Dequeue());
            } else
                Sent?.Invoke(this, _queue.Dequeue());

            ChannelQueueEmpty?.Invoke(this);
            TrySendBatch(false);
        }

        private void ChannelOnReceived(ILetter letter, ReceivedEventArgs receivedEventArgs) {
            Received?.Invoke(letter, receivedEventArgs);
        }

        private void ChannelOnFailedToSend(IChannel channel, ILetter letter) {
            _sentBatch = false;

            if(letter.Type == LetterType.Batch)
                FailedQueuedLetters();
            else
                FailedToSend?.Invoke(this, letter);
        }

        private void FailedQueuedLetters() {
            while (_queue.TryDequeue(out ILetter letter))
                FailedToSend?.Invoke(this, letter);

            _batchBuilder.Clear();
        }
    }
}