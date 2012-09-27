using System.Net.Sockets;

namespace Hyperletter.Channel {
    public class InboundChannel : Channel {
        public InboundChannel(AbstractHyperSocket hyperSocket, TcpClient tcpClient, Binding binding) : base(hyperSocket, binding) {
            TcpClient = tcpClient;
        }

        public override Direction Direction {
            get { return Direction.Inbound; }
        }

        public override void Initialize() {
            Connected();
        }
    }
}