using System;
using System.Net;
using Hyperletter.EventArgs.Channel;
using Hyperletter.EventArgs.Letter;
using Hyperletter.EventArgs.Socket;
using Hyperletter.Letter;

namespace Hyperletter {
    public interface IHyperSocket : IDisposable {
        SocketOptions Options { get; }
        event Action<IHyperSocket, IConnectingEventArgs> Connecting;
        event Action<IHyperSocket, IConnectedEventArgs> Connected;
        event Action<IHyperSocket, IInitializedEventArgs> Initialized;
        event Action<IHyperSocket, IDisconnectedEventArgs> Disconnected;
        event Action<IHyperSocket, IDisconnectingEventArgs> Disconnecting;

        event Action<ILetter, IQueuingEventArgs> Queuing;
        event Action<ILetter, ISentEventArgs> Sent;
        event Action<ILetter, IDiscardedEventArgs> Discarded;
        event Action<ILetter, IRequeuedEventArgs> Requeued;
        event Action<ILetter, IReceivedEventArgs> Received;

        event Action<IHyperSocket, IDisposedEventArgs> Disposed;

        IPEndPoint Bind(IPAddress ipAddress, int port);
        void Unbind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void Disconnect(IPAddress ipAddress, int port);

        void Send(ILetter letter);
        void SendTo(ILetter letter, Guid toNodeId);
    }
}