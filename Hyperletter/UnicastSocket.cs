using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Hyperletter.Batch;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter {
    public class UnicastSocket : AbstractHyperSocket {
        private readonly ConcurrentDictionary<Binding, IAbstractChannel> _availableChannels = new ConcurrentDictionary<Binding, IAbstractChannel>();
        private readonly ConcurrentQueue<IAbstractChannel> _channelQueue = new ConcurrentQueue<IAbstractChannel>();
        private readonly LinkedList<ILetter> _sendQueue = new LinkedList<ILetter>();

        private readonly object _syncRoot = new object();
        
        public event Action<ILetter> Requeued;

        public UnicastSocket() {
        }

        public UnicastSocket(SocketOptions options) : base(options) {
        }

        protected override void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            if(letter.Options.IsSet(LetterOptions.Requeue)) {
                _sendQueue.AddFirst(letter);
                TrySend();
                if(Requeued != null)
                    Requeued(letter);
            } else {
                Discard(abstractChannel, letter);
            }
        }

        protected override IAbstractChannel PrepareChannel(IAbstractChannel channel) {
            if(Options.BatchOptions.Enabled)
                channel = new BatchAbstractChannel(this, channel);

            channel.ChannelQueueEmpty += ChannelCanSend;
            channel.ChannelInitialized += ChannelCanSend;

            return channel;
        }

        private void ChannelCanSend(IAbstractChannel abstractChannel) {
            if (_availableChannels.TryAdd(abstractChannel.Binding, abstractChannel)) {
                _channelQueue.Enqueue(abstractChannel);
            }

            TrySend();
        }

        public override void Send(ILetter letter) {
            _sendQueue.AddLast(letter);
            TrySend();
        }

        protected void TrySend() {
            lock(_syncRoot) {
                while(CanSend()) {
                    IAbstractChannel channel = GetNextChannel();
                    _availableChannels.TryRemove(channel.Binding, out channel);

                    if(!channel.IsConnected)
                        continue;

                    ILetter letter = GetNextLetter();
                    EnqueueResult result = channel.Enqueue(letter);
                    if (result == EnqueueResult.CanEnqueueMore) {
                        _channelQueue.Enqueue(channel);
                        _availableChannels.Add(channel.Binding, channel);
                    }
                }
            }
        }

        private bool CanSend() {
            IAbstractChannel channel;
            return _channelQueue.TryPeek(out channel) && _sendQueue.Count> 0;
        }

        private IAbstractChannel GetNextChannel() {
            IAbstractChannel channel;
            _channelQueue.TryDequeue(out channel);
            return channel;
        }

        private ILetter GetNextLetter() {
            ILetter letter = _sendQueue.First.Value;
            _sendQueue.RemoveFirst();
            return letter;
        }
    }
}