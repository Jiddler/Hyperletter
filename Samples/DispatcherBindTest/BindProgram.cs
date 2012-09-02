using System;
using System.Net;
using System.Threading;
using DispatcherUtility;
using Hyperletter.Core;
using Hyperletter.Dispatcher;

namespace DispatcherBindTest {
    public class BindProgram {
        public static void Main() {
            var hyperSocket = new UnicastSocket();
            var handleDispatcher = new DelegateDispatcher(hyperSocket, new JsonTransportSerializer());
            handleDispatcher.Register<TestMessage>(IncomingTestMessage);
            hyperSocket.Bind(IPAddress.Any, 8900);

            for (int i = 0; i < 100; i++) {
                handleDispatcher.Send(new TestMessage { Message = "Message from BindProgram "});
                Console.WriteLine(DateTime.Now + " SENT MESSAGE");
                Thread.Sleep(1000);
            }

            Console.WriteLine("Waiting for messages (Press any key to continue)...");
            Console.ReadKey();
        }

        private static void IncomingTestMessage(TestMessage message) {
            Console.WriteLine(DateTime.Now + " RECEIVED MESSAGE: " + message.Message);
        }
    }
}