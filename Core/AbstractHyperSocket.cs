using System;
using System.Collections.Concurrent;
using System.Net;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public abstract class AbstractHyperSocket {
        public event Action<ILetter> Sent;
        public event Action<ILetter> Received;
        public event Action<Binding, ILetter> Discarded;

        public event Action<Binding> Connected;
        public event Action<Binding> Disconnected;

        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();

        protected readonly ConcurrentDictionary<Binding, IAbstractChannel> Channels = new ConcurrentDictionary<Binding, IAbstractChannel>();

        public Guid Id { get; private set; }

        protected AbstractHyperSocket() : this(Guid.NewGuid()) {
        }

        protected AbstractHyperSocket(Guid id) {
            Id = id;
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
            var channel = new OutboundChannel(Id, bindingKey);
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

            Channels[preparedChannel.Binding] = preparedChannel;
            preparedChannel.Initialize();
        }

        protected virtual IAbstractChannel PrepareChannel(IAbstractChannel channel) {
            return channel;
        }

        private void ChannelConnected(IAbstractChannel obj) {
            if (Connected != null)
                Connected(obj.Binding);
        }

        private void ChannelDisconnected(IAbstractChannel obj) {
            if(obj is InboundChannel) {
                IAbstractChannel value;
                Channels.TryRemove(obj.Binding, out value);
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

            AfterSent(channel);
        }

        protected virtual void AfterSent(IAbstractChannel channel) {}
        protected abstract void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter);

        protected void Discard(IAbstractChannel abstractChannel, ILetter letter) {
            if (Discarded != null && letter.Options.IsSet(LetterOptions.SilentDiscard))
                Discarded(abstractChannel.Binding, letter);
        }

        public abstract void Send(ILetter letter);
    }
}
