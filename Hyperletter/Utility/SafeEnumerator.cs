using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Hyperletter.Utility {
    public class SafeEnumerator<T> : IEnumerator<T> {
        private readonly IEnumerator<T> _inner;
        private readonly ReaderWriterLockSlim _lock;

        public SafeEnumerator(IEnumerator<T> inner, ReaderWriterLockSlim @lock) {
            _inner = inner;
            _lock = @lock;
        }

        public void Dispose() {
            _lock.ExitReadLock();
        }

        public bool MoveNext() {
            return _inner.MoveNext();
        }

        public void Reset() {
            _inner.Reset();
        }

        public T Current {
            get { return _inner.Current; }
        }

        object IEnumerator.Current {
            get { return Current; }
        }
    }
}
