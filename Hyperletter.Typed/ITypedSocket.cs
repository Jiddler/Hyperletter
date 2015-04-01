using System;
using Hyperletter.EventArgs.Channel;
using Hyperletter.EventArgs.Socket;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    public interface ITypedSocket : IDisposable {
        event Action<ITypedSocket, IConnectingEventArgs> Connecting;
        event Action<ITypedSocket, IConnectedEventArgs> Connected;
        event Action<ITypedSocket, IInitializedEventArgs> Initialized;
        event Action<ITypedSocket, IDisconnectedEventArgs> Disconnected;
        event Action<ITypedSocket, IDisposedEventArgs> Disposed;

        void Register<TMessage, THandler>() where THandler : ITypedHandler<TMessage>;
        void Register<TMessage>(Action<ITypedSocket, IAnswerable<TMessage>> handler);

        void Send<T>(T value, LetterOptions options = LetterOptions.None);
        IAnswerable<TReply> Send<TValue, TReply>(TValue value, LetterOptions options = LetterOptions.None);
        void Send<TRequest, TReply>(TRequest value, AnswerCallback<TRequest, TReply> callback, LetterOptions options = LetterOptions.None);
    }
}