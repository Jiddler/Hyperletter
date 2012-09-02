using System;
using System.Net;

namespace Hyperletter.Abstraction {
    public interface IAbstractHyperSocket {
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