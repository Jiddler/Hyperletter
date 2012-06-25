using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class OutboundChannel : AbstractChannel {
        private bool _connecting;
        private bool _reconnect;

        public OutboundChannel(Guid socketId, Binding binding) : base(socketId, binding) {
        }

        public void Connect() {
            TryConnect();
        }

        private void TryConnect() {
            if (IsConnected || _connecting || Disposed)
                return;

            _connecting = true;
            var task = new Task(() => {
                if (_reconnect)
                    Thread.Sleep(1000);

                TcpClient = new TcpClient();
                while(!TcpClient.Connected) {
                    try {
                        TcpClient.Connect(Binding.IpAddress, Binding.Port);
                        _connecting = false;
                        TcpClient.NoDelay = true;
                        TcpClient.LingerState = new LingerOption(true, 1);
                        
                        Connected();
                    } catch(SocketException) {
                        Thread.Sleep(1000);
                    }
                }
            });

            task.Start();
        }
        
        protected override void AfterDisconnected() {
            _reconnect = true;
            TryConnect();
        }
    }
}