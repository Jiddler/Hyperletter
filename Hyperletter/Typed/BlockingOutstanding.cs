using System;
using System.Threading;
using Hyperletter.Letter;

namespace Hyperletter.Typed {
    internal class BlockingOutstanding<TResult> : Outstanding {
        private readonly TypedSocket _socket;
        private readonly ManualResetEventSlim _waitLock = new ManualResetEventSlim();

        public BlockingOutstanding(TypedSocket socket) {
            _socket = socket;
        }

        public IAnswerable<TResult> Result { get; protected set; }

        public override void SetResult(Metadata metadata, ILetter letter) {
            var result = _socket.Serializer.Deserialize<TResult>(letter.Parts[1], Type.GetType(metadata.Type));
            Result = new Answerable<TResult>(_socket, letter, result);
            _waitLock.Set();
        }

        public void Wait() {
            if(!_waitLock.Wait(10000)) {
                throw new TimeoutException();
            }
        }
    }
}