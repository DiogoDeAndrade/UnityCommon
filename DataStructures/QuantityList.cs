using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class QuantityList<T> : IEnumerable<(T value, float quantity)>
    {
        [Serializable]
        class Element
        {
            [SerializeField]
            public T        value;
            [SerializeField]
            public float    quantity;
        }

        [SerializeField]
        private List<Element>   originalElements = new List<Element>();          // Current elements for selection - Need this name to be able to use the ProbList drawers

        // Constructor with optional Random generator and option for with/without replacement
        public QuantityList()
        {
        }

        // Copy constructor that takes a collection and initializes with default counts
        public QuantityList(QuantityList<T> items)
        {

            foreach (var item in items)
            {
                Add(item.value, item.quantity);
            }
        }

        public QuantityList(T initialItem)
        {
            Add(initialItem, 1);
        }

        public int Count => originalElements.Count;

        public (T value, float quantity) this[int index]
        {
            get
            {
                var element = originalElements[index];
                return (element.value, element.quantity);
            }
        }

        public T Get(int index)
        {
            return originalElements[index].value;
        }

        // Add an element with a specified occurrence count
        public void Add(T item, float quantity = 1)
        {
            if (quantity <= 0) throw new ArgumentException("Count must be greater than zero.");

            // Check if this is a new item, or if it should just be added
            int index = IndexOf(item);
            if (index == -1)
            {
                var e = new Element { value = item, quantity = quantity };
                originalElements.Add(e);
            }
            else
            {
                originalElements[index].quantity += quantity;
            }
        }

        internal void Add(QuantityList<T> otherList)
        {
            foreach (var e in otherList)
            {
                Add(e.value, e.quantity);
            }
        }

        public void Remove(T element, float quantity = 1)
        {
            int index = IndexOf(element);
            if (index == -1) return;

            originalElements[index].quantity = Mathf.Max(originalElements[index].quantity - quantity, 0);

            originalElements.RemoveAll((v) => v.quantity <= 0);
        }

        public void Remove(int index, float quantity = 1)
        {
            if (index == -1) return;

            originalElements[index].quantity = Mathf.Max(originalElements[index].quantity - quantity, 0);

            originalElements.RemoveAll((v) => v.quantity <= 0);
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < originalElements.Count; i++)
            {
                if (originalElements[i].value.Equals(item)) return i;
            }
            return -1;
        }

        // Helper to remove an item at the specified index
        private void RemoveItemAtIndex(int index)
        {
            originalElements.RemoveAt(index);
        }

        public IEnumerator<(T value, float quantity)> GetEnumerator()
        {
            for (int i = 0; i < originalElements.Count; i++)
            {
                yield return (originalElements[i].value, originalElements[i].quantity);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Clear()
        {
            originalElements.Clear();
        }

        public void Set(T element, float quantity)
        {
            // Sets a specific element to the given value
            int index = IndexOf(element);
            if (index == -1)
            {
                if (quantity > 0) Add(element, quantity);
                else return;
            }
            else
            {
                originalElements[index].quantity = quantity;
            }
        }

        public float GetQuantity(T element)
        {
            int index = IndexOf(element);
            if (index == -1) return 0;

            return originalElements[index].quantity;
        }

        public void RemoveAll(Predicate<T> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            // Remove from original elements
            int index = 0;
            while (index < originalElements.Count)
            {
                if (predicate(originalElements[index].value))
                {
                    originalElements.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        internal string ToSimpleString()
        {
            string ret = "[ ";

            for (int i = 0; i < originalElements.Count; i++)
            {
                if (i > 0) ret += ", ";
                ret += $"({originalElements[i].value.ToString()}: {originalElements[i].quantity})";
            }

            ret += " ]";

            return ret;
        }
    }

    [Serializable]
    public class ReferenceQuantityList<T> : IEnumerable<(T value, float quantity)>
    {
        [Serializable]
        class Element
        {
            [SerializeReference]
            public T value;
            [SerializeField]
            public float quantity;
        }

        [SerializeField]
        private List<Element> originalElements = new List<Element>();          // Current elements for selection - Need this name to be able to use the ProbList drawers

        // Constructor with optional Random generator and option for with/without replacement
        public ReferenceQuantityList()
        {
        }

        // Copy constructor that takes a collection and initializes with default counts
        public ReferenceQuantityList(QuantityList<T> items)
        {

            foreach (var item in items)
            {
                Add(item.value, item.quantity);
            }
        }

        public ReferenceQuantityList(T initialItem)
        {
            Add(initialItem, 1);
        }

        public int Count => originalElements.Count;

        public (T value, float quantity) this[int index]
        {
            get
            {
                var element = originalElements[index];
                return (element.value, element.quantity);
            }
        }

        public T Get(int index)
        {
            return originalElements[index].value;
        }

        // Add an element with a specified occurrence count
        public void Add(T item, float quantity = 1)
        {
            if (quantity <= 0) throw new ArgumentException("Count must be greater than zero.");

            // Check if this is a new item, or if it should just be added
            int index = IndexOf(item);
            if (index == -1)
            {
                var e = new Element { value = item, quantity = quantity };
                originalElements.Add(e);
            }
            else
            {
                originalElements[index].quantity += quantity;
            }
        }

        internal void Add(QuantityList<T> otherList)
        {
            foreach (var e in otherList)
            {
                Add(e.value, e.quantity);
            }
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < originalElements.Count; i++)
            {
                if (originalElements[i].value.Equals(item)) return i;
            }
            return -1;
        }

        // Helper to remove an item at the specified index
        private void RemoveItemAtIndex(int index)
        {
            originalElements.RemoveAt(index);
        }

        public IEnumerator<(T value, float quantity)> GetEnumerator()
        {
            for (int i = 0; i < originalElements.Count; i++)
            {
                yield return (originalElements[i].value, originalElements[i].quantity);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Clear()
        {
            originalElements.Clear();
        }

        public void Set(T element, float quantity)
        {
            // Sets a specific element to the given value
            int index = IndexOf(element);
            if (index == -1)
            {
                if (quantity > 0) Add(element, quantity);
                else return;
            }
            else
            {
                originalElements[index].quantity = quantity;
            }
        }

        internal float GetQuantity(T element)
        {
            int index = IndexOf(element);
            if (index == -1) return 0;

            return originalElements[index].quantity;
        }

        public void RemoveAll(Predicate<T> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            // Remove from original elements
            int index = 0;
            while (index < originalElements.Count)
            {
                if (predicate(originalElements[index].value))
                {
                    originalElements.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        internal string ToSimpleString()
        {
            string ret = "[ ";

            for (int i = 0; i < originalElements.Count; i++)
            {
                if (i > 0) ret += ", ";
                ret += $"({originalElements[i].value.ToString()}: {originalElements[i].quantity})";
            }

            ret += " ]";

            return ret;
        }
    }

    [Serializable]
    public class StringQuantityList : QuantityList<string> { }
}
