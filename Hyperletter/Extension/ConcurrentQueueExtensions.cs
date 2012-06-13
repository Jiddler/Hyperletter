using System.Collections.Concurrent;

namespace Hyperletter.Extension {
    public static class ConcurrentQueueExtensions {
        public static void Clear<T>(this ConcurrentQueue<T> dictionary) {
            T entry;
            while (dictionary.TryDequeue(out entry)) {}
        }
    }
}
