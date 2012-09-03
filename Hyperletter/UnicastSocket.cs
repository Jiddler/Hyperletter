using System;
using System.Collections.Concurrent;
using Hyperletter.Batch;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter {
    public class UnicastSocket : AbstractHyperSocket {
        private readonly ConcurrentQueue<IAbstractChannel> _channelQueue = new ConcurrentQueue<IAbstractChannel>();
        private readonly ConcurrentQueue<ILetter> _sendQueue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _prioritySendQueue = new ConcurrentQueue<ILetter>();

        private readonly object _syncRoot = new object();

        public event Action<ILetter> Requeued;

        public UnicastSocket() {}

        public UnicastSocket(SocketOptions options) : base(options) {
        }

        protected override void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            if (letter.Options.IsSet(LetterOptions.Requeue)) {
                _prioritySendQueue.Enqueue(letter);
                TrySend();
                if (Requeued != null)
                    Requeued(letter);
            } else {
                Discard(abstractChannel, letter);
            }
        }

        protected override IAbstractChannel PrepareChannel(IAbstractChannel channel) {
            if(Options.BatchOptions.Enabled)
                channel = new BatchAbstractChannel(channel, Options.BatchOptions);

            channel.ChannelQueueEmpty += ChannelCanSend;
            channel.ChannelInitialized += ChannelCanSend;

            return channel;
        }

        private void ChannelCanSend(IAbstractChannel abstractChannel) {
            _channelQueue.Enqueue(abstractChannel);
            TrySend();
        }

        public override void Send(ILetter letter) {
            _sendQueue.Enqueue(letter);
            TrySend();
        }
       
        protected void TrySend() {
            lock (_syncRoot) {
                while (CanSend()) {
                    IAbstractChannel channel = GetNextChannel();
                    ILetter letter = GetNextLetter();
                    var result = channel.Enqueue(letter);
                    if (result == EnqueueResult.CanEnqueueMore)
                        _channelQueue.Enqueue(channel);
                }
            }
        }

        private bool CanSend() {
            ILetter letter;
            IAbstractChannel channel;
            
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