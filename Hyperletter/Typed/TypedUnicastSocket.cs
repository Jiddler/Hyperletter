namespace Hyperletter.Typed {
    public class TypedUnicastSocket : TypedSocket {
        public TypedUnicastSocket(TypedSocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(options, new UnicastSocket(options.SocketOptions), handlerFactory, serializer) {
        }

        public TypedUnicastSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new TypedSocketOptions(), new UnicastSocket(), handlerFactory, serializer) {
        }
    }
}