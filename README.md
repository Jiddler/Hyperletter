# Hyperletter

Thanks to Jetbrains for supporting Hyperletter with ReSharper!  
[![The Most Intelligent Add-In To Visual Studio](http://www.jetbrains.com/resharper/features/rs/rs1/rs125x37_violet.gif)](http://www.jetbrains.com/)

## Version
We�re currently on V1.3. See below for product roadmap under "Whats next".

## Concept
You can think of Hyperletter as a mix between ZMQ and WCF. ZMQ is great for speedy applications but is you need to do a lot of extra work to make sure its reliable. WCF on the other hand is reliable but is a hassle to work with.

In ZMQ you have a lot of different socket pairs, in Hyperletter you have only two. HyperSocket and TypedHyperSocket. All sockets can receive and transmit. No matter who is bound or connected.

In ZMQ you�re working with a black box, you put something in and you have no clue when it was delivered or discarded. You don�t even know if somebody are connected to you or if you�re connected to someone else.

Hyperletter tries to be as transparent as possible with callbacks for all events so you�re code can act on them.

## Reliability
Hyperletter lets you decide what delivery guarantee you want. You have options like:  
Send and silent discard on failure
Send, requeue on failure and require software ACK.
This options is per letter (not per socket) so you can send one message which you want to guarantee delivery and the next one can be discarded on failure.

Hyperletter can queue letters until a connection is established.

Hyperletter _does not_ persist the queues on disk (see what�s next below), so if you�re application crashes you�re queued data is lost.
You can build disk caching if you want to; listen to the Sent-event to know when to delete it from you�re persistence. We�re going to include this feature in the future.

## Multicast
If socket A is connected to B and C and want to send a letter to both of them, just put LetterOptions.Multicast on the letter you�re sending and Hyperletter will handle the rest.

## Answering
Hyperletter supports answering to letters, like WCF-calls. You can choose between send and block until answer is received or send and callback when answer is received.

## Performance
On my laptop, I5 something.

_With TCP-batching turned off:_ Hyperletter can send around 20k letters/second with application level ACKs and around 60k letters/second with the NoAck option.

_With TCP-batching turned on:_ Depends on configuration, we�ve seen results between 90k and 900k letters/second. If one of the batched letters requires and ACK the batch as a whole will be ACK:ed and therefore its no big performance difference between ack:ed or non-ack:ed mode.

Even in no-ack mode Hyperletter will still detect network most failures (on the TCP-level) and requeue those letters if the Requeue option is set on the letter.

## Bindings
So far there is only a .NET-binding, if you like the protocol please submit language bindings for your language.

## Raw .NET example
See BindTest and ConnectTest in the source for more details

    public class Transmitter {
        public static void Main() {
            var socket = new HyperSocket();
            socket.Bind(IPAddress.Any, 8001);

            Console.WriteLine("TRANSMITTING");
            for(int i=0; i<100; i++) {
                socket.Send(new Letter(LetterOptions.Ack | LetterOptions.Requeue, new[] { (byte) 'A' } ));
            }

            Console.ReadLine();
        }
    }

    public class ReceiveAndAnswer {
        public static void Main() {
            var socket = new HyperSocket();
            
            Console.WriteLine("RECEIVING");
            socket.Received += letter => {
                Console.WriteLine("RECEIVED");

                var noReliabilityOptions = LetterOptions.SilentDiscard;
                socket.Send(new Letter(noReliabilityOptions, new[] { (byte)'B' }));
            };
			
			socket.Connect(IPAddress.Parse("127.0.0.1"), 8001);

            Console.ReadLine();
        }        
    }

## Typed .NET Example
See BindDispatcherTest and ConnectDispatcherTest in source for more details (See WIKI for example of IHandlerFactory and ITransportSerializer).

    public class ConnectProgram {
        public static void Main() {
            var socket = new TypedHyperSocket(new DefaultTypedHandlerFactory(), new JsonTransportSerializer());
            socket.Connect(IPAddress.Parse("127.0.0.1"), 8900);
            
            for (int i = 0; i < 100; i++) {
                string message = "Message from BindProgram " + i;

                Console.WriteLine(DateTime.Now + " SENDING MESSAGE (BLOCKING)   : " + message);
                IAnswerable<TestMessage> reply = socket.Send<TestMessage, TestMessage>(new TestMessage { Message = message });
                Console.WriteLine("RECEIVED ANSWER (BLOCKING): " + reply.Message.Message);
            }

            Console.WriteLine("Waiting for messages (Press enter to continue)...");
            Console.ReadLine();
        }
    }

## Whats next

### V1. Bugfixes
V1 is considered complete, and stable. Bug fixes will be done on a regular basis.

### V2. Persistence
Internal refactoring of the core queuing parts

### V3. Make use of the addresses
if you�re building chains of sockets and you send a letter from A via B to C and C decides to answer, the letter should get back to A no matter if B is connected to multiple sockets.

## Protocol specification
### Header
     4 bytes: Total length
     1 byte : Letter type
		Ack				= 0x01,
        Initialize		= 0x02,
        Heartbeat		= 0x03,
        Batch			= 0x04,
        User			= 0x64

     1 byte : Letter flags
        None			= 0,
        SilentDiscard	= 1,
        Requeue			= 2,
        Ack				= 4,
        UniqueId		= 8,
        Routed			= 16,
        Answer			= 32,
        Multicast		= 64

    16 bytes: UniqueID (GUID-compatible, Only if UniqueId is used) 

### Addresses
	 2 bytes: Addresses count (Up to 65535 addresses can be used)
	 [Multiple]
	16 bytes: Address (GUID-compatible)

### Parts
	 2 bytes: Part count
     [Multiple]
	 4 bytes: Length of data
     X bytes: Data

