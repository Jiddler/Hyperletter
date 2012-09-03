using Hyperletter.Core.Letter;

namespace Hyperletter.Core.Dispatcher {
    public abstract class AbstractDispatcher {
        protected readonly IHyperSocket HyperSocket;
        protected readonly ITransportSerializer Serializer;

        protected AbstractDispatcher(IHyperSocket hyperSocket, ITransportSerializer serializer) {
            HyperSocket = hyperSocket;
            Serializer = serializer;
            HyperSocket.Received += Received;
        }

        protected abstract void Received(ILetter letter);

        public void Send<T>(T value) {
            Send(value, LetterOptions.None);
        }

        public void Send<T>(T value, LetterOptions options) {
            var letter = new Letter.Letter(options);
            var metadata = new Metadata(value.GetType());
            letter.Parts = new byte[2][];
            letter.Parts[0] = Serializer.Serialize(metadata);
            letter.Parts[1] = Serializer.Serialize(value);

            HyperSocket.Send(letter);
        }
    }
}