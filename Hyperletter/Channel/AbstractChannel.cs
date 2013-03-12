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

        protected bool Disposed;
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

        public bool ShutdownRequested {
            get { return _shutdownRequested || _remoteShutdownRequested; }
        }

        public bool IsConnected { get; private set; }

        public bool CanSend {
            get { return IsConnected && _initalizationCount == 2; }
        }

        public Guid RemoteNodeId { get; private set; }
        public Binding Binding { get; private set; }
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
            if(!CanSend && (letter.Type == LetterType.User || letter.Type == LetterType.Batch)) {
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
            Lock(LockedConnected);
        }

        private void LockedConnected() {
            CreateTransmitter();
            CreateReceiver();

            Enqueue(new Letter.Letter {Type = LetterType.Initialize, Options = LetterOptions.Ack, Parts = new[] {_options.NodeId.ToByteArray()}});

            _connectedAt = DateTime.UtcNow;
            IsConnected = true;
            _shutdownRequested = false;
            _remoteShutdownRequested = false;

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
            if(Interlocked.Increment(ref _initalizationCount) == 2)
                ChannelInitialized(this);
        }

        public void Heartbeat() {
            if(_initalizationCount != 2) {
                if(IsConnected) {
                    DateTime now = DateTime.UtcNow;
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

            LetterType letterType = receivedLetter.Type;
            if(letterType == LetterType.Ack) {
                HandleLetterSent(_queue.Dequeue());
            } else {
                if((receivedLetter.Options & LetterOptions.Ack) == LetterOptions.Ack) {
                    if(_options.Notification.ReceivedNotifyOnAllAckStates && ((letterType & LetterType.User) == LetterType.User || (letterType & LetterType.Batch) == LetterType.Batch))
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
                    ChannelDisconnecting(this, ShutdownReason.Remote);
                    break;

                case LetterType.User:
                    Received(receivedLetter, CreateReceivedEventArgs(ackState));
                    break;

                case LetterType.Batch:
                    for(int i = 0; i < receivedLetter.Parts.Length; i++) {
                        ILetter batchedLetter = _letterDeserializer.Deserialize(receivedLetter.Parts[i]);
                        Received(batchedLetter, CreateReceivedEventArgs(ackState));
                    }
                    break;
            }
        }

        private ReceivedEventArgs CreateReceivedEventArgs(AckState ackState) {
            return new ReceivedEventArgs {AckState = ackState, RemoteNodeId = RemoteNodeId};
        }

        private void HandleLetterSent(ILetter sentLetter) {
            switch(sentLetter.Type) {
                case LetterType.Initialize:
                    HandleInitialize();
                    break;

                case LetterType.Batch:
                case LetterType.User:
                    Sent(this, sentLetter);
                    if(_queue.Count == 0 && ChannelQueueEmpty != null) {
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
            lock(this) {
                if(_shutdownRequested)
                    return;

                _shutdownRequested = true;
            }

            Lock(() => LockedShutdown(reason));
        }

        private void LockedShutdown(ShutdownReason reason) {
            _shutdownRequested = true;

            if(!_remoteShutdownRequested)
                ChannelDisconnecting(this, reason);

            if(reason == ShutdownReason.Requested) {
                var letter = new Letter.Letter(LetterOptions.Ack) {Type = LetterType.Shutdown};
                Enqueue(letter);

                if(_options.ShutdownGrace.TotalMilliseconds > 0)
                    Thread.Sleep((int) _options.ShutdownGrace.TotalMilliseconds);
                else
                    Thread.Sleep(10);
            }

            _initalizationCount = 0;
            bool wasConnected = IsConnected;
            IsConnected = false;

            if(_transmitter != null) _transmitter.Stop();
            if(_receiver != null) _receiver.Stop();

            DisconnectSocket();
            WaitForTranseiviersToShutDown();

            FailQueuedLetters();
            FailedReceivedLetters();

            if(wasConnected) {
                ChannelDisconnected(this, _remoteShutdownRequested ? ShutdownReason.Remote : reason);
                AfterDisconnectHook(reason);
            }
        }

        private void FailedReceivedLetters() {
            if(!_options.Notification.ReceivedNotifyOnAllAckStates)
                return;

            ReceivedEventArgs eventArgs = CreateReceivedEventArgs(AckState.FailedAck);

            ILetter letter;
            while(_receivedQueue.TryDequeue(out letter)) {
                Received(letter, eventArgs);
            }
        }

        private void WaitForTranseiviersToShutDown() {
            DateTime startedWaitingAt = DateTime.UtcNow;
            while((_transmitter != null && _transmitter.Sending) || (_receiver != null && _receiver.Receiving)) {
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

        public void Lock(Action perform) {
            lock(this) {
                perform();
            }
        }
    }
}