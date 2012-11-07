using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Batch {
    internal class BatchChannel : IChannel {
        private readonly BatchLetterBuilder _batchBuilder;
        private readonly IChannel _channel;
        private readonly BatchOptions _options;
        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();

        private readonly Timer _slidingTimeoutTimer;
        private readonly object _syncRoot = new object();
        private bool _canSend;

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private bool _sentBatch;

        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel, ShutdownReason> ChannelDisconnected;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<IChannel, ILetter> Received;
        public event Action<IChannel, ILetter> Sent;
        public event Action<IChannel, ILetter> FailedToSend;

        public BatchChannel(SocketOptions options, IChannel channel, BatchLetterBuilder batchBuilder) {
            _channel = channel;
            _options = options.Batch;
            _batchBuilder = batchBuilder;

            _channel.ChannelConnected += abstractChannel => ChannelConnected(this);
            _channel.ChannelDisconnected += ChannelOnDisconnected;
            _channel.ChannelQueueEmpty += abstractChannel => { /* NOOP */ };
            _channel.ChannelInitialized += ChannelOnInitialized;

            _channel.Received += ChannelOnReceived;
            _channel.Sent += ChannelOnSent;
            _channel.FailedToSend += ChannelOnFailedToSend;

            _slidingTimeoutTimer = new Timer(_options.Extend.TotalMilliseconds) {AutoReset = false};
            _slidingTimeoutTimer.Elapsed += SlidingTimeoutTimerOnElapsed;
        }

        public bool IsConnected {
            get { return _channel.IsConnected; }
        }

        public Guid RemoteNodeId {
            get { return _channel.RemoteNodeId; }
        }

        public Binding Binding {
            get { return _channel.Binding; }
        }

        public Direction Direction {
            get { return _channel.Direction; }
        }

        public void Initialize() {
            _channel.Initialize();
        }

        public EnqueueResult Enqueue(ILetter letter) {
            _slidingTimeoutTimer.Enabled = false;
            _slidingTimeoutTimer.Enabled = true;

            if(!_sentBatch)
                _stopwatch.Restart();

            _queue.Enqueue(letter);
            _batchBuilder.Add(letter);

            TrySendBatch(false);

            return _canSend ? EnqueueResult.CanEnqueueMore : EnqueueResult.CantEnqueueMore;
        }

        public void Heartbeat() {
            _channel.Heartbeat();
        }

        public void Disconnect() {
            _channel.Disconnect();
        }
        
        private void ChannelOnInitialized(IChannel channel) {
            _canSend = true;
            ChannelInitialized(this);
        }

        private void ChannelOnDisconnected(IChannel channel, ShutdownReason reason) {
            _canSend = false;

            FailedQueuedLetters();
            ChannelDisconnected(this, reason);
        }

        private void FailedQueuedLetters() {
            ILetter letter;
            while(_queue.TryDequeue(out letter)) {
                FailedToSend(this, letter);
            }
        }

        private void SlidingTimeoutTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs) {
            _slidingTimeoutTimer.Enabled = false;
            TrySendBatch(true);
        }

        private void TrySendBatch(bool timeout) {
            lock(_syncRoot) {
                if(_sentBatch)
                    return;

                if(HasSomethingToSend() && (timeout || _batchBuilder.Count >= _options.MaxLetters || _stopwatch.ElapsedMilliseconds >= _options.MaxExtend.TotalMilliseconds)) {
                    _sentBatch = true;
                    _channel.Enqueue(_batchBuilder.Build());
                }
            }
        }

        private bool HasSomethingToSend() {
            return _batchBuilder.Count > 0;
        }

        private void ChannelOnSent(IChannel channel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                _sentBatch = false;

                for(int i = 0; i < letter.Parts.Length; i++)
                    Sent(this, _queue.Dequeue());
            } else
                Sent(this, _queue.Dequeue());

            TrySendBatch(false);
        }

        private void ChannelOnReceived(IChannel channel, ILetter letter) {
            Received(this, letter);
        }

        private void ChannelOnFailedToSend(IChannel channel, ILetter letter) {
            _canSend = false;
            _sentBatch = false;

            if(letter.Type == LetterType.Batch)
                FailedQueuedLetters();
            else
                FailedToSend(this, letter);
        }
    }
}