using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Hyperletter;
using Hyperletter.Letter;

namespace BindTest {
    internal class BindProgram {
        public static object SyncRoot = new object();

        private static void Main(string[] arg) {
            var options = new SocketOptions();
            options.ShutdownGrace = TimeSpan.FromMilliseconds(250);
            var hyperSocket = new HyperSocket(options);

            var stopwatch = new Stopwatch();

            int sent = 0;
            hyperSocket.Sent += (letter, args) => {
                lock(SyncRoot) {
                    sent++;
                    if(sent%1000 == 0)
                        Console.WriteLine("->" + sent);
                }
            };

            hyperSocket.Disconnecting += (socket, args) => Console.WriteLine("DISCONNECTING" + args.Binding + " " + args.Reason);
            hyperSocket.Disconnected += (socket, args) => Console.WriteLine("DISCONNECTED " + args.Binding + " " + args.Reason);
            hyperSocket.Connected += (socket, args) => Console.WriteLine("CONNECTED " + args.Binding);

            int received = 0;
            hyperSocket.Received += (letter, args) => {
                lock(hyperSocket) {
                    if(received == 0)
                        stopwatch.Restart();
                    received++;

                    if(received%20000 == 0)
                        Console.WriteLine("<-" + received);
                    if(received%100000 == 0) {
                        Console.WriteLine("Received: " + received + " in " + stopwatch.ElapsedMilliseconds + " ms" + ". " + (received/stopwatch.ElapsedMilliseconds) + " letter/millisecond");
                        received = 0;
                    }
                }
            };

            int port = int.Parse(arg[0]);
            hyperSocket.Bind(IPAddress.Any, port);

            string line;
            while((line = Console.ReadLine()) != null) {
                if(line == "exit")
                    break;

                if(line == "k") {
                    hyperSocket.Dispose();
                    Console.WriteLine("KILLED SOCKET");
                } else
                    for(int m = 0; m < 1000; m++)
                        hyperSocket.Send(new Letter {Type = LetterType.User, Options = LetterOptions.Multicast, Parts = new[] {new[] {(byte) 'A'}}});
            }

            hyperSocket.Dispose();

            Thread.Sleep(500);
        }
    }
}