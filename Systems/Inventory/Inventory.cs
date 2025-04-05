using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class Inventory : MonoBehaviour, IEnumerable<(int slot, Item item, int count)>
    {
        public delegate void OnChange(bool add, Item item, int slot);
        public event OnChange onChange;

        [SerializeField]
        private bool limited = false;
        [SerializeField, ShowIf(nameof(limited))]
        private int maxSlots = 9;

        private class Items
        {
            public Item item;
            public int count;
        };

        private List<Items> items;

        public int Add(Item item, int quantity)
        {
            int count = 0;
            for (int i = 0; i < quantity; i++)
            {
                if (Add(item)) count++;
            }

            return count;
        }

        public bool Add(Item item)
        {
            int slotIndex = GetSlot(item);
            if (slotIndex == -1)
            {
                return false;
            }

            items[slotIndex].item = item;
            items[slotIndex].count++;

            onChange?.Invoke(true, item, slotIndex);

            return true;
        }

        public int Remove(Item item, int count)
        {
            int ret = 0;

            for (int i = 0; i < count; i++)
            {
                if (Remove(item)) ret++;
            }

            return ret;
        }

        public bool Remove(Item item)
        {
            int slotIndex = FindItem(item);
            if (slotIndex == -1)
            {
                return false;
            }

            items[slotIndex].count--;

            if (items[slotIndex].count <= 0)
            {
                items[slotIndex].item = null;
                items[slotIndex].count = 0;
            }

            onChange?.Invoke(false, item, slotIndex);

            return true;
        }

        public (Item, int) GetSlotContent(int slot)
        {
            if ((items == null) || (items.Count <= slot)) return (null, 0);

            return (items[slot].item, items[slot].count);
        }

        int FindItem(Item item)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if ((items[i].item == item) && (items[i].count > 0))
                {
                    return i;
                }
            }

            return -1;
        }

        int GetSlot(Item item)
        {
            if (items == null) items = new();

            if (item.isStackable)
            {
                // Find a stack
                for (int i = 0; i < items.Count; i++)
                {
                    if ((items[i].item == item) && (items[i].count < item.maxStack))
                    {
                        return i;
                    }
                }
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if ((items[i].item == null) && (items[i].count == 0))
                    {
                        items[i].item = null;
                        items[i].count = 0;
                        return i;
                    }
                }
            }

            if ((limited) && (items.Count >= maxSlots)) return -1;

            items.Add(new Items { item = item, count = 0 });

            return items.Count - 1;
        }

        public bool HasItem(Item item)
        {
            if (items == null) return false;

            foreach (var i in items)
            {
                if ((i.item == item) && (i.count > 0)) return true;
            }

            return false;
        }

        public bool HasItems()
        {
            if (items == null) return false;

            foreach (var i in items)
            {
                if ((i.item != null) && (i.count > 0)) return true;
            }

            return false;
        }

        public int GetItemCount(Item item)
        {
            if (items == null) return 0;

            int count = 0;
            foreach (var i in items)
            {
                if (i.item == item) count += i.count;
            }

            return count;
        }

        public IEnumerator<(int slot, Item item, int count)> GetEnumerator()
        {
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if ((items[i].item != null) && (items[i].count > 0))
                    {
                        yield return (i, items[i].item, items[i].count);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}