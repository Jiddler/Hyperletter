using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperletter.Channel {
    public class OutboundChannel : Channel {
        private bool _connecting;

        public OutboundChannel(AbstractHyperSocket hyperSocket, Binding binding) : base(hyperSocket, binding) {
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