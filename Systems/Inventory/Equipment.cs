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

        [SerializeField, Tooltip("Link an inventory if you want equipped objects to still have to stay in inventory, instead of being removed from it and equipped.")]
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

        EquipmentInstance   equipmentInstance;
        protected bool      _fromInstance;

        public EquipmentInstance instance
        {
            get
            {
                if (equipmentInstance == null)
                {
                    equipmentInstance = new();
                    equipmentInstance.onChange += InventoryInstance_onChange;
                    _fromInstance = false;
                }
                return equipmentInstance;
            }
            set
            {
                if (equipmentInstance != null) equipmentInstance.onChange -= InventoryInstance_onChange;

                equipmentInstance = value;
                equipmentInstance.onChange += InventoryInstance_onChange;
                availableSlots = equipmentInstance.GetAvailableSlots();
                _fromInstance = true;
            }
        }

        private void InventoryInstance_onChange(bool equip, Hypertag slot, Item item)
        {
            onChange?.Invoke(equip, slot, item);
        }

        public bool HasSlot(Hypertag slot) => equipmentInstance.HasSlot(slot);
        public Item GetItem(Hypertag slot) => equipmentInstance.GetItem(slot);
        public float GetLastChange(Hypertag slot) => equipmentInstance.GetLastChange(slot);
        public void Equip(Hypertag slot, Item itemToEquip)
        {
            equipmentInstance.Equip(slot, itemToEquip, Time.time);
            if (combatTextEnable)
                CombatTextManager.SpawnText(gameObject, $"Equipped {itemToEquip.displayName}", combatTextEquippedItemColor, combatTextEquippedItemColor, combatTextDuration);
        }
        public bool IsEquipped(Item item) => equipmentInstance.IsEquipped(item);
        public bool IsEquipped(Hypertag slot, Item item) => equipmentInstance.IsEquipped(slot, item);
        public void Unequip(Hypertag slot)
        {
            var prevItem = GetItem(slot);
            equipmentInstance.Unequip(slot);
            if ((combatTextEnable) && (prevItem))
            {
                CombatTextManager.SpawnText(gameObject, $"Unequipped {prevItem.displayName}", combatTextUnequippedItemColor, combatTextUnequippedItemColor, combatTextDuration);
            }
        }
        public List<Hypertag> GetAvailableSlots() => equipmentInstance.GetAvailableSlots();

        private void Update()
        {
            if (linkedInventory != null)
            {
                var slots = GetAvailableSlots();

                foreach (var slot in slots)
                {
                    var equippedItem = equipmentInstance.GetItem(slot);
                    if (equippedItem != null)
                    {
                        if (!linkedInventory.HasItem(equippedItem))
                        {
                            Unequip(slot);
                        }
                    }
                }
            }
        }
    }
}
