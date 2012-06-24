using Hyperletter.Abstraction;

namespace Hyperletter.Core {
    public class MulticastSocket : AbstractHyperSocket {
        protected override IAbstractChannel PrepareChannel(IAbstractChannel channel) {
            return new AbstractChannelQueueDecorator(channel);
        }

        protected override void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            Discard(abstractChannel, letter);
        }

        public override void Send(ILetter letter) {
            foreach (var channel in Channels.Values) {
                if (channel.IsConnected)
                    channel.Enqueue(letter);
                else
                    ChannelFailedToSend(channel, letter);
            }
        }
    }
}