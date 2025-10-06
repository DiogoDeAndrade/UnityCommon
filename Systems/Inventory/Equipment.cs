using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class Equipment : MonoBehaviour
    {
        public delegate void OnChange(bool equip, Hypertag slot, Item item);
        public event OnChange onChange;

        [SerializeField]
        private Inventory       linkedInventory;
        [SerializeField]
        private List<Hypertag>  availableSlots;
        [SerializeField]
        private bool            combatTextEnable;
        [SerializeField, ShowIf(nameof(combatTextEnable))]
        private float           combatTextDuration = 1.0f;
        [SerializeField, ShowIf(nameof(combatTextEnable))]
        private Color           combatTextEquippedItemColor = Color.yellow;
        [SerializeField, ShowIf(nameof(combatTextEnable))]
        private Color           combatTextUnequippedItemColor = Color.green;

        private class EquipItem
        {
            public Item     item;
            public float    lastChange;
        }

        private Dictionary<Hypertag, EquipItem>  items = new();

        public bool HasSlot(Hypertag slot)
        {
            return (availableSlots.IndexOf(slot) != -1);
        }

        public Item GetItem(Hypertag slot)
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

        public void Equip(Hypertag slot, Item itemToEquip)
        {
            items[slot] = new()
            {
                item = itemToEquip,
                lastChange = Time.time
            };
            onChange?.Invoke(true, slot, itemToEquip);
            if (combatTextEnable)
                CombatTextManager.SpawnText(gameObject, $"Equipped {itemToEquip.displayName}", combatTextEquippedItemColor, combatTextEquippedItemColor, combatTextDuration);
        }

        public bool IsEquipped(Item item)
        {
            foreach (var equippedItem in items)
            {
                if (equippedItem.Value.item == item) return true;
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
            onChange?.Invoke(false, slot, null);
            if ((combatTextEnable) && (prevItem))
            {
                CombatTextManager.SpawnText(gameObject, $"Unequipped {prevItem.displayName}", combatTextUnequippedItemColor, combatTextUnequippedItemColor, combatTextDuration);
            }
        }

        public List<Hypertag> GetAvailableSlots()
        {
            return availableSlots;
        }

        private void Update()
        {
            if (linkedInventory != null)
            {
                var slots = new List<Hypertag>(items.Keys); 

                foreach (var slot in slots)
                {
                    if (items.TryGetValue(slot, out var equippedItem) && equippedItem != null)
                    {
                        if (!linkedInventory.HasItem(equippedItem.item))
                        {
                            Unequip(slot);
                        }
                    }
                }
            }
        }
    }
}
