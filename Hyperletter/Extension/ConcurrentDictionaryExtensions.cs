using System.Collections.Concurrent;

namespace Hyperletter.Extension {
    public static class ConcurrentDictionaryExtensions {
        public static void Add<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value) {
            dictionary.TryAdd(key, value);
        }

        public static void Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key) {
            TValue value;
            dictionary.TryRemove(key, out value);
        }
    }
}