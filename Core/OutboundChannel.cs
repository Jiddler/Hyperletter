using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class OutboundChannel : AbstractChannel {
        private bool _connected;
        private bool _connecting;

        public OutboundChannel(HyperSocket hyperSocket, Binding binding) : base(hyperSocket, binding) {
        }

        public void Connect() {
            TryConnect();
        }

        private void TryConnect() {
            if (_connected || _connecting)
                return;

            _connecting = true;
            
            var task = new Task(() => {
                TcpClient = new TcpClient();
                while(!TcpClient.Connected) {
                    try {
                        TcpClient.Connect(Binding.IpAddress, Binding.Port);
                        _connecting = false;
                        _connected = true;
                        
                        Connected();
                    } catch(SocketException) {
                        Thread.Sleep(1000);
                    }
                }
            });

            task.Start();
        }

        protected override void Disconnected() {
            base.Disconnected();
            
            _connected = false;
            TryConnect();
        }
    }
}