using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Hyperletter.Abstraction;
using Hyperletter.Core;

namespace BindTest
{
    class Program
    {
        public static object SyncRoot = new object();
        static void Main(string[] args) {
            var hs = new UnicastSocket();
            var sw = new Stopwatch();
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
                lock (hs) {
                    if (z == 0)
                        sw.Restart();
                    z++;
                    if (z%20000 == 0)
                        Console.WriteLine("<-" + z);
                    if (z%100000 == 0) {
                        Console.WriteLine("Received: " + z + " in " + sw.ElapsedMilliseconds + " ms" + ". " + (z/sw.ElapsedMilliseconds) + " letter/millisecond");
                        z = 0;
                    }
                }
            };

            int port = int.Parse(args[0]);
            hs.Bind(IPAddress.Any, port);

            string line;
            while ((line = Console.ReadLine()) != null) {
                if(line == "exit")
                    break;

                if (line == "k") {
                    hs.Dispose();
                    Console.WriteLine("KILLED SOCKET");
                } else
                    for (int m = 0; m < 1000; m++)
                        hs.Send(new Letter { Type = LetterType.User, Parts = new[] { new[] { (byte)'A' } } });
            }

            hs.Dispose();

            Thread.Sleep(500);
        }
    }
}
