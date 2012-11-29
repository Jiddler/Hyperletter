using System;
using Hyperletter.EventArgs;
using Hyperletter.EventArgs.Letter;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal abstract class DelegateOutstanding : Outstanding {
        public abstract void SetResult(Exception exception);
    }

    internal class DelegateOutstanding<TRequest, TReply> : DelegateOutstanding {
        private readonly AnswerCallback<TRequest, TReply> _callback;
        private readonly TRequest _request;
        private readonly TypedHyperSocket _socket;

        public DelegateOutstanding(TypedHyperSocket socket, TRequest request, AnswerCallback<TRequest, TReply> callback) {
            _socket = socket;
            _request = request;
            _callback = callback;
        }

        public override void SetResult(Metadata metadata, ILetter letter, IReceivedEventArgs receivedEventArgs) {
            var result = _socket.Serializer.Deserialize<TReply>(letter.Parts[1], Type.GetType(metadata.Type));
            var answerable = new Answerable<TReply>(_socket, result, receivedEventArgs.RemoteNodeId, metadata.ConversationId);

            var eventArgs = new AnswerCallbackEventArgs<TRequest, TReply>(answerable, _request);
            _callback(_socket, eventArgs);
        }

        public override void SetResult(Exception exception) {
            var eventArgs = new AnswerCallbackEventArgs<TRequest, TReply>(_request, exception);
            _callback(_socket, eventArgs);
        }
    }
}