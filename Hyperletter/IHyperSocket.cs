using System;
using System.Net;
using Hyperletter.Letter;

namespace Hyperletter {
    public interface IHyperSocket {
        event Action<IHyperSocket, Binding> Connecting;
        event Action<IHyperSocket, Binding> Connected;
        event Action<IHyperSocket, Binding, ShutdownReason> Disconnected;

        event Action<IHyperSocket, ILetter> Sent;
        event Action<IHyperSocket, Binding, ILetter> Discarded;
        event Action<ILetter> Requeued;
        event Action<IHyperSocket, ILetter> Received;

        SocketOptions Options { get; }

        void Bind(IPAddress ipAddress, int port);
        void Unbind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void Disconnect(IPAddress ipAddress, int port);
        
        void Send(ILetter letter);
        void SendTo(ILetter answer, Guid toNodeId);
        
        void Dispose();
    }
}