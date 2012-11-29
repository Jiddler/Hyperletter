using System;
using System.Threading;
using Hyperletter.EventArgs;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class BlockingOutstanding<TResult> : Outstanding {
        private readonly TypedHyperSocket _socket;
        private readonly ManualResetEventSlim _waitLock = new ManualResetEventSlim();

        public BlockingOutstanding(TypedHyperSocket socket) {
            _socket = socket;
        }

        public IAnswerable<TResult> Result { get; protected set; }

        public override void SetResult(Metadata metadata, ILetter letter, IReceivedEventArgs receivedEventArgs) {
            var result = _socket.Serializer.Deserialize<TResult>(letter.Parts[1], Type.GetType(metadata.Type));
            Result = new Answerable<TResult>(_socket, result, receivedEventArgs.RemoteNodeId, metadata.ConversationId);
            _waitLock.Set();
        }

        public void Wait() {
            if(!_waitLock.Wait(_socket.Options.AnswerTimeout)) {
                throw new TimeoutException();
            }
        }
    }
}