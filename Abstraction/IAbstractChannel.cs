using System;

namespace Hyperletter.Abstraction {
    public interface IAbstractChannel : IDisposable {
        event Action<IAbstractChannel> ChannelConnected;
        event Action<IAbstractChannel> ChannelDisconnected;
        event Action<IAbstractChannel> ChannelQueueEmpty;
        event Action<IAbstractChannel> ChannelInitialized;

        event Action<IAbstractChannel, ILetter> Received;
        event Action<IAbstractChannel, ILetter> Sent;
        event Action<IAbstractChannel, ILetter> FailedToSend;

        bool IsConnected { get; }
        Guid ConnectedTo { get; }
        Binding Binding { get; }
        Direction Direction { get; }

        void Initialize();
        EnqueueResult Enqueue(ILetter letter);
    }
}