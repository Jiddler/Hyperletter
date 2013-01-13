using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Batch;
using Hyperletter.Channel;
using Hyperletter.EventArgs.Channel;
using Hyperletter.EventArgs.Letter;
using Hyperletter.EventArgs.Socket;
using Hyperletter.Extension;
using Hyperletter.IoC;
using Hyperletter.Letter;

namespace Hyperletter {
    public class HyperSocket : IHyperSocket {
        private readonly ConcurrentDictionary<Binding, SocketListener> _listeners = new ConcurrentDictionary<Binding, SocketListener>();
        private readonly ConcurrentDictionary<Binding, IChannel> _channels = new ConcurrentDictionary<Binding, IChannel>();
        private readonly ConcurrentDictionary<Guid, IChannel> _routeChannels = new ConcurrentDictionary<Guid, IChannel>();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly LetterDispatcher _letterDispatcher;
        private readonly Timer _heartbeat;
        private readonly HyperletterFactory _factory;

        public SocketOptions Options { get; private set; }
        internal IEnumerable<IChannel> Channels { get { return _channels.Values; } }

        public event Action<ILetter, ISentEventArgs> Sent;
        public event Action<ILetter, IReceivedEventArgs> Received;
        public event Action<ILetter, IDiscardedEventArgs> Discarded;
        public event Action<ILetter, IRequeuedEventArgs> Requeued;

        public event Action<IHyperSocket, IConnectingEventArgs> Connecting;
        public event Action<IHyperSocket, IConnectedEventArgs> Connected;
        public event Action<IHyperSocket, IInitializedEventArgs> Initialized;
        public event Action<IHyperSocket, IDisconnectedEventArgs> Disconnected;
        public event Action<IHyperSocket, IDisposedEventArgs> Disposed;

        public HyperSocket() : this(new SocketOptions()) {
        }

        public HyperSocket(SocketOptions options) {
            Options = options;

            _factory = new HyperletterFactory(BuildContainer());
            _letterDispatcher = _factory.CreateLetterDispatcher();

            _heartbeat = new Timer(Heartbeat);
            _heartbeat.Change(Options.Heartbeat.Intervall, Options.Heartbeat.Intervall);
        }

        private Container BuildContainer() {
            var container = new Container();

            container.RegisterInstance(this);
            container.RegisterInstance(Options);
            container.RegisterInstance(_cancellationTokenSource.Token);

            container.Register<LetterSerializer>().AsSingleton();
            container.Register<LetterDeserializer>().AsSingleton();
            container.Register<LetterDispatcher>().AsSingleton();
            container.Register<SocketListener>().AsSingleton();
            container.Register<HyperletterFactory>().AsSingleton();

            container.Register<LetterTransmitter>();
            container.Register<LetterReceiver>();

            container.Register<OutboundChannel>();
            container.Register<InboundChannel>();
            container.Register<BatchLetterBuilder>();
            container.Register<BatchChannel>();

            return container;
        }

        private void Heartbeat(object state) {
            foreach(var channel in Channels)
                channel.Heartbeat();
        }

        public void Bind(IPAddress ipAddress, int port) {
            var binding = new Binding(ipAddress, port);

            var listener = _factory.CreateSocketListener(binding);
            _listeners[binding] = listener;
            listener.IncomingChannel += PrepareChannel;
            listener.Start();
        }

        public void Unbind(IPAddress ipAddress, int port) {
            var binding = new Binding(ipAddress, port);
            SocketListener listener;
            if(_listeners.TryRemove(binding, out listener))
                listener.Stop();
        }

        public void Connect(IPAddress ipAddress, int port) {
            var binding = new Binding(ipAddress, port);
            var channel = _factory.CreateOutboundChannel(binding);
            PrepareChannel(channel);
            channel.Connect();
        }

        public void Disconnect(IPAddress ipAddress, int port) {
            Task.Factory.StartNew(() => {
                var binding = new Binding(ipAddress, port);
                IChannel channel;
                if(_channels.TryGetValue(binding, out channel))
                    channel.Disconnect();
            });
        }

        public void Send(ILetter letter) {
            _letterDispatcher.EnqueueLetter(letter);
        }

        public void SendTo(ILetter letter, Guid toNodeId) {
            IChannel channel;
            if(_routeChannels.TryGetValue(toNodeId, out channel))
                channel.Enqueue(letter);
            else {
                var evnt = Discarded;
                if (evnt != null && !letter.Options.HasFlag(LetterOptions.SilentDiscard))
                    evnt(letter, new DiscardedEventArgs { Binding = channel.Binding, Socket = this, RemoteNodeId = toNodeId });
            }
        }

