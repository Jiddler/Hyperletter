using System;
using System.Net.Sockets;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class InboundChannel : AbstractChannel {
        public InboundChannel(Guid socketId, TcpClient tcpClient, Binding binding) : base(socketId, binding) {
            TcpClient = tcpClient;
        }

        public override void Initialize() {
            Connected();
        }
    }
}