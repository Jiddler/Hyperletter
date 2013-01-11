using System;
using System.Net;
using System.Net.Sockets;
using Hyperletter.Channel;

namespace Hyperletter {
    internal class SocketListener : IDisposable {
        private readonly Binding _binding;
        private readonly HyperletterFactory _factory;
        private bool _listening;
        private Socket _socket;

        public event Action<InboundChannel> IncomingChannel;

        public SocketListener(Binding binding, HyperletterFactory factory) {
            _binding = binding;
            _factory = factory;
        }

        public void Dispose() {
            Stop();
        }

        public void Start() {
            _socket = new Socket(SocketType.Stream, ProtocolType.IP);
            _socket.Bind(new IPEndPoint(_binding.IpAddress, _binding.Port));
            _socket.Listen(20);

            _listening = true;

            StartListen();
        }

        public void Stop() {
            _listening = false;

            if (_socket == null)
                return;

            try {
                _socket.Close();
            } catch(SocketException) {
            }
        }

        private void StartListen() {
            if (!_listening)
                return;

            _socket.BeginAccept(EndAccept, null);
        }

        private void EndAccept(IAsyncResult res) {
            if (!_listening)
                return;

            StartListen();

            var socket = _socket.EndAccept(res);
            socket.NoDelay = true;
            socket.LingerState = new LingerOption(true, 1);
            var binding = GetBinding(socket.RemoteEndPoint);
            var boundChannel = _factory.CreateInboundChannel(socket, binding);
            IncomingChannel(boundChannel);
        }

        private Binding GetBinding(EndPoint endPoint) {
            var ipEndpoint = ((IPEndPoint) endPoint);
            return new Binding(ipEndpoint.Address, ipEndpoint.Port);
        }
    }
}