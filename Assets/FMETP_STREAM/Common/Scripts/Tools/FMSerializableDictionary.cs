using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FMSolution
{
    [System.Serializable]
    public class FMSerializableDictionary<TKey, TValue>
    {
        public List<TKey> Keys = new List<TKey>();
        public List<TValue> Values = new List<TValue>();
        public TValue this[TKey key]
        {
            get { return Values[Keys.IndexOf(key)]; }
            set
            {
                int index = Keys.IndexOf(key);
                if (index != -1) { Values[index] = value; }
                else { Keys.Add(key); Values.Add(value); }
            }
        }

        public void Add(TKey key, TValue value) { Keys.Add(key); Values.Add(value); }
        public bool Remove(TKey key)
        {
            int index = Keys.IndexOf(key);
            if (index != -1)
            {
                Keys.RemoveAt(index);
                Values.RemoveAt(index);
                return true;
            }
            return false;
        }

        public bool ContainsKey(TKey key) { return Keys.Contains(key); }
        public bool ContainsValue(TValue value) { return Values.Contains(value); }
        public bool TryGetValue(TKey key, out TValue value, bool skipNull = false)
        {
            int index = Keys.IndexOf(key);
            if (index != -1)
            {
                value = Values[index];
                if (skipNull) return value != null;
                return true;
            }
            value = default(TValue);
            return false;
        }

        public void SortByKey()
        {
            var sortedPairs = Keys.Select((key, index) => new { Key = key, Value = Values[index] })
                                  .OrderBy(pair => pair.Key)
                                  .ToList();

            Keys = sortedPairs.Select(pair => pair.Key).ToList();
            Values = sortedPairs.Select(pair => pair.Value).ToList();
        }
    }
}