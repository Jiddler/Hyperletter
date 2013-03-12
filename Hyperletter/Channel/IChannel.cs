using System;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal interface IChannel {
        bool IsConnected { get; }
        bool ShutdownRequested { get; }
        Guid RemoteNodeId { get; }
        Binding Binding { get; }
        Direction Direction { get; }
        bool CanSend { get; }

        event Action<IChannel> ChannelConnected;
        event Action<IChannel> ChannelConnecting;
        event Action<IChannel, ShutdownReason> ChannelDisconnected;
        event Action<IChannel, ShutdownReason> ChannelDisconnecting;
        event Action<IChannel> ChannelQueueEmpty;
        event Action<IChannel> ChannelInitialized;

        event Action<ILetter, ReceivedEventArgs> Received;
        event Action<IChannel, ILetter> Sent;
        event Action<IChannel, ILetter> FailedToSend;

        void Initialize();
        EnqueueResult Enqueue(ILetter letter);
        void Heartbeat();
        void Disconnect();
    }
}