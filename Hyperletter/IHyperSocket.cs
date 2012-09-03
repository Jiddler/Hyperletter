using System;
using System.Net;
using Hyperletter.Letter;

namespace Hyperletter {
    public interface IHyperSocket {
        event Action<ILetter> Sent;
        event Action<ILetter> Received;
        event Action<Binding, ILetter> Discarded;
        event Action<Binding> Connected;
        event Action<Binding> Disconnected;
        SocketOptions Options { get; set; }
        void Bind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void Send(ILetter letter);
        void Dispose();
    }
}