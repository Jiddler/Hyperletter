using System;
using System.Net;
using Hyperletter;
using Hyperletter.Abstraction;
using Hyperletter.Core;

namespace ConnectTest
{
    class Program
    {
        public static object SyncRoot = new object();
        static void Main(string[] args) {
            var hs = new UnicastSocket();
            //hs.Received += letter => Console.WriteLine(DateTime.Now + " ACTUALY RECEIVED: " + letter.Parts[0].Data);
            int y = 0;
            hs.Sent += letter => {
                lock (SyncRoot) {
                    y++;
                    if (y%5000 == 0) {
                        Console.WriteLine("->" + y);
                    }
                }
            };
            int z = 0;
            hs.Received += letter => {
                z++;
                if(z % 5000 == 0)
                    Console.WriteLine("<-" + z);
            };
            hs.Discarded += hs_Discarded;
            hs.Requeued += letter => Console.WriteLine("REQUEUED");

            hs.Connect(IPAddress.Parse("127.0.0.1"), 8001);
            hs.Connect(IPAddress.Parse("127.0.0.1"), 8002);
            hs.Connect(IPAddress.Parse("127.0.0.1"), 8003);
            string line;
            while ((line = Console.ReadLine()) != null) {
                if (line == "exit")
                    return;
                if(line == "s")
                    Console.WriteLine(y);
                else 
                    for (int i = 0; i < 100000; i++ )
                        //hs.Send(new Letter() { Options = LetterOptions.NoAck, Type = LetterType.User, Parts = new IPart[] { new Part { PartType = PartType.User, Data = new[] { (byte)'A' } } } });
                        hs.Send(new Letter() { Type = LetterType.User, Parts = new IPart[] { new Part { PartType = PartType.User, Data = new[] { (byte)'A' } } } });
            }
        }

        static void hs_Discarded(Binding arg1, ILetter arg2) {
            Console.WriteLine(arg1 + " " + arg2);
        }
    }
    /*
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
    */
}
