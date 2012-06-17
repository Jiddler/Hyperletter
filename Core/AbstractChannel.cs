using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter.Abstraction;
using Hyperletter.Core.Extension;

namespace Hyperletter.Core {
    public abstract class AbstractChannel {
        protected TcpClient TcpClient;

        public Binding Binding { get; private set; }
        
        private readonly LetterSerializer _letterSerializer;
        private readonly HyperSocket _hyperSocket;

        private readonly ConcurrentQueue<DeliveryContext> _deliveryQueye = new ConcurrentQueue<DeliveryContext>(); 
        private readonly ConcurrentQueue<ILetter> _letterQueue = new ConcurrentQueue<ILetter>();
        private readonly MemoryStream _receiveBuffer = new MemoryStream();

        private readonly AutoResetEvent _deliverSynchronization = new AutoResetEvent(false);
        private readonly Task _deliverTask;

        private readonly ManualResetEventSlim _receiveSynchronization = new ManualResetEventSlim(false);
        private readonly Task _receiveTask;

        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);

        private readonly byte[] _tcpReceiveBuffer = new byte[512];

        public event Action<AbstractChannel> CanSend;
        public event Action<AbstractChannel, ILetter> Received;
        public event Action<AbstractChannel, ILetter> Sent;
        public event Action<AbstractChannel, ILetter> FailedToSend;

        private IPart _talkingTo;
        public bool IsConnected { get; private set; }
        private bool _userLetterOnDeliveryQueue;
        private int _currentLength;

        protected AbstractChannel(HyperSocket hyperSocket, Binding binding) {
            _letterSerializer = new LetterSerializer();

            _hyperSocket = hyperSocket;
            Binding = binding;

            _receiveTask = new Task(Receive);
            _receiveTask.Start();

            _deliverTask = new Task(Deliver);
            _deliverTask.Start();
        }

        protected virtual void Disconnected() {
            if (!IsConnected)
                return;
            IsConnected = false;

            _deliverSynchronization.Reset();
            _receiveSynchronization.Reset();

            TcpClient.Client.Disconnect(false);
        }

        protected void Connected() {
            _receiveBuffer.SetLength(0);
            _letterQueue.Clear();
            
            _receiveSynchronization.Set();
            BeginSend();

            IsConnected = true;
        }

        private void BeginSend() {
            var letter = new Letter {Type = LetterType.Initialize, Parts = new IPart[] {new Part {Data = _hyperSocket.Id.ToByteArray()}}};
            Enqueue(letter);
        }

        public virtual void Initialize() {
        }

        public void HealthCheck() {
            if (!IsConnected)
                return;

            bool healty;
            try {
                healty = TcpClient.Connected; // && !(TcpClient.Client.Poll(1, SelectMode.SelectRead) && TcpClient.Client.Available == 0);
            } catch (SocketException) {
                healty = false;
            }

            if (!healty) {
                Failure();
            }
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
            _deliveryQueye.Enqueue(new DeliveryContext(letter, context));
            _deliverSynchronization.Set();
        }

        private void Deliver() {
            while(true) {
                _deliverSynchronization.WaitOne();
                DeliveryContext deliveryContext;
                while(_deliveryQueye.TryDequeue(out deliveryContext)) {
                    var letter = _letterSerializer.Serialize(deliveryContext.Send);
                    try {
                        SocketError status;
                        TcpClient.Client.Send(letter, 0, letter.Length, SocketFlags.None, out status);
                            
                        if(status != SocketError.Success)
                            Failure();
                        else if(deliveryContext.Send.Type == LetterType.Ack)
                            HandleAckSent(deliveryContext);
                        else
                            HandleLetterSent(deliveryContext);
                    } catch(SocketException) {
                        Failure();
                    }
                }
            }
        }

        private void HandleLetterSent(DeliveryContext deliveryContext) {
            if(deliveryContext.Send.Options.IsSet(LetterOptions.NoAck)) {
                ILetter sentLetter;
                _letterQueue.TryDequeue(out sentLetter);
                Sent(this, deliveryContext.Send);
                SignalCanSendMoreUserLetter();
            }
        }

        private void HandleAckSent(DeliveryContext deliveryContext) {
            var receivedLetter = deliveryContext.Context;

            if(receivedLetter.Type == LetterType.User)
                TriggerReceived(receivedLetter);
            else if(receivedLetter.Type == LetterType.Initialize)
                _talkingTo = receivedLetter.Parts[0];
        }
      
        private void Receive() {
            while(true) {
                _receiveSynchronization.Wait();
                
                try {
                    SocketError status;
                    var read = TcpClient.Client.Receive(_tcpReceiveBuffer, 0, _tcpReceiveBuffer.Length, SocketFlags.None, out status);
                    if(status != SocketError.Success || read == 0)
                        Failure();
                    else
                        HandleReceived(_tcpReceiveBuffer, read);
                } catch (SocketException ex) {
                    Failure();
                }
            }
        }

        private void HandleReceived(byte[] buffer, int length) {
            int bufferPosition = 0;
            while (bufferPosition < length) {
                if (IsNewMessage()) {
                    _currentLength = BitConverter.ToInt32(buffer, bufferPosition);
                }

                var write = (int)Math.Min(_currentLength - _receiveBuffer.Length, length - bufferPosition);
                _receiveBuffer.Write(buffer, bufferPosition, write);
                bufferPosition += write;

                if (!ReceivedFullLetter())
                    return;

                var letter = _letterSerializer.Deserialize(_receiveBuffer.ToArray());
                _receiveBuffer.SetLength(0);

                if (letter.Type == LetterType.Ack) {
                    ILetter sentLetter;
                    _letterQueue.TryDequeue(out sentLetter);

                    if (sentLetter.Type == LetterType.User) 
                        Sent(this, sentLetter);

                    SignalCanSendMoreUserLetter();
                } else {
                    if (letter.Type == LetterType.Initialize)
                        _talkingTo = letter.Parts[0];

                    if (letter.Options.IsSet(LetterOptions.NoAck))
                        TriggerReceived(letter);
                    else
                        QueueAck(letter);
                }
            }
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

        private void Failure() {
            _cleanUpLock.Reset();
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

            _deliveryQueye.Clear();
            _userLetterOnDeliveryQueue = false;
        }

        private bool ReceivedFullLetter() {
            return _receiveBuffer.Length == _currentLength;
        }

        private bool IsNewMessage() {
            return _receiveBuffer.Length == 0;
        }

        private void TriggerReceived(ILetter receivedLetter) {
            Received(this, receivedLetter);
        }
    }
}