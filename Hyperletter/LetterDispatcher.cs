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

        private readonly ConcurrentHashSet<IChannel> _queuedChannels;
        private readonly BlockingCollection<IChannel> _channelQueue;

        private readonly BlockingCollection<ILetter> _blockingSendQueue;
        private readonly ConcurrentQueue<ILetter> _sendQueue;

        public LetterDispatcher(HyperSocket hyperSocket, CancellationToken cancellationToken) {
            _hyperSocket = hyperSocket;
            _cancellationToken = cancellationToken;

            _queuedChannels = new ConcurrentHashSet<IChannel>();
            _channelQueue = new BlockingCollection<IChannel>();

            _sendQueue = new ConcurrentQueue<ILetter>();
            _blockingSendQueue = new BlockingCollection<ILetter>(_sendQueue);

            Task.Factory.StartNew(SendTask);
        }

        public void EnqueueLetter(ILetter letter) {
            _blockingSendQueue.Add(letter);
        }

        public void EnqueueChannel(IChannel channel) {
            if (_queuedChannels.Add(channel))
                _channelQueue.Add(channel);
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
                _queuedChannels.Add(channel);
            }
        }

        private ILetter GetNextLetter() {
            return _blockingSendQueue.Take(_cancellationToken);
        }

        private IChannel GetNextChannel() {
            while(true) {
                var channel = _channelQueue.Take(_cancellationToken);
                _queuedChannels.Remove(channel);

                if(!channel.IsConnected)
                    continue;

                return channel;
            }
        }

        private static bool IsMulticastLetter(ILetter letter) {
            return letter.Options.HasFlag(LetterOptions.Multicast);
        }

        private void SendMulticastLetter(ILetter letter) {
            foreach(var channel in _hyperSocket.Channels) {
                channel.Enqueue(letter);
            }
        }
    }
}