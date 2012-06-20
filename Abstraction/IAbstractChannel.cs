using System;

namespace Hyperletter.Abstraction {
    public interface IAbstractChannel {
        event Action<IAbstractChannel> ChannelConnected;
        event Action<IAbstractChannel> ChannelDisconnected;
        event Action<IAbstractChannel, ILetter> Received;
        event Action<IAbstractChannel, ILetter> Sent;
        event Action<IAbstractChannel, ILetter> FailedToSend;
        bool IsConnected { get; }
        Binding Binding { get; }
        void Initialize();
        void Enqueue(ILetter letter);
    }
}