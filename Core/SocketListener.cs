using System;
using System.Net;
using System.Net.Sockets;
using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    internal class SocketListener {
        private readonly HyperSocket _hyperSocket;
        private readonly Binding _binding;
        private TcpListener _listener;

        public event Action<InboundChannel> IncomingChannel;

        public SocketListener(HyperSocket hyperSocket, Binding binding) {
            _hyperSocket = hyperSocket;
            _binding = binding;
        }

        public void Start() {
            _listener = new TcpListener(_binding.IpAddress, _binding.Port);
            _listener.Start();
            StartListen();
        }

        private void StartListen() {
            _listener.BeginAcceptTcpClient(HandleAsyncConnection, null);
        }

        private void HandleAsyncConnection(IAsyncResult res) {
            StartListen();
            
            var tcpClient = _listener.EndAcceptTcpClient(res);
            var binding = GetBinding(tcpClient.Client.RemoteEndPoint);
            var boundChannel = new InboundChannel(_hyperSocket, tcpClient, binding);
            IncomingChannel(boundChannel);
        }

        private Binding GetBinding(EndPoint endPoint) {
            var ipEndpoint = ((IPEndPoint) endPoint);
            return new Binding(ipEndpoint.Address, ipEndpoint.Port);
        }
    }
}