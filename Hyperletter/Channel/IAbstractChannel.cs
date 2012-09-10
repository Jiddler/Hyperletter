using System;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    public interface IAbstractChannel : IDisposable {
        bool IsConnected { get; }
        Guid ConnectedTo { get; }
        Binding Binding { get; }
        Direction Direction { get; }
        event Action<IAbstractChannel> ChannelConnected;
        event Action<IAbstractChannel> ChannelDisconnected;
        event Action<IAbstractChannel> ChannelQueueEmpty;
        event Action<IAbstractChannel> ChannelInitialized;

        event Action<IAbstractChannel, ILetter> Received;
        event Action<IAbstractChannel, ILetter> Sent;
        event Action<IAbstractChannel, ILetter> FailedToSend;

        void Initialize();
        EnqueueResult Enqueue(ILetter letter);
    }
}