using NaughtyAttributes;
using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Inventory Module")]
    public class RPGInventoryModule : SOModule
    {
        [Serializable]
        public struct EquipmentSlot
        {
            public Hypertag slot;
            public Item     item;
        }
         
        [SerializeField, RequireModule(typeof(RPGItemWeapon))]
        public Item             unnarmedWeapon;
        [SerializeField]
        private bool            _hasInventory = false;
        [SerializeField, Tooltip("Max inventory slots, -1 to unlimited."), ShowIf(nameof(_hasInventory))]
        public int              inventorySize = -1;
        [SerializeField, ShowIf(nameof(_hasInventory))]
        public Item[]           defaultInventory;
        [SerializeField]
        private bool            _hasEquipment = false;
        [SerializeField, ShowIf(nameof(_hasEquipment))]
        public Hypertag[]       availableEquipmentSlots;
        [SerializeField, ShowIf(nameof(_hasEquipment))]
        public EquipmentSlot[]  defaultEquipment;

        public bool hasInventory => _hasInventory;
        public bool hasEquipment => _hasEquipment;
    }
}
