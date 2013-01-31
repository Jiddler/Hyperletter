using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hyperletter.Utility {
    internal class QueueDictionary<T>{
        private readonly LinkedList<T> _list = new LinkedList<T>();
        private readonly Dictionary<T, LinkedListNode<T>> _index = new Dictionary<T, LinkedListNode<T>>();

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim();

        public int Count {
            get {
                try {
                    _lock.EnterReadLock();
                    return _list.Count;
                } finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool TryAdd(T item) {
            try {
                _lock.EnterWriteLock();
                if(_index.ContainsKey(item))
                    return false;

                var node = _list.AddLast(item);
                _index.Add(item, node);

                _manualResetEventSlim.Set();

                return true;
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public bool TryTake(out T item) {
            try {
                _lock.EnterWriteLock();
                var node = _list.First;
                if (node != null) {
                    item = node.Value;
                    _list.Remove(node);
                    _index.Remove(node.Value);

                    _manualResetEventSlim.Set();

                    return true;
                }

                _manualResetEventSlim.Reset();

                item = default(T);
                return false;
            } finally {
                _lock.ExitWriteLock();    
            }
        }
        
        public bool Remove(T item) {
            try {
                _lock.EnterWriteLock();
                LinkedListNode<T> node;
                if (_index.TryGetValue(item, out node)) {
                    _index.Remove(item);
                    _list.Remove(node);
                    return true;
                }
                return false;
            } finally {
                _lock.ExitWriteLock();
            }
        }

        public T Take(CancellationToken cancellationToken) {
            while(true) {
                T item;
                if(TryTake(out item)) {
                    return item;
                }

                _manualResetEventSlim.Wait(cancellationToken);
            }
        }
    }

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
