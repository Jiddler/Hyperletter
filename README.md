# Hyperletter

## Concept
You can think of Hyperletter as a mix between ZMQ and WCF. ZMQ is great for speedy applications but is you need to do a lot of extra work to make sure its reliable. WCF on the other hand is reliable but is a hassle to work with.

In ZMQ you have a lot of different socket pairs, in Hyperletter you have only two. UnicastSocket and MulticastSocket, both sockets can receive and transmit, no matter who is connected or bound.

In ZMQ you´re working with a black box, you put something in and you have no clue when it was delivered, discarded or requeued. You don’t even now if somebody is connected to you or if you´re connected to someone else.

Hyperletter tries to be as transparent as possible with callbacks for all events so you´re code can act on them.

## Reliability
Hyperletter ensures your data is delivered, unless _you_ tell it that what you´re sending is not important. This is set on letter level so if one message is important, and another one is not, they can still share the same sockets.

If there is no-one to deliver the letter to Hyperletter will queue it internally until it’s possible to deliver.

Hyperletter _does not_ persist the queues on disk, so if you´re application crashes you´re data is lost.
You can build disk caching if you want to, just listen to the Sent-event to know when to delete it from you´re persistence. We´re might include this feature in the future.

## Performance
On my laptop, I5 something, Hyperletter can send around 20k letters/second with application level ACKs and around 60k letters/second with the NoAck option. Even with the NoAck option Hyperletter will still detect network failures (on the TCP-level) and requeue those letters.

## Bindings
So far there is only a .NET-binding, if you like the protocol please submit language bindings for your language.

## .NET example
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

    public class Receiver {
        public static void Main() {
            var socket = new UnicastSocket();
            socket.Connect(IPAddress.Parse("127.0.0.1"), 8001);
            Console.WriteLine("RECEIVED");
            socket.Received += letter => {
                Console.WriteLine("RECEIVED");

                var noReliabilityOptions = LetterOptions.NoAck | LetterOptions.SilentDiscard | LetterOptions.NoRequeue;
                socket.Send(new Letter(noReliabilityOptions, new[] { (byte)'B' }));
            };

            Console.ReadLine();
        }        
    }

## Whats next
Addresses, if you´re building chains of sockets and you send a letter from A via B to C and C decides to answer, the letter should get back to A no matter if B is connected to alot of sockets.

## Protocol specification
### Header
     4 bytes: Total length
     1 byte : Letter type
     1 byte : Letter options (SilentDiscard = 1, NoRequeue = 2, NoAck = 4, UniqueId = 8)
    16 bytes: UniqueID (GUID-compatible, Only if UniqueId is used) 
     1 byte : Part count

### Parts
     1 byte : Part type
     4 bytes: Length of data
     X byte : Data

