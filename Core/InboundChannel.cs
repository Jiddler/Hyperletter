using System;
using System.Net.Sockets;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class InboundChannel : AbstractChannel {
        public InboundChannel(Guid socketId, SocketMode socketMode, TcpClient tcpClient, Binding binding) : base(socketId, socketMode, binding) {
            TcpClient = tcpClient;
        }

        public override void Initialize() {
            Connected();
        }
    }
}