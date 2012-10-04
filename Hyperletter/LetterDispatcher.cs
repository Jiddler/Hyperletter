using System.Collections.Concurrent;
using System.Threading.Tasks;
using Hyperletter.Channel;
using Hyperletter.Letter;
using Hyperletter.Extension;

namespace Hyperletter {
    internal class LetterDispatcher {
        private readonly HyperSocket _hyperSocket;

        private readonly ConcurrentDictionary<Binding, IChannel> _queuedChannels;
        private readonly BlockingCollection<IChannel> _channelQueue;

        private readonly BlockingCollection<ILetter> _blockingSendQueue;
        private readonly ConcurrentQueue<ILetter> _sendQueue;

        public LetterDispatcher(HyperSocket hyperSocket) {
            _hyperSocket = hyperSocket;
            _queuedChannels = new ConcurrentDictionary<Binding, IChannel>();
            _channelQueue = new BlockingCollection<IChannel>();

            _sendQueue = new ConcurrentQueue<ILetter>();
            _blockingSendQueue = new BlockingCollection<ILetter>(_sendQueue);

            Task.Factory.StartNew(SendTask);
        }

        public void EnqueueLetter(ILetter letter) {
            _blockingSendQueue.Add(letter);
        }

        public void EnqueueChannel(IChannel channel) {
            if (_queuedChannels.TryAdd(channel.Binding, channel))
                _channelQueue.Add(channel);
        }

        private void SendTask() {
            while(true) {
                var letter = GetNextLetter();
                if(IsMulticastLetter(letter))
                    SendMulticastLetter(letter);
                else
                    SendUnicastLetter(letter);
            }
        }

        private void SendUnicastLetter(ILetter letter) {
            var channel = GetNextChannel();

            _queuedChannels.Remove(channel.Binding);
            var result = channel.Enqueue(letter);

            if(result == EnqueueResult.CanEnqueueMore) {
                _channelQueue.Add(channel);
                _queuedChannels.Add(channel.Binding, channel);
            }
        }

        private ILetter GetNextLetter() {
            return _blockingSendQueue.Take();
        }

        private IChannel GetNextChannel() {
            while(true) {
                var channel = _channelQueue.Take();
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