using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hyperletter;
using Hyperletter.Letter;

namespace CoreSendAndReceiveTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            var receiveTask = ReceiveTask(cts.Token);
            var sendTask = SendTask(cts.Token);

            Console.WriteLine("Enter to close");
            Console.ReadLine();

            cts.Cancel();
            Task.WaitAll(receiveTask, sendTask);
        }

        private static async Task ReceiveTask(CancellationToken token) {
            await Task.Yield();

            using(var socket = new HyperSocket()) {
                socket.Connected += (s, e) => Console.WriteLine("R: Connected");
                socket.Connecting += (s, e) => Console.WriteLine("R: Connecting");
                socket.Disconnected += (s, e) => Console.WriteLine("R: Disconnected");
                socket.Disconnecting += (s, e) => Console.WriteLine("R: Disconnecting");
                socket.Initialized += (s, e) => Console.WriteLine("R: Initialized");
                socket.Disposed += (s, e) => Console.WriteLine("R: Disposed");
                socket.Discarded += (l, e) => Console.WriteLine("R: Discarded");
                socket.Queuing += (l, e) => Console.WriteLine("R: Queuing");
                socket.Received += (l, e) => Console.WriteLine($"R: Received({l.UniqueId}): {Encoding.UTF8.GetString(l.Parts[0])}");
                socket.Requeued += (l, e) => Console.WriteLine("R: Requeued");
                socket.Sent += (l, e) => Console.WriteLine("R: Sent");

                socket.Bind(IPAddress.Any, 4711);
                token.WaitHandle.WaitOne();

                socket.Unbind(IPAddress.Any, 4711);
            }
        }

        private static async Task SendTask(CancellationToken token)
        {
            await Task.Yield();

            using(var socket = new HyperSocket()) {
                socket.Connected += (s, e) => Console.WriteLine("S: Connected");
                socket.Connecting += (s, e) => Console.WriteLine("S: Connecting");
                socket.Disconnected += (s, e) => Console.WriteLine("S: Disconnected");
                socket.Disconnecting += (s, e) => Console.WriteLine("S: Disconnecting");
                socket.Initialized += (s, e) => Console.WriteLine("S: Initialized");
                socket.Disposed += (s, e) => Console.WriteLine("S: Disposed");
                socket.Discarded += (l, e) => Console.WriteLine("S: Discarded");
                socket.Queuing += (l, e) => Console.WriteLine($"S: Queuing: {l.UniqueId}");
                socket.Received += (l, e) => Console.WriteLine("S: Received");
                socket.Requeued += (l, e) => Console.WriteLine("S: Requeued");
                socket.Sent += (l, e) => Console.WriteLine($"S: Sent: {l.UniqueId}");

                socket.Send(new Letter { Type = LetterType.User, Parts = new[] { Encoding.UTF8.GetBytes("Letter before connect.") } });
                socket.Connect(IPAddress.Loopback, 4711);
                socket.Send(new Letter {Type = LetterType.User, Parts = new[] {Encoding.UTF8.GetBytes("Letter after connect.")}});

                token.WaitHandle.WaitOne();

                socket.Disconnect(IPAddress.Loopback, 4711);
            }
        }
    }
}