using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class DelegateOutstanding<TRequest, TReply> : Outstanding {
        private readonly AnswerCallback<TRequest, TReply> _callback;
        private readonly TRequest _request;
        private readonly TypedSocket _socket;

        public DelegateOutstanding(TypedSocket socket, TRequest request, AnswerCallback<TRequest, TReply> callback) {
            _socket = socket;
            _request = request;
            _callback = callback;
        }

        public override void SetResult(Metadata metadata, ILetter letter) {
            var result = _socket.Serializer.Deserialize<TReply>(letter.Parts[1], Type.GetType(metadata.Type));
            var answerable = new Answerable<TReply>(_socket, letter, result);

            var eventArgs = new AnswerCallbackEventArgs<TRequest, TReply>(answerable, _request);
            _callback(_socket, eventArgs);
        }
    }
}