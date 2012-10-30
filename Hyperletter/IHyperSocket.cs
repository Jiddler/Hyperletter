using System;
using System.Net;
using Hyperletter.Letter;

namespace Hyperletter {
    public interface IHyperSocket {
        SocketOptions Options { get; }
        event Action<IHyperSocket, ILetter> Sent;
        event Action<IHyperSocket, ILetter> Received;
        event Action<IHyperSocket, Binding, ILetter> Discarded;
        event Action<IHyperSocket, Binding> Connecting;
        event Action<IHyperSocket, Binding> Connected;
        event Action<IHyperSocket, Binding, DisconnectReason> Disconnected;
        event Action<IHyperSocket, Binding> InitalizationFailed;

        void Bind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void SendTo(ILetter letter, Guid nodeId);
        void Send(ILetter letter);
        void Dispose();
    }
}