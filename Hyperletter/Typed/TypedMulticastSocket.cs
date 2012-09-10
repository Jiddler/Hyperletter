namespace Hyperletter.Typed {
    public class TypedMulticastSocket : TypedSocket {
        public TypedMulticastSocket(SocketOptions options, ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new MulticastSocket(options), handlerFactory, serializer) {
        }

        public TypedMulticastSocket(ITypedHandlerFactory handlerFactory, ITransportSerializer serializer) : base(new MulticastSocket(), handlerFactory, serializer) {
        }
    }
}