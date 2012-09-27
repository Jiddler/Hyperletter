using System;
using System.Collections.Concurrent;
using System.Net;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter {
    public abstract class AbstractHyperSocket : IDisposable, IHyperSocket {
        protected readonly ConcurrentDictionary<Binding, IChannel> Channels = new ConcurrentDictionary<Binding, IChannel>();
        protected readonly ConcurrentDictionary<Guid, IChannel> RouteChannels = new ConcurrentDictionary<Guid, IChannel>();
        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();
        
        internal LetterSerializer LetterSerializer { get; private set; }
        public SocketOptions Options { get; set; }

        public event Action<IHyperSocket, ILetter> Sent;
        public event Action<IHyperSocket, ILetter> Received;
        public event Action<IHyperSocket, Binding, ILetter> Discarded;

        public event Action<IHyperSocket, Binding> Connected;
        public event Action<IHyperSocket, Binding> Disconnected;

        protected AbstractHyperSocket() : this(new SocketOptions()) {
        }

        protected AbstractHyperSocket(SocketOptions options) {
            Options = options;
            LetterSerializer = new LetterSerializer(options.Id);
        }

        public void Dispose() {
            foreach(SocketListener listener in _listeners.Values)
                listener.Dispose();

            foreach(IChannel channel in Channels.Values)
                channel.Dispose();
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
            var channel = new OutboundChannel(this, bindingKey);
            HookupChannel(channel);
            channel.Connect();
        }

        public void Answer(ILetter answer, ILetter answeringTo) {
            Guid address = answeringTo.Address[0];

            IChannel channel;
            if(RouteChannels.TryGetValue(address, out channel)) {
                channel.Enqueue(answer);
            }
        }

        public abstract void Send(ILetter letter);

        private void HookupChannel(IChannel channel) {
            IChannel preparedChannel = PrepareChannel(channel);

            preparedChannel.Received += ChannelReceived;
            preparedChannel.FailedToSend += ChannelFailedToSend;
            preparedChannel.Sent += ChannelSent;
            preparedChannel.ChannelDisconnected += ChannelDisconnected;
            preparedChannel.ChannelConnected += ChannelConnected;
            preparedChannel.ChannelInitialized += ChannelInitialized;

            Channels[preparedChannel.Binding] = preparedChannel;
            preparedChannel.Initialize();
        }

        private void ChannelInitialized(IChannel obj) {
            RouteChannels.TryAdd(obj.ConnectedTo, obj);
        }

        protected virtual IChannel PrepareChannel(IChannel channel) {
            return channel;
        }

        private void ChannelConnected(IChannel obj) {
            if(Connected != null)
                Connected(this, obj.Binding);
        }

        private void ChannelDisconnected(IChannel channel) {
            channel.Dispose();

            var binding = channel.Binding;
            IChannel value;
            Channels.TryRemove(binding, out value);
            RouteChannels.TryRemove(channel.ConnectedTo, out value);

            if(Disconnected != null)
                Disconnected(this, binding);

            if(channel.Direction == Direction.Outbound)
                Connect(binding.IpAddress, binding.Port);
        }

        private void ChannelReceived(IChannel channel, ILetter letter) {
            if(Received != null)
                Received(this, letter);
        }

        private void ChannelSent(IChannel channel, ILetter letter) {
            if(Sent != null)
                Sent(this, letter);
        }

        protected void Discard(IChannel channel, ILetter letter) {
            if(Discarded != null && !letter.Options.IsSet(LetterOptions.SilentDiscard))
                Discarded(this, channel.Binding, letter);
        }

        protected void SendRoutedLetter(ILetter letter) {
        }

        protected abstract void ChannelFailedToSend(IChannel channel, ILetter letter);
    }
}