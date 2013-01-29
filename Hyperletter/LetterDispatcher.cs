using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Channel;
using Hyperletter.Letter;
using Hyperletter.Utility;

namespace Hyperletter {
    internal class LetterDispatcher {
        private readonly HyperSocket _hyperSocket;
        private readonly CancellationToken _cancellationToken;

        private readonly QueueDictionary<IChannel> _queuedChannels;
        private readonly BlockingCollection<IChannel> _channelQueue;

        private readonly BlockingCollection<ILetter> _blockingSendQueue;
        private readonly QueueDictionary<ILetter> _sendQueue;

        public LetterDispatcher(HyperSocket hyperSocket, CancellationToken cancellationToken) {
            _hyperSocket = hyperSocket;
            _cancellationToken = cancellationToken;

            _queuedChannels = new QueueDictionary<IChannel>();
            _channelQueue = new BlockingCollection<IChannel>(_queuedChannels);

            _sendQueue = new QueueDictionary<ILetter>();
            _blockingSendQueue = new BlockingCollection<ILetter>(_sendQueue);

            Task.Factory.StartNew(SendTask);
        }

        public void EnqueueLetter(ILetter letter) {
            _blockingSendQueue.Add(letter);
        }

        public void EnqueueChannel(IChannel channel) {
            _channelQueue.TryAdd(channel);
        }

        public void DequeueChannel(IChannel channel) {
            _queuedChannels.Remove(channel);
        }

        private void SendTask() {
            while(true) {
                try {
                    var letter = GetNextLetter();
                    if(IsMulticastLetter(letter))
                        SendMulticastLetter(letter);
                    else
                        SendUnicastLetter(letter);
                } catch(OperationCanceledException) {
                    break;
                } catch(Exception) {
                }
            }
        }

        private void SendUnicastLetter(ILetter letter) {
            var channel = GetNextChannel();
            var result = channel.Enqueue(letter);

            if(result == EnqueueResult.CanEnqueueMore) {
                _channelQueue.Add(channel);
            }
        }

        private ILetter GetNextLetter() {
            return _blockingSendQueue.Take(_cancellationToken);
        }

        private IChannel GetNextChannel() {
            while(true) {
                var channel = _channelQueue.Take(_cancellationToken);
                if(!channel.CanSend || channel.ShutdownRequested)
                    continue;

                return channel;
            }
        }

        private static bool IsMulticastLetter(ILetter letter) {
            return (letter.Options & LetterOptions.Multicast) == LetterOptions.Multicast;
        }

        private void SendMulticastLetter(ILetter letter) {
            foreach(var channel in _hyperSocket.Channels) {
                channel.Enqueue(letter);
            }
        }
    }
}