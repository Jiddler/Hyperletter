using System;
using System.Net;
using System.Text;
using Hyperletter;
using Hyperletter.Letter;

namespace ConnectTest {
    internal class ConnectProgram {
        public static object SyncRoot = new object();

        private static void Main() {
            var socketOptions = new SocketOptions { ReconnectInterval = TimeSpan.FromMilliseconds(100)};
            var hyperSocket = new HyperSocket(socketOptions);

            int sent = 0;
            int received = 0;

            hyperSocket.Sent += (socket, letter) => {
                lock(SyncRoot) {
                    sent++;

                    if(sent%20000 == 0) {
                        Console.WriteLine("SENT: " + sent);
                    }
                }
            };

            hyperSocket.Received += (socket, letter) => {
                received++;

                if(received%20000 == 0)
                    Console.WriteLine("RECEIVED: " + received);
            };

            hyperSocket.Discarded += (letter, args) => Console.WriteLine("DISCARDED: " + args.Binding + " " + Encoding.Unicode.GetString(letter.Parts[0]));
            hyperSocket.Requeued += (letter, args) => Console.WriteLine("REQUEUED: " + letter);
            hyperSocket.Queuing += (letter, args) => { };

            hyperSocket.Disconnecting += (socket, args) => Console.WriteLine("DISCONNECTING" + args.Binding + " " + args.Reason);
            hyperSocket.Disconnected += (socket, args) => Console.WriteLine("DISCONNECTED " + args.Binding + " " + args.Reason);
            hyperSocket.Connected += (socket, args) => Console.WriteLine("CONNECTED " + args.Binding);
            hyperSocket.Initialized += (socket, args) => Console.WriteLine("INITIALIZED");
            hyperSocket.Disposed += (socket, args) => {};

            hyperSocket.Connect(IPAddress.Parse("127.0.0.1"), 8001);
            hyperSocket.Connect(IPAddress.Parse("127.0.0.1"), 8002);

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
                else if(line == "reconnect") {
                    hyperSocket.Disconnect(IPAddress.Parse("127.0.0.1"), 8001);
                    hyperSocket.Connect(IPAddress.Parse("127.0.0.1"), 8001);
                } else if(line != "")
                    SendXLetters(hyperSocket, int.Parse(line));
                else
                    SendXLetters(hyperSocket, 1000000);
            }

            hyperSocket.Dispose();
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