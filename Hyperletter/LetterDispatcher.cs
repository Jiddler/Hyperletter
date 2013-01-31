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

        private readonly QueueDictionary<IChannel> _channelQueue = new QueueDictionary<IChannel>();
        private readonly QueueDictionary<ILetter> _letterQueue = new QueueDictionary<ILetter>();

        public LetterDispatcher(HyperSocket hyperSocket, CancellationToken cancellationToken) {
            _hyperSocket = hyperSocket;
            _cancellationToken = cancellationToken;

            Task.Factory.StartNew(SendTask);
        }

        public void EnqueueLetter(ILetter letter) {
            _letterQueue.TryAdd(letter);
        }

        public void EnqueueChannel(IChannel channel) {
            _channelQueue.TryAdd(channel);
        }

        public void DequeueChannel(IChannel channel) {
            _channelQueue.Remove(channel);
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
                _channelQueue.TryAdd(channel);
            }
        }

        private ILetter GetNextLetter() {
            return _letterQueue.Take(_cancellationToken);
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