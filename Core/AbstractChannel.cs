using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Hyperletter.Abstraction;
using Hyperletter.Core.Channel;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public abstract class AbstractChannel {
        private readonly Guid _hyperSocketId;
        private readonly SocketMode _socketMode;
        private const int HeartbeatInterval = 1000;

        protected TcpClient TcpClient;

        private CancellationTokenSource _cancellationTokenSource;
        private LetterTransmitter _transmitter;
        private LetterReceiver _receiver;

        private readonly ConcurrentQueue<ILetter> _letterQueue = new ConcurrentQueue<ILetter>();
        private bool _userLetterOnDeliveryQueue;

        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);
        
        private readonly Timer _heartbeat;
        private int _lastAction;
        private int _lastActionHeartbeat;

        public event Action<AbstractChannel> ChannelConnected;
        public event Action<AbstractChannel> ChannelDisconnected;

        public event Action<AbstractChannel, ILetter> Received;
        public event Action<AbstractChannel, ILetter> Sent;
        public event Action<AbstractChannel, ILetter> FailedToSend;

        private IPart _talkingTo;
        
        public bool IsConnected { get; private set; }
        public Binding Binding { get; private set; }

        protected AbstractChannel(Guid hyperSocketId, SocketMode socketMode, Binding binding) {
            _hyperSocketId = hyperSocketId;
            _socketMode = socketMode;
            Binding = binding;

            _heartbeat = new Timer(Heartbeat);
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

            var letter = new Letter { Type = LetterType.Initialize, Parts = new IPart[] { new Part { Data = _hyperSocketId.ToByteArray() } } };
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

        public virtual void Initialize() {
        }

        public void Enqueue(ILetter letter) {
            _cleanUpLock.Wait();

            _letterQueue.Enqueue(letter);

            if (_socketMode == SocketMode.Unicast) {
                _transmitter.Enqueue(new TransmitContext(letter));
            } else {
                if (!_userLetterOnDeliveryQueue) {
                    _userLetterOnDeliveryQueue = true;
                    _transmitter.Enqueue(new TransmitContext(letter));
                }
            }
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
            var ack = new Letter {Type = LetterType.Ack, Id = letter.Id };
            _transmitter.Enqueue(new TransmitContext(ack, letter));
        }

        private void SignalCanSendMoreUserLetter() {
            if (_socketMode == SocketMode.Multicast) {
                ILetter nextLetter;
                if (_letterQueue.TryPeek(out nextLetter)) {
                    _transmitter.Enqueue(new TransmitContext(nextLetter));
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

        private void ResetHeartbeatTimer() {
            _lastAction++;
            if (_lastAction > 10000000)
                _lastAction = 0;
        }
    }
}