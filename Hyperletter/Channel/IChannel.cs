using System;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    public interface IChannel : IDisposable {
        bool IsConnected { get; }
        Guid ConnectedTo { get; }
        Binding Binding { get; }
        Direction Direction { get; }
        event Action<IChannel> ChannelConnected;
        event Action<IChannel> ChannelDisconnected;
        event Action<IChannel> ChannelQueueEmpty;
        event Action<IChannel> ChannelInitialized;

        event Action<IChannel, ILetter> Received;
        event Action<IChannel, ILetter> Sent;
        event Action<IChannel, ILetter> FailedToSend;

        void Initialize();
        EnqueueResult Enqueue(ILetter letter);
    }
}