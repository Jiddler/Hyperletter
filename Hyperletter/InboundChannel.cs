using System;
using System.Net.Sockets;

namespace Hyperletter {
    public class InboundChannel : AbstractChannel {
        public event Action<AbstractChannel, Binding> Disconnect;

        public InboundChannel(HyperSocket hyperSocket, TcpClient tcpClient, Binding binding) : base(hyperSocket, binding) {
            TcpClient = tcpClient;
        }

        public override void Initialize() {
            Connected();
        }

        protected override void Disconnected() {
            Disconnect(this, Binding);
        }
    }
}