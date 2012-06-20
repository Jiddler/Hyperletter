using System.Collections.Generic;
using System.Threading;

namespace Hyperletter.Core.Collection {
    public class WaitableQueue<T> {
        private readonly ManualResetEventSlim _resetEvent = new ManualResetEventSlim();
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object _syncRoot = new object();
        private int _queueLength;

        public void WaitUntilQueueNotEmpty(CancellationTokenSource cancellationTokenSource) {
            _resetEvent.Wait(cancellationTokenSource.Token);
        }

        public void Enqueue(T value) {
            lock (_syncRoot) {
                _queue.Enqueue(value);
                ChangeQueueLength(1);
            }
        }

        public T Dequeue() {
            lock (_syncRoot) {
                ChangeQueueLength(-1);
                var value = _queue.Dequeue();
                return value;
            }
        }

        private void ChangeQueueLength(int counter) {
            lock (_syncRoot) {
                _queueLength += counter;
                if (_queueLength == 0) {
                    _resetEvent.Reset();
                } else {
                    _resetEvent.Set();
                }
            }
        }
    }
}
