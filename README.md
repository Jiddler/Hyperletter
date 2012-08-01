# Hyperletter

## Version
We´re currently on V1. See below for product roadmap under "Whats next".

## Concept
You can think of Hyperletter as a mix between ZMQ and WCF. ZMQ is great for speedy applications but is you need to do a lot of extra work to make sure its reliable. WCF on the other hand is reliable but is a hassle to work with.

In ZMQ you have a lot of different socket pairs, in Hyperletter you have only two. UnicastSocket and MulticastSocket, both sockets can receive and transmit, no matter who is connected or bound.

In ZMQ you´re working with a black box, you put something in and you have no clue when it was delivered, discarded or requeued. You don’t even now if somebody is connected to you or if you´re connected to someone else.

Hyperletter tries to be as transparent as possible with callbacks for all events so you´re code can act on them.

## Reliability
Hyperletter ensures your data is delivered, unless _you_ tell it that what you´re sending is not important. This is set on letter level so if one message is important, and another one is not, they can still share the same sockets.

If there is no-one to deliver the letter to Hyperletter will queue it internally until it’s possible to deliver.

Hyperletter _does not_ persist the queues on disk (see whats next below), so if you´re application crashes you´re queued data is lost.
You can build disk caching if you want to, just listen to the Sent-event to know when to delete it from you´re persistence. We´re going to include this feature in the future.

## Performance
On my laptop, I5 something.

_With TCP-batching turned off:_ Hyperletter can send around 20k letters/second with application level ACKs and around 60k letters/second with the NoAck option.

_With TCP batching turned on:_ Depends on configuration, we´ve seen results between 90k and 900k letters/second. If one of the batched letters requires and ACK the batch as a whole will be ACK:ed and therefore its no big performance difference between ACK or NoAck.

Even with the NoAck option Hyperletter will still detect network most failures (on the TCP-level) and requeue those letters.

## Bindings
So far there is only a .NET-binding, if you like the protocol please submit language bindings for your language.

## .NET example
See BindTest and ConnectTest in the source for more details

    public class Transmitter {
        public static void Main() {
            var socket = new UnicastSocket();
            socket.Bind(IPAddress.Any, 8001);

            Console.WriteLine("TRANSMITTING");
            for(int i=0; i<100; i++) {
                socket.Send(new Letter(LetterOptions.None, new[] { (byte) 'A' } ));
            }

            Console.ReadLine();
        }
    }

    public class ReceiveAndAnswer {
        public static void Main() {
            var socket = new UnicastSocket();
            
            Console.WriteLine("RECEIVING");
            socket.Received += letter => {
                Console.WriteLine("RECEIVED");

                var noReliabilityOptions = LetterOptions.NoAck | LetterOptions.SilentDiscard | LetterOptions.NoRequeue;
                socket.Send(new Letter(noReliabilityOptions, new[] { (byte)'B' }));
            };
			
			socket.Connect(IPAddress.Parse("127.0.0.1"), 8001);

            Console.ReadLine();
        }        
    }

## Whats next

### V1. Refactoring
Internal refactoring of the core queuing parts

### V2. Persistence
Internal refactoring of the core queuing parts

### V3. Make use of the addresses
if you´re building chains of sockets and you send a letter from A via B to C and C decides to answer, the letter should get back to A no matter if B is connected to multiple sockets.

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
		SilentDiscard	= 0x01,
		NoRequeue		= 0x02,
		NoAck			= 0x04,
		UniqueId		= 0x08

    16 bytes: UniqueID (GUID-compatible, Only if UniqueId is used) 

### Addresses
	 2 bytes: Addresses count
	 [Multiple]
	16 bytes: Address (An address part is allowed to be up to 255 chars)

### Parts
	 2 bytes: Part count
     [Multiple]
	 4 bytes: Length of data
     X bytes: Data

