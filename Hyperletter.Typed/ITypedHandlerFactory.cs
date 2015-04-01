namespace Hyperletter.Typed {
    public interface ITypedHandlerFactory {
        ITypedHandler<TMessage> CreateHandler<THandler, TMessage>(TMessage message);
    }
}