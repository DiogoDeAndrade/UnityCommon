using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using UC.RPG;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class InventoryRPGInstance : IEnumerable<(int slot, RPGEntity item, int count)>
    {
        public delegate void OnChange(bool add, RPGEntity item, int slot);
        public event OnChange onChange;

        [SerializeField]
        private bool limited = false;
        [SerializeField, ShowIf(nameof(limited))]
        private int maxSlots = 9;

        public InventoryRPGInstance(bool limited = false, int maxSlots = 9)
        {
            this.limited = limited;
            this.maxSlots = maxSlots;
        }

        [Serializable]
        public class Items
        {
            public RPGEntity    item;
            public int          count;
        };

        private List<Items> items;

        public int Add(RPGEntity item, int quantity)
        {
            int count = 0;
            for (int i = 0; i < quantity; i++)
            {
                if (Add(item)) count++;
            }

            return count;
        }

        public bool Add(RPGEntity item)
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

        public void SetOnSlot(int slotIndex, RPGEntity item, int count)
        {
            if (items.Count <= slotIndex)
            {
                for (int i = items.Count; i <= slotIndex; i++)
                {
                    items.Add(new Items
                    {
                        item = null,
                        count = 0
                    });
                }
            }
            items[slotIndex].count = count;
            items[slotIndex].item = item;
        }

        public int Remove(RPGEntity item, int count)
        {
            int ret = 0;

            for (int i = 0; i < count; i++)
            {
                if (Remove(item)) ret++;
            }

            return ret;
        }

        public bool Remove(RPGEntity item)
        {
            int slotIndex = FindItem(item);
            if (slotIndex == -1)
            {
                return false;
            }

            return RemoveBySlot(slotIndex, 1);
        }

        public bool RemoveBySlot(int slotIndex, int count)
        {
            var item = items[slotIndex].item;
            if (item == null) return false;

            items[slotIndex].count -= count;

            if (items[slotIndex].count <= 0)
            {
                items[slotIndex].item = null;
                items[slotIndex].count = 0;
            }

            onChange?.Invoke(false, item, slotIndex);

            return true;
        }


        public (RPGEntity item, int count) GetSlotContent(int slot)
        {
            if ((items == null) || (items.Count <= slot)) return (null, 0);

            return (items[slot].item, items[slot].count);
        }

        int FindItem(RPGEntity item)
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

        int GetSlot(RPGEntity item)
        {
            if (items == null) items = new();

            if (item.item.isStackable)
            {
                // Find a stack
                for (int i = 0; i < items.Count; i++)
                {
                    if ((items[i].item == item) && ((items[i].count < item.item.maxStack) || (item.item.maxStack == 0)))
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

        public bool HasItem(RPGEntity item)
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

        public int GetItemCount(RPGEntity item)
        {
            if (items == null) return 0;

            int count = 0;
            foreach (var i in items)
            {
                if (i.item == item) count += i.count;
            }

            return count;
        }

        public IEnumerator<(int slot, RPGEntity item, int count)> GetEnumerator()
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
