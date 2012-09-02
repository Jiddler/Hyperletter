using System.Collections.Generic;
using System.Linq;

namespace Hyperletter.Dispatcher {
    public class DictionaryList<TKey, TValue> {
        private readonly Dictionary<TKey, List<TValue>> _dictionary;
        private readonly IEnumerable<TValue> _emptyEnumerable = Enumerable.Empty<TValue>();

        public DictionaryList() {
            _dictionary = new Dictionary<TKey, List<TValue>>();
        }

        public void Add(TKey key, TValue value) {
            List<TValue> list;
            if(!_dictionary.TryGetValue(key, out list)) {
                list = new List<TValue>();
                _dictionary.Add(key, list);
            }

            list.Add(value);
        }

        public IEnumerable<TValue> Get(TKey key) {
            List<TValue> list;
            return _dictionary.TryGetValue(key, out list) ? list : _emptyEnumerable;
        }

        public bool Remove(TKey key) {
            return _dictionary.Remove(key);
        }

        public bool Remove(TKey key, TValue value) {
            List<TValue> list;
            if(!_dictionary.TryGetValue(key, out list))
                return false;

            return list.Remove(value);
        }
    }
}