using System;
using System.Collections.Concurrent;
using System.Net;
using Hyperletter.Channel;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter {
    public abstract class AbstractHyperSocket : IDisposable, IHyperSocket {
        public event Action<IHyperSocket, ILetter> Sent;
        public event Action<IHyperSocket, ILetter> Received;
        public event Action<IHyperSocket, Binding, ILetter> Discarded;

        public event Action<IHyperSocket, Binding> Connected;
        public event Action<IHyperSocket, Binding> Disconnected;

        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();

        protected readonly ConcurrentDictionary<Binding, IAbstractChannel> Channels = new ConcurrentDictionary<Binding, IAbstractChannel>();
        protected readonly ConcurrentDictionary<Guid, IAbstractChannel> RouteChannels = new ConcurrentDictionary<Guid, IAbstractChannel>();

        internal LetterSerializer LetterSerializer { get; private set; }
        public SocketOptions Options { get; set; }

        protected AbstractHyperSocket() : this(new SocketOptions()) {
        }

        protected AbstractHyperSocket(SocketOptions options) {
            Options = options;
            LetterSerializer = new LetterSerializer(options.Id);
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

        private void HookupChannel(IAbstractChannel channel) {
            var preparedChannel = PrepareChannel(channel);

            preparedChannel.Received += ChannelReceived;
            preparedChannel.FailedToSend += ChannelFailedToSend;
            preparedChannel.Sent += ChannelSent;
            preparedChannel.ChannelDisconnected += ChannelDisconnected;
            preparedChannel.ChannelConnected += ChannelConnected;
            preparedChannel.ChannelInitialized += ChannelInitialized;

            Channels[preparedChannel.Binding] = preparedChannel;
            preparedChannel.Initialize();
        }

        private void ChannelInitialized(IAbstractChannel obj) {
            RouteChannels.TryAdd(obj.ConnectedTo, obj);
        }

        protected virtual IAbstractChannel PrepareChannel(IAbstractChannel channel) {
            return channel;
        }

        private void ChannelConnected(IAbstractChannel obj) {
            if (Connected != null)
                Connected(this, obj.Binding);
        }

        private void ChannelDisconnected(IAbstractChannel obj) {
            IAbstractChannel value;
            Channels.TryRemove(obj.Binding, out value);
            RouteChannels.TryRemove(obj.ConnectedTo, out value);

            if (Disconnected != null)
                Disconnected(this, obj.Binding);

            if (obj.Direction == Direction.Outbound)
                Connect(obj.Binding.IpAddress, obj.Binding.Port);

            obj.Dispose();
        }

        private void ChannelReceived(IAbstractChannel channel, ILetter letter) {
            if (Received != null)
                Received(this, letter);
        }

        private void ChannelSent(IAbstractChannel channel, ILetter letter) {
            if (Sent != null)
                Sent(this, letter);
        }
        
        protected void Discard(IAbstractChannel abstractChannel, ILetter letter) {
            if (Discarded != null && !letter.Options.IsSet(LetterOptions.SilentDiscard))
                Discarded(this, abstractChannel.Binding, letter);
        }

        public void Answer(ILetter answer, ILetter answeringTo) {
            var address = answeringTo.Address[0];

            IAbstractChannel channel;
            if(RouteChannels.TryGetValue(address, out channel)) {
                channel.Enqueue(answer);
            }
        }

        protected void SendRoutedLetter(ILetter letter) {
            
            
        }

        public abstract void Send(ILetter letter);
        protected abstract void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter);
        
        public void Dispose() {
            foreach (var listener in _listeners.Values)
                listener.Dispose();

            foreach(var channel in Channels.Values)
                channel.Dispose();
        }
    }
}
