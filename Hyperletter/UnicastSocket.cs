using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Hyperletter.Batch;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter {
    public class UnicastSocket : AbstractHyperSocket {
        private readonly ConcurrentDictionary<Binding, IChannel> _availableChannels = new ConcurrentDictionary<Binding, IChannel>();
        private readonly ConcurrentQueue<IChannel> _channelQueue = new ConcurrentQueue<IChannel>();
        private readonly LinkedList<ILetter> _sendQueue = new LinkedList<ILetter>();

        private readonly object _syncRoot = new object();
        
        public event Action<ILetter> Requeued;

        public UnicastSocket() {
        }

        public UnicastSocket(SocketOptions options) : base(options) {
        }

        protected override void ChannelFailedToSend(IChannel channel, ILetter letter) {
            if(letter.Options.IsSet(LetterOptions.Requeue)) {
                _sendQueue.AddFirst(letter);
                TrySend();
                if(Requeued != null)
                    Requeued(letter);
            } else {
                Discard(channel, letter);
            }
        }

        protected override IChannel PrepareChannel(IChannel channel) {
            if(Options.BatchOptions.Enabled)
                channel = new BatchChannel(this, channel);

            channel.ChannelQueueEmpty += ChannelCanSend;
            channel.ChannelInitialized += ChannelCanSend;

            return channel;
        }

        private void ChannelCanSend(IChannel channel) {
            if (_availableChannels.TryAdd(channel.Binding, channel)) {
                _channelQueue.Enqueue(channel);
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
                    IChannel channel = GetNextChannel();
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
            IChannel channel;
            return _channelQueue.TryPeek(out channel) && _sendQueue.Count> 0;
        }

        private IChannel GetNextChannel() {
            IChannel channel;
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