using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace Hyperletter {
    public class HyperSocket {
        public event Action<ILetter> Sent;
        public event Action<ILetter> Received;
        public event Action<ILetter> Requeued;
        public event Action<Binding, ILetter> Discarded;

        private readonly ConcurrentQueue<ILetter> _sendQueue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _prioritySendQueue = new ConcurrentQueue<ILetter>();

        private readonly ConcurrentQueue<AbstractChannel> _channelQueue = new ConcurrentQueue<AbstractChannel>();
        private readonly ConcurrentDictionary<Binding, AbstractChannel> _channels = new ConcurrentDictionary<Binding, AbstractChannel>();
        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();
        private readonly Timer _timer;

        protected object SyncRoot = new object();

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

            _timer = new Timer(HealthCheckChannels, null, 1000, Timeout.Infinite);
        }

        private void HealthCheckChannels(object state) {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            foreach(var channel in _channels.Values)
                channel.HealthCheck();

            _timer.Change(1000, Timeout.Infinite);
        }

        public void Bind(IPAddress ipAddress, int port) {
            var binding = new Binding(ipAddress, port);
            
            var listener = new SocketListener(this, binding);
            _listeners[binding] = listener;
            listener.Connection += (incomingBinding, channel) => {
                channel.Disconnect += ClientOnDisconnect;
                HookupChannel(incomingBinding, channel);
            };
   
            listener.Start();
        }

        private void ChannelCanSend(AbstractChannel abstractChannel) {
            _channelQueue.Enqueue(abstractChannel);
            TrySend();
        }

        private void ClientOnDisconnect(AbstractChannel abstractChannel, Binding binding) {
            AbstractChannel value;
            _channels.TryRemove(binding, out value);
        }

        public void Connect(IPAddress ipAddress, int port) {
            var bindingKey = new Binding(ipAddress, port);
            var channel = new OutboundChannel(this, bindingKey);
            HookupChannel(bindingKey, channel);

            channel.Connect();
        }

        private void HookupChannel(Binding binding, AbstractChannel channel) {
            channel.CanSend += ChannelCanSend;
            channel.Received += ChannelReceived;
            channel.FailedToSend += ChannelFailedToSend;
            channel.Sent += ChannelSent;

            _channels[binding] = channel;
            channel.Initialize();
        }

        private void ChannelReceived(AbstractChannel channel, ILetter letter) {
            if (Received != null)
                Received(letter);
        }

        private void ChannelSent(AbstractChannel channel, ILetter letter) {
            if (Sent != null)
                Sent(letter);
        }

        private void ChannelFailedToSend(AbstractChannel abstractChannel, ILetter letter) {
            if (SocketMode == SocketMode.Unicast) {
                _prioritySendQueue.Enqueue(letter);
                if (Requeued != null)
                    Requeued(letter);

                TrySend();
            } else if (SocketMode == SocketMode.Multicast) {
                if (Discarded != null)
                    Discarded(abstractChannel.Binding, letter);
            }
        }

        public void Send(ILetter letter) {
            _sendQueue.Enqueue(letter);
            TrySend();
        }

        private void TrySend() {
            lock (SyncRoot) {
                if (SocketMode == SocketMode.Unicast)
                    TrySendUnicast();
                else if (SocketMode == SocketMode.Multicast)
                    TrySendMulticast();
            }
        }

        private void TrySendMulticast() {
            ILetter letter;
            while((letter = GetNextLetter()) != null) {
                foreach (var channel in _channels) {
                    if(channel.Value.IsConnected)
                        channel.Value.Enqueue(letter);
                    else
                        ChannelFailedToSend(channel.Value, letter);
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
