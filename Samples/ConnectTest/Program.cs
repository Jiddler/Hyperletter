using System;
using System.Net;
using System.Text;
using System.Threading;
using Hyperletter;
using Hyperletter.Letter;

namespace ConnectTest {
    internal class Program {
        public static object SyncRoot = new object();

        private static void Main() {
            var socketOptions = new SocketOptions();
            var unicastSocket = new HyperSocket(socketOptions);

            int sent = 0;

            unicastSocket.Sent += (socket, letter) => {
                                      lock(SyncRoot) {
                                          sent++;

                                          if(sent%20000 == 0)
                                              Console.WriteLine("SENT: " + sent);
                                      }
                                  };

            int received = 0;
            unicastSocket.Received += (socket, letter) => {
                                          received++;

                                          if(received%20000 == 0)
                                              Console.WriteLine("RECEIVED: " + received);
                                      };

            unicastSocket.Discarded += (letter, args) => Console.WriteLine("DISCARDED: " + args.Binding + " " + Encoding.Unicode.GetString(letter.Parts[0]));
            unicastSocket.Requeued += (letter, args) => Console.WriteLine("REQUEUED: " + letter);

            unicastSocket.Disconnecting += (socket, args) => Console.WriteLine("DISCONNECTING" + args.Binding + " " + args.Reason);
            unicastSocket.Disconnected += (socket, args) => {
                {
                    Console.WriteLine("DISCONNECTED " + args.Binding + " " + args.Reason);
                }};
            unicastSocket.Connected += (socket, args) => Console.WriteLine("CONNECTED " + args.Binding);
            var x = 0;
            unicastSocket.Initialized += (socket, args) => {
            //    unicastSocket.Dispose();
                                         };
            unicastSocket.Disposed += (socket, args) => unicastSocket.Connect(IPAddress.Parse("127.0.0.1"), 8001);

            unicastSocket.Connect(IPAddress.Parse("127.0.0.1"), 8001);
            //unicastSocket.Connect(IPAddress.Parse("127.0.0.1"), 8002);

            Console.WriteLine("Commands: ");
            Console.WriteLine("status\t\t- print number of sent and received letters");
            Console.WriteLine("any number\t\t- send a number of letters");
            Console.WriteLine("anything else\t- Send 1 000 000 letters");
            Console.WriteLine("exit\t\t- exit");

            string line;
            Console.Write("\n\nENTER COMMAND: ");
            while((line = Console.ReadLine()) != null) {
                if(line == "exit")
                    return;
                else if(line == "status")
                    WriteStatus(sent, received);
                else if (line == "reconnect") {
                    unicastSocket.Disconnect(IPAddress.Parse("127.0.0.1"), 8001);
                    unicastSocket.Connect(IPAddress.Parse("127.0.0.1"), 8001);
                } else if (line != "")
                    SendXLetters(unicastSocket, int.Parse(line));
                else
                    SendXLetters(unicastSocket, 1000000);
            }

            unicastSocket.Dispose();
        }

        private static void WriteStatus(int sent, int received) {
            Console.WriteLine("SENT: " + sent);
            Console.WriteLine("RECEIVED: " + received);
        }

        private static void SendXLetters(HyperSocket unicastSocket, int numberToSend) {
            for(int i = 0; i < numberToSend; i++)
                unicastSocket.Send(new Letter {Options = LetterOptions.Ack | LetterOptions.Requeue, Type = LetterType.User, Parts = new[] {Encoding.Unicode.GetBytes("Hej " + i)}});
        }
    }
}