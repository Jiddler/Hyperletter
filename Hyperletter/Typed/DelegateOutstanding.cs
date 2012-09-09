using System;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class DelegateOutstanding<TResult> : Outstanding {
        private readonly Action<ITypedSocket, IAnswerable<TResult>> _callback;
        private readonly TypedSocket _socket;

        public DelegateOutstanding(TypedSocket socket, Action<ITypedSocket, IAnswerable<TResult>> callback) {
            _socket = socket;
            _callback = callback;
        }

        public override void SetResult(Metadata metadata, ILetter letter) {
            var result = _socket.Serializer.Deserialize<TResult>(letter.Parts[1], Type.GetType(metadata.Type));
            var answerable = new Answerable<TResult>(_socket, letter, result);
            _callback(_socket, answerable);
        }
    }
}