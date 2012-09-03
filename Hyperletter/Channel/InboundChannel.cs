using System;
using System.Net.Sockets;

namespace Hyperletter.Core.Channel {
    public class InboundChannel : AbstractChannel {
        public InboundChannel(Guid socketId, TcpClient tcpClient, Binding binding) : base(socketId, binding) {
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