using System.Collections.Concurrent;

namespace Hyperletter.Core.Extension {
    public static class ConcurrentQueueExtensions {
        public static void Clear<T>(this ConcurrentQueue<T> dictionary) {
            T entry;
            while (dictionary.TryDequeue(out entry)) {}
        }
    }
}
