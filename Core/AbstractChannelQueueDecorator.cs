using System;
using System.Collections.Concurrent;
using System.Threading;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class AbstractChannelQueueDecorator : IAbstractChannel {
        private readonly IAbstractChannel _abstractChannel;
        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);

        private bool _letterEnqueuedButNotSent;

        public event Action<IAbstractChannel> ChannelConnected;
        public event Action<IAbstractChannel> ChannelDisconnected;
        public event Action<IAbstractChannel, ILetter> Received;
        public event Action<IAbstractChannel, ILetter> Sent;
        public event Action<IAbstractChannel, ILetter> FailedToSend;

        public bool IsConnected { get { return _abstractChannel.IsConnected; } }
        public Binding Binding { get { return _abstractChannel.Binding; } }

        public AbstractChannelQueueDecorator(IAbstractChannel abstractChannel) {
            _abstractChannel = abstractChannel;
            _abstractChannel.ChannelConnected += channel => ChannelConnected(channel);
            _abstractChannel.ChannelDisconnected += AbstractChannelOnChannelDisconnected;
            _abstractChannel.FailedToSend += (channel, letter) => FailedToSend(channel, letter);
            _abstractChannel.Received += (channel, letter) => Received(channel, letter);
            _abstractChannel.Sent += AbstractChannelOnSent;

            _letterEnqueuedButNotSent = false;
        }

        private void AbstractChannelOnChannelDisconnected(IAbstractChannel abstractChannel) {
            _cleanUpLock.Reset();
            
            FailedQueuedLetters();
            ChannelDisconnected(this);
            
            _cleanUpLock.Set();
        }

        private void FailedQueuedLetters() {
            ILetter failedLetter;
            while(_queue.TryDequeue(out failedLetter)) {
                FailedToSend(this, failedLetter);
            }
        }

        private void AbstractChannelOnSent(IAbstractChannel abstractChannel, ILetter letter) {
            Sent(this, letter);
            TryEnqueueNewLetter();
        }

        private void TryEnqueueNewLetter() {
            ILetter nextLetter;
            if (_queue.TryDequeue(out nextLetter)) {
                _letterEnqueuedButNotSent = true;
                _abstractChannel.Enqueue(nextLetter);
            } else {
                _letterEnqueuedButNotSent = false;
            }
        }

        public void Initialize() {
            _abstractChannel.Initialize();
        }

        public void Enqueue(ILetter letter) {
            _cleanUpLock.Wait();

            if (_letterEnqueuedButNotSent) {
                _queue.Enqueue(letter);
            } else {
                _letterEnqueuedButNotSent = true;
                _abstractChannel.Enqueue(letter);
            }
        }
    }
}