        public void Dispose() {
            Task.Factory.StartNew(() => {
                _cancellationTokenSource.Cancel();
                _heartbeat.Dispose();

                foreach(var listener in _listeners.Values)
                    listener.Stop();

                foreach(var channel in _channels.Values)
                    channel.Disconnect();

                var evnt = Disposed;
                if (evnt != null) evnt(this, new DisposedEventArgs { Socket = this });
            });
        }

        private void PrepareChannel(IChannel channel) {
            if(Options.Batch.Enabled)
                channel = _factory.CreateBatchChannel(channel);

            HookChannel(channel);

            _channels[channel.Binding] = channel;
            channel.Initialize();
        }

        private void HookChannel(IChannel channel) {
            channel.Received += ChannelReceived;
            channel.FailedToSend += ChannelFailedToSend;
            channel.Sent += ChannelSent;
            channel.ChannelDisconnected += ChannelDisconnected;
            channel.ChannelConnecting += ChannelConnecting;
            channel.ChannelConnected += ChannelConnected;
            channel.ChannelInitialized += ChannelInitialized;
            channel.ChannelInitialized += ChannelAvailable;
            channel.ChannelQueueEmpty += ChannelAvailable;
        }

        private void UnhookChannel(IChannel channel) {
            channel.Received -= ChannelReceived;
            channel.FailedToSend -= ChannelFailedToSend;
            channel.Sent -= ChannelSent;
            channel.ChannelDisconnected -= ChannelDisconnected;
            channel.ChannelConnecting -= ChannelConnecting;
            channel.ChannelConnected -= ChannelConnected;
            channel.ChannelInitialized -= ChannelInitialized;
            channel.ChannelInitialized -= ChannelAvailable;
            channel.ChannelQueueEmpty -= ChannelAvailable;
        }

        private void ChannelConnecting(IChannel channel) {
            var evnt = Connecting;
            if (evnt != null) evnt(this, new ConnectingEventArgs { Binding = channel.Binding, Socket = this });
        }

        private void ChannelConnected(IChannel channel) {
            var evnt = Connected;
            if (evnt != null) evnt(this, new ConnectedEventArgs { Binding = channel.Binding, Socket = this });
        }

        private void ChannelInitialized(IChannel channel) {
            _routeChannels.TryAdd(channel.RemoteNodeId, channel);

            var evnt = Initialized;
            if (evnt != null) evnt(this, new InitializedEventArgs { Binding = channel.Binding, Socket = this, RemoteNodeId = channel.RemoteNodeId });
        }

        private void ChannelDisconnected(IChannel channel, ShutdownReason reason) {
            var binding = channel.Binding;

            _routeChannels.Remove(channel.RemoteNodeId);

            if(channel.Direction == Direction.Inbound || reason == ShutdownReason.Requested) {
                _channels.Remove(binding);
                UnhookChannel(channel);
            }

            var evnt = Disconnected;
            if (evnt != null) evnt(this, new DisconnectedEventArgs { Binding = binding, Reason = reason, Socket = this });
        }

        private void ChannelReceived(ILetter letter, ReceivedEventArgs receivedEventArgs) {
            var evnt = Received;
            if (evnt != null) {
                receivedEventArgs.Socket = this;
                evnt(letter, receivedEventArgs);
            }
        }

        private void ChannelSent(IChannel channel, ILetter letter) {
            var evnt = Sent;
            if (evnt != null)
                evnt(letter, new SentEventArgs { Binding = channel.Binding, Socket = this, RemoteNodeId = channel.RemoteNodeId });
        }

        private void ChannelFailedToSend(IChannel channel, ILetter letter) {
            if(letter.Options.HasFlag(LetterOptions.Multicast)) {
                Discard(channel, letter);
            } else if(letter.Options.HasFlag(LetterOptions.Requeue)) {
                Requeue(channel, letter);
            } else {
                Discard(channel, letter);
            }
        }

        private void Discard(IChannel channel, ILetter letter) {
            var evnt = Discarded;
            if (evnt != null && !letter.Options.HasFlag(LetterOptions.SilentDiscard))
                evnt(letter, new DiscardedEventArgs {Binding = channel.Binding, Socket = this, RemoteNodeId = channel.RemoteNodeId });
        }

        private void Requeue(IChannel channel, ILetter letter) {
            _letterDispatcher.EnqueueLetter(letter);

            var evnt = Requeued;
            if (evnt != null) evnt(letter, new RequeuedEventArgs { Socket = this, RemoteNodeId = channel.RemoteNodeId });
        }

        private void ChannelAvailable(IChannel channel) {
            _letterDispatcher.EnqueueChannel(channel);
        }
    }
}