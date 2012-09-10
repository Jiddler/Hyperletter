using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperletter.Channel {
    public class OutboundChannel : AbstractChannel {
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
            var task = new Task(() => {
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
    }
}