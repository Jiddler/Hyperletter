using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    public abstract class AbstractChannel : IChannel {
        private const int HeartbeatInterval = 1000;
        private readonly Timer _heartbeat;
        private readonly HyperSocket _hyperSocket;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _receivedQueue = new ConcurrentQueue<ILetter>();
        protected bool Disposed;
        protected TcpClient TcpClient;

        private CancellationTokenSource _cancellationTokenSource;
        private int _initalizationCount;

        private int _lastAction;
        private int _lastActionHeartbeat;
        private LetterReceiver _receiver;
        private LetterTransmitter _transmitter;

        public bool IsConnected { get; private set; }
        public Guid ConnectedTo { get; private set; }
        public Binding Binding { get; private set; }
        public abstract Direction Direction { get; }

        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel> ChannelDisconnected;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<IChannel, ILetter> Received;
        public event Action<IChannel, ILetter> Sent;
        public event Action<IChannel, ILetter> FailedToSend;

        protected AbstractChannel(HyperSocket hyperSocket, Binding binding) {
            _hyperSocket = hyperSocket;
            Binding = binding;

            _heartbeat = new Timer(Heartbeat);
        }

        public virtual void Initialize() {
        }

        public EnqueueResult Enqueue(ILetter letter) {
            if (!IsConnected)
                FailedToSend(this, letter);

            _queue.Enqueue(letter);
            _transmitter.Enqueue(letter);

            return EnqueueResult.CantEnqueueMore;
        }

        public void Dispose() {
            Disposed = true;
            DisconnectSocket();
        }

        protected void Connected() {
            IsConnected = true;

            _cancellationTokenSource = new CancellationTokenSource();

            _transmitter = new LetterTransmitter(_hyperSocket.LetterSerializer, TcpClient, _cancellationTokenSource);
            _transmitter.Sent += TransmitterOnSent;
            _transmitter.SocketError += SocketError;
            _transmitter.Start();

            _receiver = new LetterReceiver(_hyperSocket.LetterSerializer, TcpClient.Client, _cancellationTokenSource);
            _receiver.Received += ReceiverReceived;
            _receiver.SocketError += SocketError;
            _receiver.Start();

            _initalizationCount = 0;

            Enqueue(new Letter.Letter {Type = LetterType.Initialize, Options = LetterOptions.Ack, Parts = new[] {_hyperSocket.Options.Id.ToByteArray()}});
            ChannelConnected(this);
        }

        private void HandleInitialize() {
            lock(this) {
                _initalizationCount++;
                if(_initalizationCount == 2)
                    ChannelInitialized(this);

                _heartbeat.Change(HeartbeatInterval, HeartbeatInterval);
            }
        }

        private void Heartbeat(object state) {
            if(_lastAction != _lastActionHeartbeat) {
                _lastActionHeartbeat = _lastAction;
            } else {
                Enqueue(new Letter.Letter {Type = LetterType.Heartbeat, Options = LetterOptions.SilentDiscard});
            }
        }

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            if(receivedLetter.Type == LetterType.Ack) {
                HandleLetterSent(_queue.Dequeue());
            } else {
                if(receivedLetter.Options.IsSet(LetterOptions.Ack))
                    QueueAck(receivedLetter);
                else
                    HandleReceivedLetter(receivedLetter);
            }
        }

        private void TransmitterOnSent(ILetter sentLetter) {
            ResetHeartbeatTimer();

            if(sentLetter.Type == LetterType.Ack)
                HandleAckSent();
            else if(!sentLetter.Options.IsSet(LetterOptions.Ack))
                HandleLetterSent(_queue.Dequeue());
        }

        private void HandleAckSent() {
            ILetter receivedLetter = _receivedQueue.Dequeue();
            HandleReceivedLetter(receivedLetter);
        }

        private void HandleReceivedLetter(ILetter receivedLetter) {
            if(receivedLetter.Type == LetterType.Initialize) {
                ConnectedTo = new Guid(receivedLetter.Parts[0]);
                HandleInitialize();
            } else if(receivedLetter.Type == LetterType.User || receivedLetter.Type == LetterType.Batch) {
                Received(this, receivedLetter);
            }
        }

        private void HandleLetterSent(ILetter sentLetter) {
            if(sentLetter.Type == LetterType.Initialize) {
                HandleInitialize();
            } else if(sentLetter.Type == LetterType.User || sentLetter.Type == LetterType.Batch) {
                Sent(this, sentLetter);

                if(_queue.Count == 0 && ChannelQueueEmpty != null)
                    ChannelQueueEmpty(this);
            }
        }

        private void QueueAck(ILetter letter) {
            _receivedQueue.Enqueue(letter);
            var ack = new Letter.Letter {Type = LetterType.Ack, Id = letter.Id, Options = (letter.Options & LetterOptions.UniqueId) == LetterOptions.UniqueId ? LetterOptions.UniqueId : LetterOptions.None};
            _transmitter.Enqueue(ack);
        }

        private void SocketError() {
            lock(this) {
                _heartbeat.Change(Timeout.Infinite, Timeout.Infinite);
                _cancellationTokenSource.Cancel();

                FailQueuedLetters();

                if(IsConnected) {
                    DisconnectSocket();
                    ChannelDisconnected(this);
                }
            }
        }

        private void DisconnectSocket() {
            IsConnected = false;

            try {
                TcpClient.Client.Disconnect(false);
                TcpClient.Client.Dispose();
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