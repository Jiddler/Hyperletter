namespace Hyperletter.Typed {
    public class TypedUnicastSocket : TypedSocket {
        public TypedUnicastSocket(SocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new UnicastSocket(options), handlerFactory, serializer) {
        }

        public TypedUnicastSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new UnicastSocket(), handlerFactory, serializer) {
        }
    }
}