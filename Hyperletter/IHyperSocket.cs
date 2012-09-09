using System;
using System.Net;
using Hyperletter.Letter;

namespace Hyperletter {
    public interface IHyperSocket {
        event Action<IHyperSocket, ILetter> Sent;
        event Action<IHyperSocket, ILetter> Received;
        event Action<IHyperSocket, Binding, ILetter> Discarded;
        event Action<IHyperSocket, Binding> Connected;
        event Action<IHyperSocket, Binding> Disconnected;

        SocketOptions Options { get; set; }

        void Bind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void Answer(ILetter answer, ILetter answeringTo);
        void Send(ILetter letter);
        void Dispose();
    }
}