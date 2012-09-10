using System;
using System.Net;
using DispatcherUtility;
using Hyperletter.Typed;

namespace DispatcherConnectTest {
    public class ConnectProgram {
        public static void Main() {
            var socket = new TypedUnicastSocket(new DefaultTypedHandlerFactory(), new JsonTransportSerializer());
            socket.Register<TestMessage, MessageHandler>();
            socket.Connect(IPAddress.Parse("127.0.0.1"), 8900);

            Console.WriteLine("Waiting for messages (Press enter to continue)...");
            Console.ReadLine();
        }
    }

    public class MessageHandler : ITypedHandler<TestMessage> {
        public void Execute(ITypedSocket socket, IAnswerable<TestMessage> message) {
            Console.WriteLine(DateTime.Now + " RECEIVED MESSAGE: " + message.Message.Message);
            Console.WriteLine(DateTime.Now + " SENDING ANSWER");
            message.Answer(new TestMessage {Message = "ANSWER: " + message.Message.Message});
        }
    }
}