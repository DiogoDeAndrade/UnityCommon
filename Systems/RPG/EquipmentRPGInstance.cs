using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{
    public class EquipmentRPGInstance
    {
        public delegate void OnChange(bool equip, Hypertag slot, RPGEntity item);
        public event OnChange onChange;

        [SerializeField]
        private List<Hypertag>  availableSlots;

        private class EquipItem
        {
            public RPGEntity    item;
            public float        lastChange;
        }

        private Dictionary<Hypertag, EquipItem>  items = new();

        public void AddSlot(Hypertag slot)
        {
            if (HasSlot(slot)) return;

            if (availableSlots == null) availableSlots = new();
            availableSlots.Add(slot);
        }

        public bool HasSlot(Hypertag slot)
        {
            if (availableSlots == null) return false;
            return (availableSlots.IndexOf(slot) != -1);
        }

        public RPGEntity GetItem(Hypertag slot)
        {
            if (items.TryGetValue(slot, out var item))
            {
                return item.item;
            }

            return null;
        }
        public float GetLastChange(Hypertag slot)
        {
            if (items.TryGetValue(slot, out var item))
            {
                return item.lastChange;
            }

            return 0.0f;
        }

        public void Equip(Hypertag slot, RPGEntity itemToEquip, float time = 0.0f)
        {
            items[slot] = new()
            {
                item = itemToEquip,
                lastChange = time
            };
            onChange?.Invoke(true, slot, itemToEquip);
        }

        public bool IsEquipped(RPGEntity item)
        {
            foreach (var equippedItem in items)
            {
                if (equippedItem.Value.item == item) return true;
            }

            return false;
        }

        public bool IsEquipped(Hypertag slot, RPGEntity item)
        {
            if (items.ContainsKey(slot))
            {
                return items[slot].item == item;
            }

            return false;
        }

        public void Unequip(Hypertag slot)
        {
            var prevItem = GetItem(slot);
            items[slot] = new()
            {
                item = null,
                lastChange = Time.time
            };
            onChange?.Invoke(false, slot, prevItem);
        }

        public void Unequip(RPGEntity item)
        {
            foreach (var i in items)
            {
                if (i.Value.item == item)
                {
                    Unequip(i.Key);
                    return;
                }
            }
        }

        public List<Hypertag> GetAvailableSlots()
        {
            return availableSlots;
        }
    }
}
