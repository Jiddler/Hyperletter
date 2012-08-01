using System;
using System.Diagnostics;
using System.Timers;
using System.Collections.Concurrent;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core.Batch {
    public class BatchAbstractChannel : IAbstractChannel {
        private readonly IAbstractChannel _channel;
        private readonly BatchOptions _options;
        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();

        private readonly LetterSerializer _letterSerializer;
        private readonly BatchLetterBuilder _batchBuilder;
        private readonly Timer _slidingTimeoutTimer;
        private readonly object _syncRoot = new object();

        private DateTime _firstEnqueueAt;
        private bool _sentBatch;
        private bool _canSend;

        public event Action<IAbstractChannel> ChannelConnected;
        public event Action<IAbstractChannel> ChannelDisconnected;
        public event Action<IAbstractChannel> ChannelQueueEmpty;
        public event Action<IAbstractChannel> ChannelInitialized;

        public event Action<IAbstractChannel, ILetter> Received;
        public event Action<IAbstractChannel, ILetter> Sent;
        public event Action<IAbstractChannel, ILetter> FailedToSend;

        public bool IsConnected { get { return _channel.IsConnected; } }
        public Guid ConnectedTo { get { return _channel.ConnectedTo; } }
        public Binding Binding { get { return _channel.Binding; } }
        public Direction Direction { get { return _channel.Direction; } }

        public BatchAbstractChannel(IAbstractChannel channel, BatchOptions options) {
            _channel = channel;
            _options = options;

            _letterSerializer = new LetterSerializer();
            _batchBuilder = new BatchLetterBuilder(_options.MaxLetters);

            _channel.ChannelConnected += abstractChannel => ChannelConnected(this);
            _channel.ChannelDisconnected += ChannelOnDisconnected;
            _channel.ChannelQueueEmpty += abstractChannel => { /* NOOP */ };
            _channel.ChannelInitialized += ChannelOnInitialized;
            
            _channel.Received += ChannelOnReceived;
            _channel.Sent += ChannelOnSent;
            _channel.FailedToSend += ChannelOnFailedToSend;

            _slidingTimeoutTimer = new Timer(_options.Extend.TotalMilliseconds) { AutoReset = false };
            _slidingTimeoutTimer.Elapsed += SlidingTimeoutTimerOnElapsed;
        }

        private void ChannelOnInitialized(IAbstractChannel abstractChannel) {
            _canSend = true;
            ChannelInitialized(this);
        }

        private void ChannelOnDisconnected(IAbstractChannel channel) {
            _canSend = false;

            FailedQueuedLetters();
            ChannelDisconnected(this);
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

        public void Initialize() {
            _channel.Initialize();
        }

        public EnqueueResult Enqueue(ILetter letter) {
            _slidingTimeoutTimer.Enabled = false;
            _slidingTimeoutTimer.Enabled = true;

            if (_queue.Count == 0)
                _firstEnqueueAt = DateTime.Now;

            _queue.Enqueue(letter);
            _batchBuilder.Add(letter);            
            
            TrySendBatch(false);
            
            return _canSend ? EnqueueResult.CanEnqueueMore : EnqueueResult.CantEnqueueMore;
        }

        private void TrySendBatch(bool timeout) {
            lock (_syncRoot) {
                if (_sentBatch)
                    return;

                if (HasSomethingToSend() && (timeout || _batchBuilder.Count >= _options.MaxLetters || (DateTime.Now - _firstEnqueueAt).TotalMilliseconds >= _options.MaxExtend.TotalMilliseconds)) {
                    _sentBatch = true;
                    _channel.Enqueue(_batchBuilder.Build());
                }
            }
        }

        private bool HasSomethingToSend() {
            return _batchBuilder.Count > 0;
        }

        private void ChannelOnSent(IAbstractChannel abstractChannel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                _sentBatch = false;

                for (int i = 0; i < letter.Parts.Length; i++)
                    Sent(this, _queue.Dequeue());
            } else
                Sent(this, _queue.Dequeue());

            TrySendBatch(false);
        }

        private void ChannelOnReceived(IAbstractChannel abstractChannel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                UnpackBatch(letter, data => Received(this, _letterSerializer.Deserialize(data)));
            } else {
                Received(this, letter);
            }
        }
        
        private void ChannelOnFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            _canSend = false;
            _sentBatch = false;

            if (letter.Type == LetterType.Batch)
                FailedQueuedLetters();
            else
                FailedToSend(this, letter);
        }

        private void UnpackBatch(ILetter letter, Action<byte[]> callback) {
            for (int i = 0; i < letter.Parts.Length; i++)
                callback(letter.Parts[i]);
        }

        public void Dispose() {
            _channel.Dispose();
        }
    }
}
