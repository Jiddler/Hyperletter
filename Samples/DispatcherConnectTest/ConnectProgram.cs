using System;
using System.Net;
using DispatcherUtility;
using Hyperletter;
using Hyperletter.Typed;

namespace DispatcherConnectTest {
    public class ConnectProgram {
        public static void Main()
        {
            var hyperSocket = new UnicastSocket();
            var handleDispatcher = new TypedSocket(hyperSocket, new DefaultTypedHandlerFactory(), new JsonTransportSerializer());
            handleDispatcher.Register<TestMessage, MessageHandler>();
            hyperSocket.Connect(IPAddress.Parse("127.0.0.1"), 8900);

            /*
            for (int i = 0; i < 100; i++)
            {
                handleDispatcher.Send(new TestMessage { Message = "Message from ConnectProgram " });
                Console.WriteLine(DateTime.Now + " SENT MESSAGE");
                Thread.Sleep(1000);
            }*/

            Console.WriteLine("Waiting for messages (Press any key to continue)...");
            Console.ReadKey();
        }
    }

    public class MessageHandler : ITypedHandler<TestMessage> {
        public void Execute(ITypedSocket socket, IAnswerable<TestMessage> message) {
            Console.WriteLine(DateTime.Now + " RECEIVED MESSAGE: " + message.Message.Message);
            Console.WriteLine(DateTime.Now + " SENDING ANSWER");
            message.Answer(new TestMessage() { Message = "ANSWER: " + message.Message.Message});
        }
    }
}