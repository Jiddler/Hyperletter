using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal abstract class AbstractChannel : IChannel {
        private static readonly Letter.Letter AckLetter = new Letter.Letter { Type = LetterType.Ack };
        private static readonly Letter.Letter HeartbeatLetter = new Letter.Letter {Type = LetterType.Heartbeat, Options = LetterOptions.SilentDiscard};

        private readonly SocketOptions _options;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _receivedQueue = new ConcurrentQueue<ILetter>();
        
        private readonly LetterDeserializer _letterDeserializer;
        private readonly HyperletterFactory _factory;

        protected bool Disposed;
        protected TcpClient TcpClient;

        private CancellationTokenSource _cancellationTokenSource;
        private int _initalizationCount;

        private int _lastAction;
        private int _lastActionHeartbeat;
        private LetterReceiver _receiver;
        private LetterTransmitter _transmitter;
        private DisconnectReason _shutdownReason;
        private bool _wasConnected;
        private bool _shutdownRequested;

        public bool IsConnected { get; private set; }
        public Guid RemoteNodeId { get; private set; }
        public Binding Binding { get; private set; }
        public abstract Direction Direction { get; }
        
        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel, DisconnectReason> ChannelDisconnected;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<IChannel, ILetter> Received;
        public event Action<IChannel, ILetter> Sent;
        public event Action<IChannel, ILetter> FailedToSend;

        protected AbstractChannel(SocketOptions options, Binding binding, LetterDeserializer letterDeserializer, HyperletterFactory factory) {
            _options = options;
            _letterDeserializer = letterDeserializer;
            _factory = factory;
            Binding = binding;
        }

        public virtual void Initialize() {
        }

        public EnqueueResult Enqueue(ILetter letter) {
            if (!IsConnected) {
                FailedToSend(this, letter);
                return EnqueueResult.CantEnqueueMore;
            }

            _queue.Enqueue(letter);
            _transmitter.Enqueue(letter);

            return EnqueueResult.CantEnqueueMore;
        }

        public void Dispose() {
            Disposed = true;
            Disconnect();
        }

        protected void Connected() {
            IsConnected = true;
            _shutdownRequested = false;
          
            _transmitter = _factory.CreateLetterTransmitter(TcpClient.Client);
            _transmitter.Sent += TransmitterOnSent;
            _transmitter.SocketError += SocketError;
            _transmitter.Start();

            _receiver = _factory.CreateLetterReceiver(TcpClient.Client);
            _receiver.Received += ReceiverReceived;
            _receiver.SocketError += SocketError;
            _receiver.Start();

            _initalizationCount = 0;

            Enqueue(new Letter.Letter { Type = LetterType.Initialize, Options = LetterOptions.Ack, Parts = new[] { _options.NodeId.ToByteArray() } });
            ChannelConnected(this);
        }

        private void HandleInitialize() {
            lock(this) {
                _initalizationCount++;
                if(_initalizationCount == 2)
                    ChannelInitialized(this);
            }
        }

        public void Heartbeat() {
            if (_initalizationCount != 2 || !IsConnected)
                return;

            if(_lastAction != _lastActionHeartbeat)
                _lastActionHeartbeat = _lastAction;
            else
                Enqueue(HeartbeatLetter);
        }

        public void Disconnect() {
            BeginShutdown(DisconnectReason.Requested);
        }

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            if(receivedLetter.Type == LetterType.Ack) {
                HandleLetterSent(_queue.Dequeue());
            } else {
                if(receivedLetter.Options.HasFlag(LetterOptions.Ack))
                    QueueAck(receivedLetter);
                else
                    HandleReceivedLetter(receivedLetter);
            }
        }

        private void TransmitterOnSent(ILetter sentLetter) {
            ResetHeartbeatTimer();

            if(sentLetter.Type == LetterType.Ack)
                HandleAckSent();
            else if(!sentLetter.Options.HasFlag(LetterOptions.Ack))
                HandleLetterSent(_queue.Dequeue());

            if(_shutdownRequested)
                EndShutdown(false);
        }

        private void HandleAckSent() {
            var receivedLetter = _receivedQueue.Dequeue();
            HandleReceivedLetter(receivedLetter);
        }

        private void HandleReceivedLetter(ILetter receivedLetter) {
            switch(receivedLetter.Type) {
                case LetterType.Initialize:
                    RemoteNodeId = receivedLetter.RemoteNodeId;
                    HandleInitialize();
                    break;

                case LetterType.User:
                    Received(this, receivedLetter);
                    break;

                case LetterType.Batch:
                    for(var i = 0; i < receivedLetter.Parts.Length; i++)
                        Received(this, _letterDeserializer.Deserialize(RemoteNodeId, receivedLetter.Parts[i]));
                    break;
            }
        }

        private void HandleLetterSent(ILetter sentLetter) {
            switch(sentLetter.Type) {
                case LetterType.Initialize:
                    HandleInitialize();
                    break;

                case LetterType.Batch:
                case LetterType.User:
                    Sent(this, sentLetter);
                    if(_queue.Count == 0 && ChannelQueueEmpty != null)
                        ChannelQueueEmpty(this);
                    break;
            }
        }

        private void QueueAck(ILetter letter) {
            _receivedQueue.Enqueue(letter);
            _transmitter.Enqueue(AckLetter);
        }

        private void SocketError(DisconnectReason reason) {
            BeginShutdown(reason);
        }

        private void BeginShutdown(DisconnectReason reason) {
            lock(this) {
                if (_shutdownRequested)
                    return;

                _shutdownRequested = true;
                _shutdownReason = reason;
                _wasConnected = IsConnected;
                IsConnected = false;

                _transmitter.Stop();
                _receiver.Stop();

                DisconnectSocket();
                EndShutdown(_transmitter.Sending);
            }
        }

        private void EndShutdown(bool transmitterSending) {
            lock(this) {
                if (transmitterSending)
                    return;

                FailQueuedLetters();

                _transmitter.Sent -= TransmitterOnSent;
                _transmitter.SocketError -= SocketError;
                _transmitter = null;

                _receiver.Received -= ReceiverReceived;
                _receiver.SocketError -= SocketError;
                _receiver = null;

                if (_wasConnected) {
                    ChannelDisconnected(this, _shutdownReason);
                    AfterDisconnectHook(_shutdownReason);
                }
            }
        }

        protected virtual void AfterDisconnectHook(DisconnectReason reason) {
        }

        private void DisconnectSocket() {
            try {
                if(TcpClient.Client != null) {
                    TcpClient.Client.Disconnect(false);
                    TcpClient.Client.Dispose();
                }
                TcpClient.Close();
            } catch(Exception) {
            }
        }

        private void FailQueuedLetters() {
            ILetter letter;
            while(_queue.TryDequeue(out letter)) {
                if(letter.Type == LetterType.User || letter.Type == LetterType.Batch)
                    FailedToSend(this, letter);
            }
        }

        private void ResetHeartbeatTimer() {
            _lastAction++;
            if(_lastAction > 10000000)
                _lastAction = 0;
        }
    }
}