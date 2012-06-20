using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public class UnicastSocket : AbstractHyperSocket {
        public event Action<ILetter> Requeued;

        private readonly ConcurrentQueue<IAbstractChannel> _channelQueue = new ConcurrentQueue<IAbstractChannel>();
        private readonly ConcurrentQueue<ILetter> _sendQueue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _prioritySendQueue = new ConcurrentQueue<ILetter>();

        private readonly Task _sendTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly object _syncRoot = new object();
        public UnicastSocket() {
            //_sendTask = new Task(Send);
            //_sendTask.Start();
        }

        protected override void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            if (letter.Options.IsSet(LetterOptions.NoRequeue)) {
                Discard(abstractChannel, letter);
            } else {
                _sendQueue.Enqueue(letter);
                if (Requeued != null)
                    Requeued(letter);
            }
        }

        public override void Send(ILetter letter) {
            _sendQueue.Enqueue(letter);
            TrySend();
        }

        protected override void AfterSent(IAbstractChannel channel) {
            _channelQueue.Enqueue(channel);
            TrySend();
        }
        /*
        private void Send() {
            try {
                while (true) {
                    _channelQueue.WaitUntilQueueNotEmpty(_cancellationTokenSource);
                    _sendQueue.WaitUntilQueueNotEmpty(_cancellationTokenSource);

                    var channel = _channelQueue.Dequeue();
                    var letter = _sendQueue.Dequeue();

                    channel.Enqueue(letter);

                    _channelQueue.Done();
                    _sendQueue.Done();
                }
            } catch(OperationCanceledException) {
            }
        }
         * */
        
        protected void TrySend() {
            lock (_syncRoot) {
                while (CanSend()) {
                    IAbstractChannel channel = GetNextChannel();
                    ILetter letter = GetNextLetter();
                    channel.Enqueue(letter);
                }
            }
        }

        private bool CanSend() {
            IAbstractChannel channel;
            ILetter letter;
            return _channelQueue.TryPeek(out channel) && (_prioritySendQueue.TryPeek(out letter) || _sendQueue.TryPeek(out letter));
        }

        private IAbstractChannel GetNextChannel() {
            IAbstractChannel channel;
            _channelQueue.TryDequeue(out channel);
            return channel;
        }

        private ILetter GetNextLetter() {
            ILetter letter;
            if (!_prioritySendQueue.TryDequeue(out letter))
                _sendQueue.TryDequeue(out letter);
            return letter;
        }
    }
}