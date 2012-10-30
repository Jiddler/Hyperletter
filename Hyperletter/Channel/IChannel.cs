using System;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    public interface IChannel {
        bool IsConnected { get; }
        Guid RemoteNodeId { get; }
        Binding Binding { get; }
        Direction Direction { get; }
        
        event Action<IChannel> ChannelConnected;
        event Action<IChannel, DisconnectReason> ChannelDisconnected;
        event Action<IChannel> ChannelQueueEmpty;
        event Action<IChannel> ChannelInitialized;

        event Action<IChannel, ILetter> Received;
        event Action<IChannel, ILetter> Sent;
        event Action<IChannel, ILetter> FailedToSend;

        void Initialize();
        EnqueueResult Enqueue(ILetter letter);
        void Heartbeat();
        void Disconnect();
    }
}