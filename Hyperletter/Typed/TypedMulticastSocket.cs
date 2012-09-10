namespace Hyperletter.Typed {
    public class TypedMulticastSocket : TypedSocket {
        public TypedMulticastSocket(TypedSocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(options, new MulticastSocket(options.SocketOptions), handlerFactory, serializer) {
        }

        public TypedMulticastSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new TypedSocketOptions(), new MulticastSocket(), handlerFactory, serializer) {
        }
    }
}