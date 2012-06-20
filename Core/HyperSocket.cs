using System;
using System.Collections.Concurrent;
using System.Net;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public class HyperSocket : IHyperSocket {
        public event Action<ILetter> Sent;
        public event Action<ILetter> Received;
        public event Action<ILetter> Requeued;
        public event Action<Binding, ILetter> Discarded;

        public event Action<Binding> Connected;
        public event Action<Binding> Disconnected;

        private readonly ConcurrentQueue<ILetter> _sendQueue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _prioritySendQueue = new ConcurrentQueue<ILetter>();

        private readonly ConcurrentQueue<AbstractChannel> _channelQueue = new ConcurrentQueue<AbstractChannel>();
        private readonly ConcurrentDictionary<Binding, AbstractChannel> _channels = new ConcurrentDictionary<Binding, AbstractChannel>();
        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();

        private readonly object _syncRoot = new object();

        public Guid Id { get; private set; }
        public SocketMode SocketMode { get; set; }

        public HyperSocket() : this(Guid.NewGuid(),  SocketMode.Unicast) {
        }

        public HyperSocket(SocketMode socketMode) : this(Guid.NewGuid(), socketMode) {
        }

        public HyperSocket(Guid id) : this(id, SocketMode.Unicast) {
        }

        public HyperSocket(Guid id, SocketMode socketMode) {
            Id = id;
            SocketMode = socketMode;
        }

        public void Bind(IPAddress ipAddress, int port) {
            var binding = new Binding(ipAddress, port);
            
            var listener = new SocketListener(this, binding);
            _listeners[binding] = listener;
            listener.IncomingChannel += HookupChannel;
   
            listener.Start();
        }

        public void Connect(IPAddress ipAddress, int port) {
            var bindingKey = new Binding(ipAddress, port);
            var channel = new OutboundChannel(Id, SocketMode, bindingKey);
            HookupChannel(channel);

            channel.Connect();
        }

        private void HookupChannel(AbstractChannel channel) {
            channel.Received += ChannelReceived;
            channel.FailedToSend += ChannelFailedToSend;
            channel.Sent += ChannelSent;
            channel.ChannelDisconnected += ChannelDisconnected;
            channel.ChannelConnected += ChannelConnected;

            _channels[channel.Binding] = channel;
            channel.Initialize();
        }

        private void ChannelConnected(AbstractChannel obj) {
            if (Connected != null)
                Connected(obj.Binding);
        }

        private void ChannelDisconnected(AbstractChannel obj) {
            if(obj is InboundChannel) {
                AbstractChannel value;
                _channels.TryRemove(obj.Binding, out value);
            }

            if (Disconnected != null)
                Disconnected(obj.Binding);
        }

        private void ChannelReceived(AbstractChannel channel, ILetter letter) {
            if (Received != null)
                Received(letter);
        }

        private void ChannelSent(AbstractChannel channel, ILetter letter) {
            if (Sent != null)
                Sent(letter);

            if (SocketMode == SocketMode.Unicast) {
                _channelQueue.Enqueue(channel);
                TrySend();
            }
        }

        private void ChannelFailedToSend(AbstractChannel abstractChannel, ILetter letter) {
            if (SocketMode == SocketMode.Unicast) {
                if (letter.Options.IsSet(LetterOptions.NoRequeue)) {
                    Discard(abstractChannel, letter);
                } else {
                    _prioritySendQueue.Enqueue(letter);
                    if (Requeued != null)
                        Requeued(letter);

                    TrySend();
                }
            } else if (SocketMode == SocketMode.Multicast) {
                Discard(abstractChannel, letter);
            }
        }

        private void Discard(AbstractChannel abstractChannel, ILetter letter) {
            if (Discarded != null && letter.Options.IsSet(LetterOptions.SilentDiscard))
                Discarded(abstractChannel.Binding, letter);
        }

        public void Send(ILetter letter) {
            _sendQueue.Enqueue(letter);
            TrySend();
        }

        private void TrySend() {
            lock (_syncRoot) {
                if (SocketMode == SocketMode.Unicast)
                    TrySendUnicast();
                else if (SocketMode == SocketMode.Multicast)
                    TrySendMulticast();
            }
        }

        private void TrySendMulticast() {
            ILetter letter;
            while((letter = GetNextLetter()) != null) {
                foreach (var channel in _channels.Values) {
                    if(channel.IsConnected)
                        channel.Enqueue(letter);
                    else
                        ChannelFailedToSend(channel, letter);
                }
            }
        }

        private void TrySendUnicast() {
            while (CanSend()) {
                AbstractChannel channel = GetNextChannel();
                ILetter letter = GetNextLetter();
                channel.Enqueue(letter);
            }
        }

        private bool CanSend() {
            AbstractChannel channel;
            ILetter letter;
            return _channelQueue.TryPeek(out channel) && (_prioritySendQueue.TryPeek(out letter) || _sendQueue.TryPeek(out letter));
        }

        private AbstractChannel GetNextChannel() {
            AbstractChannel channel;
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
