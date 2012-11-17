using System;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class OutboundChannel : AbstractChannel {
        private readonly SocketOptions _options;

        public override event Action<IChannel> ChannelConnecting;

        public OutboundChannel(SocketOptions options, Binding binding, LetterDeserializer letterDeserializer, HyperletterFactory factory) : base(options, binding, letterDeserializer, factory) {
            _options = options;
        }

        public override Direction Direction {
            get { return Direction.Outbound; }
        }

        public void Connect() {
            TryConnect();
        }

        private void TryConnect() {
            if(Disposed)
                return;

            ChannelConnecting(this);

            TcpClient = new TcpClient();
            try {
                TcpClient.BeginConnect(Binding.IpAddress, Binding.Port, EndConnect, null);
            } catch(Exception) {
                TryReconnect();
            }
        }

        private void EndConnect(IAsyncResult ar) {
            try {
                TcpClient.EndConnect(ar);
            } catch (Exception) {
                TryReconnect();
                return;
            }

            TcpClient.NoDelay = true;
            TcpClient.LingerState = new LingerOption(true, 1);
            
            Connected();
        }

        private void TryReconnect() {
            Thread.Sleep(_options.ReconnectIntervall);
            TryConnect();
        }

        protected override void AfterDisconnectHook(ShutdownReason reason) {
            if(reason != ShutdownReason.Requested)
                TryReconnect();
        }
    }
}