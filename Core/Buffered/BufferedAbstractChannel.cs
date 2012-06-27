using System;
using System.Collections.Concurrent;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core.Buffered {
    public class BufferedAbstractChannel : IAbstractChannel {
        private readonly IAbstractChannel _channel;
        private ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();

        private readonly LetterSerializer _letterSerializer;
        private readonly BatchLetterBuilder _batchBuilder;

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

        public BufferedAbstractChannel(IAbstractChannel channel) {
            _channel = channel;
            _letterSerializer = new LetterSerializer();
            _batchBuilder = new BatchLetterBuilder();

            _channel.ChannelConnected += abstractChannel => ChannelConnected(this);
            _channel.ChannelDisconnected += ChannelOnChannelDisconnected;
            _channel.ChannelQueueEmpty += abstractChannel => { if (ChannelQueueEmpty != null) ChannelQueueEmpty(this); };
            _channel.ChannelInitialized += abstractChannel => ChannelInitialized(this);
            
            _channel.Received += ChannelOnReceived;
            _channel.Sent += ChannelOnSent;
            _channel.FailedToSend += ChannelOnFailedToSend;
        }

        private void ChannelOnChannelDisconnected(IAbstractChannel abstractChannel) {
            //ILetter failedLetter;
            //while(_queue.TryDequeue(out failedLetter)) {
            //    FailedToSend(this, failedLetter);
            //}
        }

        public void Initialize() {
            _channel.Initialize();
        }

        public bool Enqueue(ILetter letter) {
            _queue.Enqueue(letter);
            _batchBuilder.Add(letter);

            lock (this) {
                if (_batchBuilder.Count >= 4000) {
                    _channel.Enqueue(_batchBuilder.Build());
                    return false;
                }

                return true;
            }
        }

        private void ChannelOnSent(IAbstractChannel abstractChannel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                for (int i = 0; i < letter.Parts.Length; i++) {
                    Sent(this, _queue.Dequeue());
                }
            } else
                Sent(this, _queue.Dequeue());
        }

        private void ChannelOnReceived(IAbstractChannel abstractChannel, ILetter letter) {
            if(letter.Type == LetterType.Batch) {
                UnpackBatch(letter, data => Received(this, _letterSerializer.Deserialize(data)));
            } else {
                Received(this, letter);
            }
        }
        
        private void ChannelOnFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            if (letter.Type == LetterType.Batch)
                UnpackBatch(letter, bytes => FailedToSend(this, _queue.Dequeue()));
            else
                FailedToSend(this, letter);
        }

        private void UnpackBatch(ILetter letter, Action<byte[]> callback) {
            for (int i = 0; i < letter.Parts.Length; i++) {
                var part = letter.Parts[i];
                if (part.PartType != PartType.Letter)
                    continue;

                callback(part.Data);
            }
        }

        public void Dispose() {
            _channel.Dispose();
        }
    }
}
