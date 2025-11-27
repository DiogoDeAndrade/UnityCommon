using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UC.RPG;
using UnityEngine;

namespace UC
{
    // Same as Equipment, but for RPGs, which takes RPGEntity as items, and not Item itself
    // Basically, it's an equipment manager that can store items with state

    public class EquipmentRPG : MonoBehaviour
    {
        public delegate void OnChange(bool equip, Hypertag slot, RPGEntity item);
        public event OnChange onChange;

        [SerializeField, Tooltip("Link an inventory if you want equipped objects to still have to stay in inventory, instead of being removed from it and equipped.")]
        private InventoryRPG    linkedInventory;
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

        EquipmentRPGInstance    equipmentInstance;
        protected bool          _fromInstance;

        public EquipmentRPGInstance instance
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

        private void InventoryInstance_onChange(bool equip, Hypertag slot, RPGEntity item)
        {
            onChange?.Invoke(equip, slot, item);
        }

        public bool HasSlot(Hypertag slot) => equipmentInstance.HasSlot(slot);
        public RPGEntity GetItem(Hypertag slot) => equipmentInstance.GetItem(slot);
        public float GetLastChange(Hypertag slot) => equipmentInstance.GetLastChange(slot);
        public void Equip(Hypertag slot, RPGEntity itemToEquip)
        {
            equipmentInstance.Equip(slot, itemToEquip, Time.time);
            if (combatTextEnable)
                CombatTextManager.SpawnText(gameObject, $"Equipped {itemToEquip.item.displayName}", combatTextEquippedItemColor, combatTextEquippedItemColor, combatTextDuration);
        }
        public bool IsEquipped(RPGEntity item) => equipmentInstance.IsEquipped(item);
        public bool IsEquipped(Hypertag slot, RPGEntity item) => equipmentInstance.IsEquipped(slot, item);
        public void Unequip(Hypertag slot)
        {
            var prevItem = GetItem(slot);
            equipmentInstance.Unequip(slot);
            if ((combatTextEnable) && (prevItem != null))
            {
                CombatTextManager.SpawnText(gameObject, $"Unequipped {prevItem.item.displayName}", combatTextUnequippedItemColor, combatTextUnequippedItemColor, combatTextDuration);
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
