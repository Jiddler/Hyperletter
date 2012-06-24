using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Abstraction;
using Hyperletter.Core.Channel;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public abstract class AbstractChannel : IAbstractChannel {
        private readonly Guid _hyperSocketId;
        private const int HeartbeatInterval = 1000;

        protected TcpClient TcpClient;

        private CancellationTokenSource _cancellationTokenSource;
        private LetterTransmitter _transmitter;
        private LetterReceiver _receiver;

        private readonly Queue<ILetter> _queue = new Queue<ILetter>();
        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);
        
        private readonly Timer _heartbeat;
        private int _lastAction;
        private int _lastActionHeartbeat;
        private int _initalizationCount;

        public event Action<IAbstractChannel> ChannelConnected;
        public event Action<IAbstractChannel> ChannelDisconnected;
        public event Action<IAbstractChannel> ChannelQueueEmpty;
        public event Action<IAbstractChannel> ChannelInitialized;

        public event Action<IAbstractChannel, ILetter> Received;
        public event Action<IAbstractChannel, ILetter> Sent;
        public event Action<IAbstractChannel, ILetter> FailedToSend;

        public bool IsConnected { get; private set; }
        public Guid ConnectedTo { get; private set; }
        public Binding Binding { get; private set; }

        protected AbstractChannel(Guid hyperSocketId, Binding binding) {
            _hyperSocketId = hyperSocketId;
            Binding = binding;

            _heartbeat = new Timer(Heartbeat);
        }

        public virtual void Initialize() {
        }

        protected void Heartbeat(object state) {
            if (_lastAction != _lastActionHeartbeat) {
                _lastActionHeartbeat = _lastAction;
                _transmitter.TransmitHeartbeat();
            }
        }

        protected void Connected() {
            IsConnected = true;

            _cancellationTokenSource = new CancellationTokenSource();

            _transmitter = new LetterTransmitter(TcpClient, _cancellationTokenSource);
            _transmitter.Sent += TransmitterOnSent;
            _transmitter.SocketError += SocketError;
            //_transmitter.CanSendMore += () => ChannelQueueEmpty(this);
            _transmitter.Start();

            _receiver = new LetterReceiver(TcpClient.Client, _cancellationTokenSource);
            _receiver.Received += ReceiverReceived;
            _receiver.SocketError += SocketError;
            _receiver.Start();

            _initalizationCount = 0;

            Enqueue(new Letter { Type = LetterType.Initialize, Parts = new IPart[] { new Part { Data = _hyperSocketId.ToByteArray() } } });

            ChannelConnected(this);
        }

        protected void Disconnected() {
            if (!IsConnected)
                return;
            IsConnected = false;
            _heartbeat.Change(Timeout.Infinite, Timeout.Infinite);

            _cancellationTokenSource.Cancel();
            TcpClient.Client.Disconnect(false);

            ChannelDisconnected(this);
            
            AfterDisconnected();
        }

        protected virtual void AfterDisconnected() { }

        public void Enqueue(ILetter letter) {
            _cleanUpLock.Wait();
            _queue.Enqueue(letter);
            _transmitter.Enqueue(new TransmitContext(letter));
        }

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            if (receivedLetter.Type == LetterType.Ack) {
                HandleLetterSent();
            } else {
                if (receivedLetter.Options.IsSet(LetterOptions.NoAck))
                    Received(this, receivedLetter);
                else
                    QueueAck(receivedLetter);
            }
        }

        private void TransmitterOnSent(TransmitContext transmitContext) {
            ResetHeartbeatTimer();

            if(transmitContext.Letter.Type == LetterType.Ack)
                HandleAckSent(transmitContext);
            else if (transmitContext.Letter.Options.IsSet(LetterOptions.NoAck))
                HandleLetterSent();
        }

        private void HandleLetterSent() {
            var sentLetter = _queue.Dequeue();
            if (sentLetter.Type == LetterType.Initialize)
                HandleInitialize();
            else {
                Sent(this, sentLetter);

                if (_queue.Count == 0 && ChannelQueueEmpty != null)
                    ChannelQueueEmpty(this);
            }
        }

        private void HandleInitialize() {
            lock(this) {
                _initalizationCount++;
                if(_initalizationCount == 2)
                    ChannelInitialized(this);

                _heartbeat.Change(HeartbeatInterval, HeartbeatInterval);
            }
        }

        private void HandleAckSent(TransmitContext deliveryContext) {
            var receivedLetter = deliveryContext.Context;

            if(receivedLetter.Type == LetterType.User) {
                Received(this, receivedLetter);
            } else if (receivedLetter.Type == LetterType.Initialize) {
                ConnectedTo = new Guid(receivedLetter.Parts[0].Data);
                HandleInitialize();
            }
        }
      
        private void QueueAck(ILetter letter) {
            var ack = new Letter {Type = LetterType.Ack, Id = letter.Id, Options = (letter.Options & LetterOptions.UniqueId) == LetterOptions.UniqueId ? LetterOptions.UniqueId : LetterOptions.None };
            _transmitter.Enqueue(new TransmitContext(ack, letter));
        }

        private void SocketError() {
            _cleanUpLock.Reset();
            
            _cancellationTokenSource.Cancel();
            FailQueuedLetters();
            Disconnected();

            _cleanUpLock.Set();
        }

        private void FailQueuedLetters() {
            while (_queue.Count > 0)
                FailedToSend(this, _queue.Dequeue());
        }

        private void ResetHeartbeatTimer() {
            _lastAction++;
            if (_lastAction > 10000000)
                _lastAction = 0;
        }
    }
}