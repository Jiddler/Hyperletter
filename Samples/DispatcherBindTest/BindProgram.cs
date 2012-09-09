using System;
using System.Net;
using System.Threading;
using DispatcherUtility;
using Hyperletter;
using Hyperletter.Typed;

namespace DispatcherBindTest {
    public class BindProgram {
        public static void Main() {
            var hyperSocket = new UnicastSocket();
            var handleDispatcher = new TypedSocket(hyperSocket, new DefaultTypedHandlerFactory(),
                                                   new JsonTransportSerializer());
            handleDispatcher.Register<TestMessage>(IncomingTestMessage);
            hyperSocket.Bind(IPAddress.Any, 8900);

            for(int i = 0; i < 100; i++) {
                string message = "Message from BindProgram " + i;
                Console.WriteLine(DateTime.Now + " SENDING MESSAGE (NONBLOCKING): " + message);
                handleDispatcher.Send<TestMessage, TestMessage>(new TestMessage {Message = message}, Callback);

                Console.WriteLine(DateTime.Now + " SENDING MESSAGE (BLOCKING)   : " + message);
                IAnswerable<TestMessage> reply = handleDispatcher.Send<TestMessage, TestMessage>(new TestMessage {Message = message});
                Console.WriteLine("RECEIVED ANSWER (BLOCKING): " + reply.Message.Message);

                Thread.Sleep(1000);
            }

            Console.WriteLine("Waiting for messages (Press any key to continue)...");
            Console.ReadKey();
        }

        private static void Callback(ITypedSocket socket, IAnswerable<TestMessage> answerable) {
            Console.WriteLine("RECEIVED ANSWER (NONBLOCKING): " + answerable.Message.Message);
        }

        private static void IncomingTestMessage(ITypedSocket typedSocket, IAnswerable<TestMessage> answerable) {
            //Console.WriteLine(DateTime.Now + " RECEIVED MESSAGE: " + answerable.Message.Message);
        }
    }
}