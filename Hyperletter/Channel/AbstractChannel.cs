using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Extension;
using Hyperletter.Letter;

namespace Hyperletter.Channel {
    internal abstract class AbstractChannel : IChannel {
        private static readonly Letter.Letter AckLetter = new Letter.Letter {Type = LetterType.Ack};
        private static readonly Letter.Letter HeartbeatLetter = new Letter.Letter {Type = LetterType.Heartbeat, Options = LetterOptions.SilentDiscard};
        private readonly HyperletterFactory _factory;
        private readonly LetterDeserializer _letterDeserializer;

        private readonly SocketOptions _options;

        private readonly ConcurrentQueue<ILetter> _queue = new ConcurrentQueue<ILetter>();
        private readonly ConcurrentQueue<ILetter> _receivedQueue = new ConcurrentQueue<ILetter>();

        private SpinLock _lock = new SpinLock(false);

        protected Socket Socket;
        private DateTime _connectedAt;

        private int _initalizationCount;

        private int _lastAction;
        private int _lastActionHeartbeat;
        private LetterReceiver _receiver;
        private bool _remoteShutdownRequested;
        private bool _shutdownRequested;
        private LetterTransmitter _transmitter;

        protected AbstractChannel(SocketOptions options, Binding binding, LetterDeserializer letterDeserializer, HyperletterFactory factory) {
            _options = options;
            _letterDeserializer = letterDeserializer;
            _factory = factory;
            Binding = binding;
        }

        public bool ShutdownRequested => _shutdownRequested || _remoteShutdownRequested;

        public bool IsConnected { get; private set; }

        public bool CanSend => IsConnected && _initalizationCount == 2;

        public Guid RemoteNodeId { get; private set; }
        public Binding Binding { get; }
        public abstract Direction Direction { get; }

        public virtual event Action<IChannel> ChannelConnecting;
        public event Action<IChannel> ChannelConnected;
        public event Action<IChannel, ShutdownReason> ChannelDisconnected;
        public event Action<IChannel, ShutdownReason> ChannelDisconnecting;
        public event Action<IChannel> ChannelQueueEmpty;
        public event Action<IChannel> ChannelInitialized;

        public event Action<ILetter, ReceivedEventArgs> Received;
        public event Action<IChannel, ILetter> Sent;
        public event Action<IChannel, ILetter> FailedToSend;

        public virtual void Initialize() {
        }

        public EnqueueResult Enqueue(ILetter letter) {
            if(!CanSend) {
                FailedToSend?.Invoke(this, letter);
                return EnqueueResult.CantEnqueueMore;
            }

            InternalEnqueue(letter);

            return EnqueueResult.CantEnqueueMore;
        }

        private void InternalEnqueue(ILetter letter) {
            _queue.Enqueue(letter);
            _transmitter.Enqueue(letter);
        }

        protected void Connected() {
            Lock(LockedConnected);
        }

        private void LockedConnected() {
            ChannelConnected?.Invoke(this);

            CreateTransmitter();
            CreateReceiver();

            InternalEnqueue(new Letter.Letter { Type = LetterType.Initialize, Options = LetterOptions.Ack, Parts = new[] { _options.NodeId.ToByteArray() } });

            _connectedAt = DateTime.UtcNow;
            IsConnected = true;
            _shutdownRequested = false;
            _remoteShutdownRequested = false;
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
            Lock(() => {
                if (Interlocked.Increment(ref _initalizationCount) == 2)
                    ChannelInitialized?.Invoke(this);                     
            });
        }

        public void Heartbeat() {
            if(_initalizationCount != 2) {
                if(!IsConnected) return;
                var now = DateTime.UtcNow;
                if((now - _connectedAt) > _options.MaximumInitializeTime)
                    Shutdown(ShutdownReason.Socket);

                return;
            }

            if(_lastAction != _lastActionHeartbeat)
                _lastActionHeartbeat = _lastAction;
            else
                InternalEnqueue(HeartbeatLetter);
        }

        public void Disconnect() {
            Shutdown(ShutdownReason.Requested);
        }

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            LetterType letterType = receivedLetter.Type;
            if(letterType == LetterType.Ack) {
                HandleLetterSent(_queue.Dequeue());
            } else {
                if((receivedLetter.Options & LetterOptions.Ack) == LetterOptions.Ack) {
                    if(_options.Notification.ReceivedNotifyOnAllAckStates && (letterType == LetterType.User || letterType == LetterType.Batch))
                        HandleReceivedLetter(receivedLetter, AckState.BeforeAck);

                    QueueAck(receivedLetter);
                } else {
                    HandleReceivedLetter(receivedLetter, AckState.NoAck);
                }
            }
        }

        private void TransmitterOnSent(ILetter sentLetter) {
            ResetHeartbeatTimer();
            if(sentLetter.Type == LetterType.Ack)
                HandleAckSent();
            else if((sentLetter.Options & LetterOptions.Ack) != LetterOptions.Ack)
                HandleLetterSent(_queue.Dequeue());
        }

