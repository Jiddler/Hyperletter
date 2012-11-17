using System;
using System.Net;
using System.Net.Sockets;
using Hyperletter.Channel;

namespace Hyperletter {
    internal class SocketListener : IDisposable {
        private readonly Binding _binding;
        private readonly HyperletterFactory _factory;
        private TcpListener _listener;
        private bool _listening;

        public event Action<InboundChannel> IncomingChannel;

        public SocketListener(Binding binding, HyperletterFactory factory) {
            _binding = binding;
            _factory = factory;
        }

        public void Dispose() {
            Stop();
        }

        public void Start() {
            _listener = new TcpListener(_binding.IpAddress, _binding.Port);
            _listener.Start();

            _listening = true;

            StartListen();
        }

        public void Stop() {
            _listening = false;

            if (_listener == null)
                return;

            try {
                _listener.Stop();
            } catch(SocketException) {
            }
        }

        private void StartListen() {
            if (!_listening)
                return;

            _listener.BeginAcceptTcpClient(EndAccept, null);
        }

        private void EndAccept(IAsyncResult res) {
            if (!_listening)
                return;

            StartListen();

            var tcpClient = _listener.EndAcceptTcpClient(res);
            tcpClient.NoDelay = true;
            tcpClient.LingerState = new LingerOption(true, 1);
            var binding = GetBinding(tcpClient.Client.RemoteEndPoint);
            var boundChannel = _factory.CreateInboundChannel(tcpClient, binding);
            IncomingChannel(boundChannel);
        }

        private Binding GetBinding(EndPoint endPoint) {
            var ipEndpoint = ((IPEndPoint) endPoint);
            return new Binding(ipEndpoint.Address, ipEndpoint.Port);
        }
    }
}