using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Hyperletter {
    public abstract class AbstractChannel {
        protected TcpClient TcpClient;

        public Binding Binding { get; private set; }
        
        private readonly LetterSerializer _letterSerializer;
        private readonly HyperSocket _hyperSocket;
        private readonly object _syncRoot = new object();

        private readonly ConcurrentQueue<DeliveryContext> _deliveryQueye = new ConcurrentQueue<DeliveryContext>(); 
        private readonly ConcurrentQueue<ILetter> _letterQueue = new ConcurrentQueue<ILetter>();
        private readonly MemoryStream _receiveBuffer = new MemoryStream();

        private readonly AutoResetEvent _deliverSynchronization = new AutoResetEvent(false);
        private readonly Task _deliverTask;

        private readonly ManualResetEventSlim _receiveSynchronization = new ManualResetEventSlim(false);
        private readonly Task _receiveTask;

        private readonly ManualResetEventSlim _cleanUpLock = new ManualResetEventSlim(true);

        public event Action<AbstractChannel> CanSend;
        public event Action<AbstractChannel, ILetter> Received;
        public event Action<AbstractChannel, ILetter> Sent;
        public event Action<AbstractChannel, ILetter> FailedToSend;

        private IPart _talkingTo;
        public bool IsConnected { get; private set; }
        private bool _userLetterOnDeliveryQueue;

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
            Log("MIGHT DISCONNECTED");
            if (!IsConnected)
                return;
            IsConnected = false;

            Log("DISCONNECTED");
            _deliverSynchronization.Reset();
            _receiveSynchronization.Reset();

            TcpClient.Client.Disconnect(false);
            
        }

        protected void Connected() {
            _receiveBuffer.SetLength(0);
            _letterQueue.Select(s => s).ToList();
            
            _receiveSynchronization.Set();
            BeginSend();

            IsConnected = true;
            Log("CONNECTED");
        }

        private void BeginSend() {
            var letter = new Letter {LetterType = LetterType.Initialize, Parts = new IPart[] {new Part {Data = _hyperSocket.Id.ToByteArray()}}};
            Enqueue(letter);
        }

        public virtual void Initialize() {
        }

        public void HealthCheck() {
            if (!IsConnected)
                return;

            bool healty;
            try {
                healty = TcpClient.Connected && !(TcpClient.Client.Poll(1, SelectMode.SelectRead) && TcpClient.Client.Available == 0);
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
                        else if(deliveryContext.Send.LetterType == LetterType.Ack)
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
        }

        private void HandleAckSent(DeliveryContext deliveryContext) {
            var receivedLetter = deliveryContext.Context;

            if(receivedLetter.LetterType == LetterType.User)
                Received(this, receivedLetter);
            else if(receivedLetter.LetterType == LetterType.Initialize)
                _talkingTo = receivedLetter.Parts[0];
        }

        public static void Log(string message) {
            Console.WriteLine(DateTime.Now.Ticks + " " + message);
        }

        private void Receive() {
            while(true) {
                _receiveSynchronization.Wait();
                var receiveBuffer = new byte[512];
                try {
                    SocketError status;
                    var read = TcpClient.Client.Receive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, out status);
                    if(status != SocketError.Success || read == 0)
                        Failure();
                    else
                        HandleReceived(receiveBuffer, read);
                } catch (SocketException ex) {
                    Failure();
                }
            }
        }

        private void HandleReceived(byte[] buffer, int length) {
            int bufferPosition = 0;
            while (bufferPosition < length) {
                if (IsNewMessage()) {
                    _receiveBuffer.Capacity = BitConverter.ToInt32(buffer, bufferPosition);
                }

                var write = (int)Math.Min(_receiveBuffer.Capacity - _receiveBuffer.Length, length - bufferPosition);
                _receiveBuffer.Write(buffer, bufferPosition, write);
                bufferPosition += write;

                if (!ReceivedFullLetter())
                    return;
                int l = (int)_receiveBuffer.Length;
                var letter = _letterSerializer.Deserialize(_receiveBuffer.ToArray());
                _receiveBuffer.SetLength(0);

                if (letter.LetterType == LetterType.Ack) {
                    ILetter sentLetter;
                    _letterQueue.TryDequeue(out sentLetter);

                    if (sentLetter.LetterType == LetterType.User) 
                        Sent(this, sentLetter);

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
                } else {
                    if (letter.LetterType == LetterType.Initialize)
                        _talkingTo = letter.Parts[0];

                    var ack = new Letter { LetterType = LetterType.Ack };
                    QueueForDelivery(ack, letter);
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
                if (letter.LetterType == LetterType.User)
                    FailedToSend(this, letter);
            }

            DeliveryContext deliveryContext;
            while (_deliveryQueye.TryDequeue(out deliveryContext)) {}

            _userLetterOnDeliveryQueue = false;
        }

        private bool ReceivedFullLetter() {
            return _receiveBuffer.Length == _receiveBuffer.Capacity;
        }

        private bool IsNewMessage() {
            return _receiveBuffer.Length == 0;
        }
    }
}