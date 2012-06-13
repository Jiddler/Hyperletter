using System;
using System.Diagnostics;
using System.Net;
using Hyperletter;

namespace BindTest
{
    class Program
    {
        public static object SyncRoot = new object();
        static void Main(string[] args) {
            var hs = new HyperSocket();
            int i = 0;
            Stopwatch sw = new Stopwatch();
            int y = 0;
            hs.Sent += letter => {
                lock (SyncRoot) {
                    y++;
                    if (y%1000 == 0)
                        Console.WriteLine("->" + y);
                }
            };
            int z = 0;
            hs.Received += letter => {
                if(z == 0)
                    sw.Start();
                z++;
                //if (z % 10000 == 0)
                    Console.WriteLine("<-" + z);
                //if(z == 100000)
                  //  Console.WriteLine("Received: " + z + " in " + sw.ElapsedMilliseconds + " ms");
            };

            int port = int.Parse(args[0]);
            hs.Bind(IPAddress.Any, port);

            string line;
            while ((line = Console.ReadLine()) != null) {
                if(line == "exit")
                    continue;
                
                for (int m = 0; m < 1000; m++ )
                    hs.Send(new Letter() { LetterType = LetterType.User, Parts = new IPart[] { new Part { PartType = PartType.User, Data = new[] { (byte)'A' } } } });
            }
        }
    }
}
