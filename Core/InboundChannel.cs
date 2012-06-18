using System;
using System.Net.Sockets;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class InboundChannel : AbstractChannel {
        public InboundChannel(HyperSocket hyperSocket, TcpClient tcpClient, Binding binding) : base(hyperSocket, binding) {
            TcpClient = tcpClient;
        }

        public override void Initialize() {
            Connected();
        }
    }
}