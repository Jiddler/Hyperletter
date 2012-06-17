using System;
using System.Net;

namespace Hyperletter.Abstraction {
    public interface IHyperSocket {
        event Action<ILetter> Sent;
        event Action<ILetter> Received;
        event Action<ILetter> Requeued;
        event Action<Binding, ILetter> Discarded;
        Guid Id { get; }
        SocketMode SocketMode { get; set; }
        void Bind(IPAddress ipAddress, int port);
        void Connect(IPAddress ipAddress, int port);
        void Send(ILetter letter);
    }
}