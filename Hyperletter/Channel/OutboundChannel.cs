using System;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class OutboundChannel : AbstractChannel {
        private readonly SocketOptions _options;

        public OutboundChannel(SocketOptions options, Binding binding, LetterDeserializer letterDeserializer, HyperletterFactory factory) : base(options, binding, letterDeserializer, factory) {
            _options = options;
        }

        public override Direction Direction {
            get { return Direction.Outbound; }
        }

        public override event Action<IChannel> ChannelConnecting;

        public void Connect() {
            TryConnect();
        }

        private void TryConnect() {
            if(Disposed)
                return;

            ChannelConnecting(this);

            Socket = new Socket(Binding.IpAddress.AddressFamily, SocketType.Stream, ProtocolType.IP);
            try {
                Socket.BeginConnect(Binding.IpAddress, Binding.Port, EndConnect, null);
            } catch(Exception) {
                TryReconnect();
            }
        }

        private void EndConnect(IAsyncResult ar) {
            try {
                Socket.EndConnect(ar);
            } catch(Exception) {
                TryReconnect();
                return;
            }

            Socket.NoDelay = true;
            Socket.LingerState = new LingerOption(true, 1);

            Connected();
        }

        private void TryReconnect() {
            Thread.Sleep(_options.ReconnectInterval);
            TryConnect();
        }

        protected override void AfterDisconnectHook(ShutdownReason reason) {
            if(reason != ShutdownReason.Requested)
                TryReconnect();
        }
    }
}