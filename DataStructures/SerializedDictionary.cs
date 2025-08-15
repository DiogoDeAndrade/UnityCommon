using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    [System.Serializable]
    public class SerializedDictionary<KEY, VALUE> : IEnumerable<(KEY, VALUE)>, ISerializationCallbackReceiver
    {
        [System.Serializable]
        struct Elem
        {
            public KEY key;
            public VALUE value;
        }

        [SerializeField]
        private List<Elem> _itemList = new();

        // Runtime cache, rebuilt after deserialization and kept in sync by methods.
        private Dictionary<KEY, VALUE> _itemDic;

        public SerializedDictionary()
        {
            _itemList = new();
        }

        public SerializedDictionary(SerializedDictionary<KEY, VALUE> src)
        {
            _itemList = new(src._itemList);
            if (src._itemDic != null) _itemDic = new(src._itemDic);
        }

        private Dictionary<KEY, VALUE> items
        {
            get
            {
                if (_itemDic == null)
                {
                    // Build from the serialized list as canonical source of truth.
                    _itemDic = new Dictionary<KEY, VALUE>();
                    if (_itemList != null)
                    {
                        for (int i = 0; i < _itemList.Count; i++)
                        {
                            var e = _itemList[i];
                            // If duplicates exist in the serialized data, last one wins.
                            _itemDic[e.key] = e.value;
                        }
                    }
                }
                return _itemDic;
            }
        }

        // Public helper if you ever modify _itemList directly in code (not recommended).
        public void RefreshCache()
        {
            _itemDic = null;
            _ = items; // force rebuild now
        }

        public void Add(KEY key, VALUE value)
        {
            // Ensure cache exists
            var dic = items;
            if (dic.ContainsKey(key)) return;

            _itemList.Add(new Elem { key = key, value = value });
            dic.Add(key, value);
        }

        public void Change(KEY key, VALUE value)
        {
            var dic = items;

            bool found = false;
            for (int i = 0; i < _itemList.Count; i++)
            {
                if (EqualityComparer<KEY>.Default.Equals(_itemList[i].key, key))
                {
                    _itemList[i] = new Elem { key = key, value = value };
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                _itemList.Add(new Elem { key = key, value = value });
            }

            dic[key] = value;
        }

        public void RemoveKey(KEY key)
        {
            var dic = items;
            if (!dic.ContainsKey(key)) return;

            _itemList.RemoveAll(i => EqualityComparer<KEY>.Default.Equals(i.key, key));
            dic.Remove(key);
        }

        public bool ContainsKey(KEY key) => items.ContainsKey(key);

        public bool TryGetValue(KEY key, out VALUE value) => items.TryGetValue(key, out value);

        public int Count => _itemList.Count;

        public VALUE this[KEY key] => items[key];

        public IEnumerator<(KEY, VALUE)> GetEnumerator()
        {
            // Iterate over items to guarantee deduped, latest values
            foreach (var kv in items)
                yield return (kv.Key, kv.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // ISerializationCallbackReceiver: keep _itemDic in sync with _itemList automatically.
        public void OnBeforeSerialize()
        {
            // Nothing special: _itemList is the canonical serialized data.
            // If you wanted _itemDic to be canonical, you could regenerate _itemList here instead.
        }

        public void OnAfterDeserialize()
        {
            // Rebuild runtime cache from serialized list
            _itemDic = new Dictionary<KEY, VALUE>();
            if (_itemList != null)
            {
                for (int i = 0; i < _itemList.Count; i++)
                {
                    var e = _itemList[i];
                    if (e.key == null) continue;

                    _itemDic[e.key] = e.value;
                }
            }
        }
    }
}
