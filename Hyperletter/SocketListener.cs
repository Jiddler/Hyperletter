using System;
using System.Net;
using System.Net.Sockets;
using Hyperletter.Channel;

namespace Hyperletter {
    internal class SocketListener : IDisposable {
        private readonly HyperletterFactory _factory;
        private bool _listening;
        private Socket _socket;
        private Binding _binding;

        public SocketListener(HyperletterFactory factory) {
            _factory = factory;
        }

        public event Action<InboundChannel> IncomingChannel;

        public void Dispose() {
            Stop();
        }

        public Binding Start(IPAddress ipAddress, int port) {
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.IP);
            _socket.Bind(new IPEndPoint(ipAddress, port));
            _socket.Listen(20);

            _binding = new Binding(ipAddress, ((IPEndPoint)_socket.LocalEndPoint).Port);

            _listening = true;

            StartListen();

            return _binding;
        }

        public void Stop() {
            _listening = false;

            if(_socket == null)
                return;

            try {
                _socket.Close();
            } catch(SocketException) {
            }
        }

        private void StartListen() {
            if(!_listening)
                return;

            _socket.BeginAccept(EndAccept, null);
        }

        private void EndAccept(IAsyncResult res) {
            if(!_listening)
                return;

            StartListen();

            try {
                Socket socket = _socket.EndAccept(res);
                socket.NoDelay = true;
                socket.LingerState = new LingerOption(true, 1);
                Binding binding = GetBinding(socket.RemoteEndPoint);
                InboundChannel boundChannel = _factory.CreateInboundChannel(socket, binding);
                IncomingChannel(boundChannel);
            } catch(SocketException) {
            }
        }

        private Binding GetBinding(EndPoint endPoint) {
            var ipEndpoint = ((IPEndPoint) endPoint);
            return new Binding(ipEndpoint.Address, ipEndpoint.Port);
        }
    }
}