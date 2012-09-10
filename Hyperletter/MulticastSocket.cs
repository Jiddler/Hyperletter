using Hyperletter.Channel;
using Hyperletter.Letter;

namespace Hyperletter {
    public class MulticastSocket : AbstractHyperSocket {
        public MulticastSocket() {
        }

        public MulticastSocket(SocketOptions options) : base(options) {
        }

        protected override void ChannelFailedToSend(IAbstractChannel abstractChannel, ILetter letter) {
            Discard(abstractChannel, letter);
        }

        public override void Send(ILetter letter) {
            foreach(IAbstractChannel channel in Channels.Values) {
                if(channel.IsConnected)
                    channel.Enqueue(letter);
                else
                    ChannelFailedToSend(channel, letter);
            }
        }
    }
}