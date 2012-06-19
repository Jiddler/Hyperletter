using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Abstraction;
using Hyperletter.Core.Channel;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public abstract class AbstractChannel {
        private const int HeartbeatInterval = 1000;

        protected TcpClient TcpClient;
        
        private readonly HyperSocket _hyperSocket;

        private CancellationTokenSource _cancellationTokenSource;
        private LetterTransmitter _transmitter;
        private LetterReceiver _receiver;

        private readonly ConcurrentQueue<ILetter> _letterQueue = new ConcurrentQueue<ILetter>();

        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);
        private readonly Timer _heartbeat;

        public event Action<AbstractChannel> ChannelConnected;
        public event Action<AbstractChannel> ChannelDisconnected;

        public event Action<AbstractChannel> CanSend;
        public event Action<AbstractChannel, ILetter> Received;
        public event Action<AbstractChannel, ILetter> Sent;
        public event Action<AbstractChannel, ILetter> FailedToSend;

        private IPart _talkingTo;
        public bool IsConnected { get; private set; }
        private bool _userLetterOnDeliveryQueue;
        private DateTime _lastAction;

        public Binding Binding { get; private set; }

        protected AbstractChannel(HyperSocket hyperSocket, Binding binding) {
            _heartbeat = new Timer(Heartbeat);

            _hyperSocket = hyperSocket;
            Binding = binding;
        }

        protected void Heartbeat(object state) {
            if ((DateTime.Now - _lastAction).TotalMilliseconds > HeartbeatInterval) {
                _transmitter.TransmitHeartbeat();
            }
        }

        protected void Connected() {
            IsConnected = true;

            _cancellationTokenSource = new CancellationTokenSource();

            _transmitter = new LetterTransmitter(TcpClient.Client, _cancellationTokenSource);
            _transmitter.Sent += TransmitterOnSent;
            _transmitter.SocketError += SocketError;
            _transmitter.Start();

            _receiver = new LetterReceiver(TcpClient.Client, _cancellationTokenSource);
            _receiver.Received += ReceiverReceived;
            _receiver.SocketError += SocketError;
            _receiver.Start();

            _userLetterOnDeliveryQueue = false;

            _heartbeat.Change(HeartbeatInterval, HeartbeatInterval);

            var letter = new Letter { Type = LetterType.Initialize, Parts = new IPart[] { new Part { Data = _hyperSocket.Id.ToByteArray() } } };
            Enqueue(letter);

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

        private void ReceiverReceived(ILetter receivedLetter) {
            ResetHeartbeatTimer();

            if (receivedLetter.Type == LetterType.Ack) {
                ILetter sentLetter;
                _letterQueue.TryDequeue(out sentLetter);

                if (sentLetter.Type == LetterType.User)
                    Sent(this, sentLetter);

                SignalCanSendMoreUserLetter();
            } else {
                if (receivedLetter.Type == LetterType.Initialize)
                    _talkingTo = receivedLetter.Parts[0];

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
            else
                HandleLetterSent(transmitContext);
        }

        private void ResetHeartbeatTimer() {
            _lastAction = DateTime.Now;
        }

        public virtual void Initialize() {
        }

        public void Enqueue(ILetter letter) {
            _cleanUpLock.Wait();

            _letterQueue.Enqueue(letter);

            if(!_userLetterOnDeliveryQueue) {
                _userLetterOnDeliveryQueue = true;
                QueueForDelivery(letter, letter);
            }
        }
        
        private void QueueForDelivery(ILetter letter, ILetter context) {
            _transmitter.Enqueue(new TransmitContext(letter, context));
        }

        private void HandleLetterSent(TransmitContext deliveryContext) {
            if(deliveryContext.Letter.Options.IsSet(LetterOptions.NoAck)) {
                ILetter sentLetter;
                _letterQueue.TryDequeue(out sentLetter);
                Sent(this, deliveryContext.Letter);
                SignalCanSendMoreUserLetter();
            }
        }

        private void HandleAckSent(TransmitContext deliveryContext) {
            var receivedLetter = deliveryContext.Context;

            if(receivedLetter.Type == LetterType.User) {
                Received(this, receivedLetter);
            } else if(receivedLetter.Type == LetterType.Initialize)
                _talkingTo = receivedLetter.Parts[0];
        }
      
        private void QueueAck(ILetter letter) {
            var ack = new Letter {Type = LetterType.Ack};
            QueueForDelivery(ack, letter);
        }

        private void SignalCanSendMoreUserLetter() {
            if (_hyperSocket.SocketMode == SocketMode.Unicast) {
                _userLetterOnDeliveryQueue = false;
                CanSend(this);
            } else if (_hyperSocket.SocketMode == SocketMode.Multicast) {
                ILetter nextLetter;
                if (_letterQueue.TryPeek(out nextLetter)) {
                    QueueForDelivery(nextLetter, nextLetter);
                } else {
                    _userLetterOnDeliveryQueue = false;
                }
            }
        }

        private void SocketError() {
            _cleanUpLock.Reset();
            
            _cancellationTokenSource.Cancel();
            FailQueuedLetters();
            Disconnected();

            _cleanUpLock.Set();
        }

        private void FailQueuedLetters() {
            ILetter letter;
            while(_letterQueue.TryDequeue(out letter)) {
                if (letter.Type == LetterType.User)
                    FailedToSend(this, letter);
            }
        }
    }
}