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
            hs.Received += letter => {
                if(i == 0)
                    sw.Restart();
                i++;
                if (i % 500 == 0) {
                    i = 0;
                    Console.WriteLine(sw.ElapsedMilliseconds);
                }

                //Console.WriteLine(DateTime.Now + " ACTUALY RECEIVED: " + letter.Parts[0].Data);

            };

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
                z++;
                if (z % 500 == 0)
                    Console.WriteLine("<-" + z);
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