        private void HandleAckSent() {
            ILetter receivedLetter = _receivedQueue.Dequeue();
            HandleReceivedLetter(receivedLetter, AckState.AfterAck);
        }

        private void HandleReceivedLetter(ILetter receivedLetter, AckState ackState) {
            switch(receivedLetter.Type) {
                case LetterType.Initialize:
                    RemoteNodeId = new Guid(receivedLetter.Parts[0]);
                    HandleInitialize();
                    break;

                case LetterType.Shutdown:
                    _remoteShutdownRequested = true;
                    ChannelDisconnecting?.Invoke(this, ShutdownReason.Remote);
                    break;

                case LetterType.User:
                    Received?.Invoke(receivedLetter, CreateReceivedEventArgs(ackState));
                    break;

                case LetterType.Batch:
                    for(var i = 0; i < receivedLetter.Parts.Length; i++) {
                        var batchedLetter = _letterDeserializer.Deserialize(receivedLetter.Parts[i]);
                        Received?.Invoke(batchedLetter, CreateReceivedEventArgs(ackState));
                    }
                    break;
            }
        }

        private ReceivedEventArgs CreateReceivedEventArgs(AckState ackState) {
            return new ReceivedEventArgs {AckState = ackState, RemoteNodeId = RemoteNodeId, Binding = Binding };
        }

        private void HandleLetterSent(ILetter sentLetter) {
            switch(sentLetter.Type) {
                case LetterType.Initialize:
                    HandleInitialize();
                    break;

                case LetterType.Heartbeat:
                    NotifyOnEmptyQueue();
                    break;

                case LetterType.Batch:
                case LetterType.User:
                    Sent?.Invoke(this, sentLetter);
                    NotifyOnEmptyQueue();
                    break;
            }
        }

        private void NotifyOnEmptyQueue() {
            if(_queue.Count == 0) {
                ChannelQueueEmpty?.Invoke(this);
            }
        }

        private void QueueAck(ILetter letter) {
            _receivedQueue.Enqueue(letter);
            _transmitter.Enqueue(AckLetter);
        }

        private void Shutdown(ShutdownReason reason) {
            lock(this) {
                if(_shutdownRequested)
                    return;

                _shutdownRequested = true;
            }

            Lock(() => LockedShutdown(reason));

            AfterDisconnectHook(reason);
        }

        private void LockedShutdown(ShutdownReason reason) {
            if(!_remoteShutdownRequested)
                ChannelDisconnecting?.Invoke(this, reason);

            if(reason == ShutdownReason.Requested) {
                var letter = new Letter.Letter(LetterOptions.Ack) {Type = LetterType.Shutdown};
                InternalEnqueue(letter);

                if(_options.ShutdownGrace.TotalMilliseconds > 0)
                    Thread.Sleep((int) _options.ShutdownGrace.TotalMilliseconds);
                else
                    Thread.Sleep(10);
            }

            bool wasConnected = IsConnected;

            _initalizationCount = 0;
            IsConnected = false;

            _transmitter?.Stop();
            _receiver?.Stop();

            DisconnectSocket();
            WaitForTranseiviersToShutDown();

            FailQueuedLetters();
            FailedReceivedLetters();

            if(wasConnected)
                ChannelDisconnected?.Invoke(this, _remoteShutdownRequested ? ShutdownReason.Remote : reason);
        }

        private void FailedReceivedLetters() {
            if(!_options.Notification.ReceivedNotifyOnAllAckStates)
                return;

            var eventArgs = CreateReceivedEventArgs(AckState.FailedAck);

            while (_receivedQueue.TryDequeue(out ILetter letter))
                Received?.Invoke(letter, eventArgs);
        }

        private void WaitForTranseiviersToShutDown() {
            var startedWaitingAt = DateTime.UtcNow;
            while((_transmitter != null && _transmitter.Sending) || (_receiver != null && _receiver.Receiving)) {
                if((DateTime.UtcNow - startedWaitingAt) > _options.ShutdownWait)
                    break;

                Thread.Sleep(10);
            }

            _transmitter?.Dispose();
            _receiver?.Dispose();
        }

        protected virtual void AfterDisconnectHook(ShutdownReason reason) {
        }

        private void DisconnectSocket() {
            try {
                Socket?.Dispose();
            } catch(Exception) {
            }
        }

        private void FailQueuedLetters() {
            while (_queue.TryDequeue(out ILetter letter))
            {
                if (letter.Type == LetterType.User || letter.Type == LetterType.Batch)
                    FailedToSend?.Invoke(this, letter);
            }
        }

        private void ResetHeartbeatTimer() {
            _lastAction++;
            if(_lastAction > 10000000)
                _lastAction = 0;
        }

        private void Lock(Action perform) {
            bool lockTaken = false;
            
            try {
                _lock.Enter(ref lockTaken);
                perform();
            } finally { 
                if (lockTaken) _lock.Exit(false);
            } 
        }
    }
}