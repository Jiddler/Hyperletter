using System;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal class OutboundChannel : AbstractChannel {
        private bool _connecting;

        public event Action<IChannel> ChannelConnecting;

        public OutboundChannel(SocketOptions options, Binding binding, LetterDeserializer letterDeserializer, HyperletterFactory factory) : base(options, binding, letterDeserializer, factory) {
        }

        public override Direction Direction {
            get { return Direction.Outbound; }
        }

        public void Connect() {
            TryConnect();
        }

        private void TryConnect() {
            if(IsConnected || _connecting || Disposed)
                return;

            _connecting = true;
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

            _connecting = false;
            TcpClient.NoDelay = true;
            TcpClient.LingerState = new LingerOption(true, 1);
            
            Connected();
        }

        private void TryReconnect() {
            _connecting = false;

            Thread.Sleep(1000);
            TryConnect();
        }
    }
}