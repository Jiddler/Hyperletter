using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Hyperletter.Channel;

namespace Hyperletter.Utility {
    internal class QueueDictionary<T> : IProducerConsumerCollection<T> {
        private readonly LinkedList<T> _list = new LinkedList<T>();
        private readonly Dictionary<T, LinkedListNode<T>> _index = new Dictionary<T, LinkedListNode<T>>();

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly object _syncRoot = new object();
        private readonly public ManualResetEventSlim _autoResetEvent = new ManualResetEventSlim();

        public IEnumerator<T> GetEnumerator() {
            _lock.EnterReadLock();
            return new SafeEnumerator<T>(_list.GetEnumerator(), _lock);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index) {
            _lock.EnterReadLock();
            var sourceArray = _list.ToArray();
            Array.Copy(sourceArray, 0, array, index, sourceArray.Length);
            _lock.ExitReadLock();
        }

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

        public object SyncRoot { get { return _syncRoot; } }
        public bool IsSynchronized { get { return true; } }

        public void CopyTo(T[] array, int index) {
            try {
                _lock.EnterReadLock();
                _list.CopyTo(array, index);
            } finally {
                _lock.ExitReadLock();
            }
        }

        public bool TryAdd(T item) {
            try {
                _lock.EnterWriteLock();
                if(_index.ContainsKey(item))
                    return true;

                var node = _list.AddLast(item);
                _index.Add(item, node);

                _autoResetEvent.Set();

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
                    return true;
                }

                item = default(T);
                return false;
            } finally {
                _lock.ExitWriteLock();    
            }
        }

        public T[] ToArray() {
            try {
                _lock.EnterReadLock();
                return _list.ToArray();
            } finally {
                _lock.ExitReadLock();
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

        public IChannel Take(CancellationToken cancellationToken) {
            while(true) {
                _autoResetEvent.Wait(cancellationToken);
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
