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

        private readonly ConcurrentQueue<IAbstractChannel> _channelQueue = new ConcurrentQueue<IAbstractChannel>();
        private readonly ConcurrentDictionary<Binding, IAbstractChannel> _channels = new ConcurrentDictionary<Binding, IAbstractChannel>();
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
            listener.IncomingChannel += ListenerOnIncomingChannel;
            listener.Start();
        }

        private void ListenerOnIncomingChannel(IAbstractChannel inboundChannel) {
            DecorateMulticastChannel(inboundChannel);
        }

        private void DecorateMulticastChannel(IAbstractChannel inboundChannel) {
            if (SocketMode == SocketMode.Multicast) {
                var decoratedChannel = new AbstractChannelMulticastDecorator(inboundChannel);
                HookupChannel(decoratedChannel);
            } else {
                HookupChannel(inboundChannel);
            }
        }

        public void Connect(IPAddress ipAddress, int port) {
            var bindingKey = new Binding(ipAddress, port);
            var channel = new OutboundChannel(Id, SocketMode, bindingKey);
            DecorateMulticastChannel(channel);

            channel.Connect();
        }

        private void HookupChannel(IAbstractChannel channel) {
            channel.Received += ChannelReceived;
            channel.FailedToSend += ChannelFailedToSend;
            channel.Sent += ChannelSent;
            channel.ChannelDisconnected += ChannelDisconnected;
            channel.ChannelConnected += ChannelConnected;

            _channels[channel.Binding] = channel;
            channel.Initialize();
        }

        private void ChannelConnected(IAbstractChannel obj) {
            if (Connected != null)
                Connected(obj.Binding);
        }

        private void ChannelDisconnected(IAbstractChannel obj) {
            if(obj is InboundChannel) {
                IAbstractChannel value;
                _channels.TryRemove(obj.Binding, out value);
            }

            if (Disconnected != null)
                Disconnected(obj.Binding);
        }

        private void ChannelReceived(IAbstractChannel channel, ILetter letter) {
            if (Received != null)
                Received(letter);
        }

        private void ChannelSent(IAbstractChannel channel, ILetter letter) {
            if (Sent != null)
                Sent(letter);

            if (SocketMode == SocketMode.Unicast) {
                _channelQueue.Enqueue(channel);
                TrySend();
            }
        }

        private void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
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

        private void Discard(IAbstractChannel abstractChannel, ILetter letter) {
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
                IAbstractChannel channel = GetNextChannel();
                ILetter letter = GetNextLetter();
                channel.Enqueue(letter);
            }
        }

        private bool CanSend() {
            IAbstractChannel channel;
            ILetter letter;
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
