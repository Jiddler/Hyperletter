using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.EventArgs.Letter;
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
        protected Socket Socket;

        private int _initalizationCount;

        private int _lastAction;
        private int _lastActionHeartbeat;
        private LetterReceiver _receiver;
        private LetterTransmitter _transmitter;
        private DateTime _connectedAt;

        public bool ShutdownRequested { get; private set; }
        public bool IsConnected { get; private set; }

        public bool CanSend { get { return IsConnected && !ShutdownRequested; } }
        public Guid RemoteNodeId { get; private set; }
        public Binding Binding { get; private set; }
        public abstract Direction Direction { get; }
        
        public virtual event Action<IChannel> ChannelConnecting;
        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel, ShutdownReason> ChannelDisconnected;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<ILetter, ReceivedEventArgs> Received;
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
            if (!CanSend) {
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
            _connectedAt = DateTime.UtcNow;
            IsConnected = true;
            ShutdownRequested = false;

            CreateTransmitter();
            CreateReceiver();

            Enqueue(new Letter.Letter { Type = LetterType.Initialize, Options = LetterOptions.Ack, Parts = new[] { _options.NodeId.ToByteArray() } });
            ChannelConnected(this);
        }

        private void CreateReceiver() {
            if(_receiver != null) {
                _receiver.Received -= ReceiverReceived;
                _receiver.SocketError -= Shutdown;
            }
            _receiver = _factory.CreateLetterReceiver(Socket);
            _receiver.Received += ReceiverReceived;
            _receiver.SocketError += Shutdown;
            _receiver.Start();
        }

        private void CreateTransmitter() {
            if(_transmitter != null) {
                _transmitter.Sent -= TransmitterOnSent;
                _transmitter.SocketError -= Shutdown;
            }

            _transmitter = _factory.CreateLetterTransmitter(Socket);
            _transmitter.Sent += TransmitterOnSent;
            _transmitter.SocketError += Shutdown;
            _transmitter.Start();
        }

        private void HandleInitialize() {
            lock(this) {
                _initalizationCount++;
                if(_initalizationCount == 2)
                    ChannelInitialized(this);
            }
        }

        public void Heartbeat() {
            if(_initalizationCount != 2) {
                if(IsConnected) {
                    var now = DateTime.UtcNow;
                    if((now - _connectedAt).TotalMilliseconds > _options.MaximumInitializeTime)
                        Shutdown(ShutdownReason.Socket);
                }

                return;
            }

            if(_lastAction != _lastActionHeartbeat)
                _lastActionHeartbeat = _lastAction;
            else
                Enqueue(HeartbeatLetter);
        }

        public void Disconnect() {
            Shutdown(ShutdownReason.Requested);
        }

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            if(receivedLetter.Type == LetterType.Ack) {
                HandleLetterSent(_queue.Dequeue());
            } else {
                if(receivedLetter.Options.HasFlag(LetterOptions.Ack)) {
                    if(_options.Notification.NotifyBeforeSendingAck)
                        HandleReceivedLetter(receivedLetter, false);

                    QueueAck(receivedLetter);
                } else {
                    HandleReceivedLetter(receivedLetter, false);
                }
            }
        }

        private void TransmitterOnSent(ILetter sentLetter) {
            ResetHeartbeatTimer();
            if(sentLetter.Type == LetterType.Ack)
                HandleAckSent();
            else if(!sentLetter.Options.HasFlag(LetterOptions.Ack))
                HandleLetterSent(_queue.Dequeue());
        }

        private void HandleAckSent() {
            var receivedLetter = _receivedQueue.Dequeue();
            HandleReceivedLetter(receivedLetter, true);
        }

        private void HandleReceivedLetter(ILetter receivedLetter, bool acked) {
            switch(receivedLetter.Type) {
                case LetterType.Initialize:
                    RemoteNodeId = new Guid(receivedLetter.Parts[0]);
                    HandleInitialize();
                    break;

                case LetterType.User:
                    Received(receivedLetter, CreateReceivedEventArgs(receivedLetter, acked));
                    break;

                case LetterType.Batch:
                    for (var i = 0; i < receivedLetter.Parts.Length; i++) {
                        var batchedLetter = _letterDeserializer.Deserialize(receivedLetter.Parts[i]);
                        Received(batchedLetter, CreateReceivedEventArgs(batchedLetter, acked));
                    }
                    break;
            }
        }

        private ReceivedEventArgs CreateReceivedEventArgs(ILetter letter, bool acked) {
            return new ReceivedEventArgs { Acked = acked, AckRequested = letter.Options.HasFlag(LetterOptions.Ack), RemoteNodeId = RemoteNodeId };
        }

        private void HandleLetterSent(ILetter sentLetter) {
            switch(sentLetter.Type) {
                case LetterType.Initialize:
                    HandleInitialize();
                    break;

                case LetterType.Batch:
                case LetterType.User:
                    Sent(this, sentLetter);
                    if (_queue.Count == 0 && ChannelQueueEmpty != null) {
                        ChannelQueueEmpty(this);
                    }
                    break;
            }
        }

        private void QueueAck(ILetter letter) {
            _receivedQueue.Enqueue(letter);
            _transmitter.Enqueue(AckLetter);
        }

        private void Shutdown(ShutdownReason reason) {
            lock (this) {
                if(ShutdownRequested)
                    return;

                ShutdownRequested = true;
            }

            _initalizationCount = 0;
            var wasConnected = IsConnected;
            IsConnected = false;

            if(_transmitter != null) _transmitter.Stop();
            if(_receiver != null) _receiver.Stop();

            DisconnectSocket();
            WaitForTranseiviersToShutDown();
            FailQueuedLetters();

            if (wasConnected) {
                ChannelDisconnected(this, reason);
                AfterDisconnectHook(reason);
            }
        }

        private void WaitForTranseiviersToShutDown() {
            DateTime startedWaitingAt = DateTime.UtcNow;
            while ((_transmitter != null && _transmitter.Sending) || (_receiver != null && _receiver.Receiving)) {
                if((DateTime.UtcNow - startedWaitingAt).TotalMilliseconds > _options.ShutdownWait)
                    break;

                Thread.Sleep(10);
            }

            if(_transmitter != null) _transmitter.Dispose();
            if(_receiver != null) _receiver.Dispose();
        }

        protected virtual void AfterDisconnectHook(ShutdownReason reason) {
        }

        private void DisconnectSocket() {
            try {
                if(Socket != null) {
                    Socket.Close();
                }
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