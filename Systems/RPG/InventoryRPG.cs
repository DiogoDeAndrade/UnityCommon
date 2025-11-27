using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC.RPG
{
    // Same as Inventory , but for RPGs, which takes RPGEntity as items, and not Item itself
    // Basically, it's an inventory that can store items with state
    public class InventoryRPG : MonoBehaviour, IEnumerable<(int slot, RPGEntity item, int count)>
    {
        public delegate void OnChange(bool add, RPGEntity item, int slot);
        public event OnChange onChange;

        [SerializeField]
        private bool            limited = false;
        [SerializeField, ShowIf(nameof(limited))]
        private int             maxSlots = 9;
        [SerializeField]
        private bool            enableInput = false;
        [SerializeField, ShowIf(nameof(enableInput))]
        private PlayerInput     playerInput;
        [SerializeField, ShowIf(nameof(enableInput)), InputPlayer(nameof(playerInput)), InputButton]
        private InputControl    inventoryButton;

                    InventoryDisplay        inventoryDisplay;
                    InventoryRPGInstance    inventoryInstance;
        protected   bool                    _fromInstance;

        public InventoryRPGInstance instance
        {
            get
            {
                if (inventoryInstance == null)
                {
                    inventoryInstance = new(limited, maxSlots);
                    inventoryInstance.onChange += InventoryInstance_onChange;
                    _fromInstance = false;
                }
                return inventoryInstance;
            }
            set
            {
                if (inventoryInstance != null) inventoryInstance.onChange -= InventoryInstance_onChange;

                inventoryInstance = value;
                inventoryInstance.onChange += InventoryInstance_onChange;
                _fromInstance = true;
            }
        }

        private void InventoryInstance_onChange(bool add, RPGEntity item, int slot)
        {
            onChange?.Invoke(add, item, slot);
        }

        void Start()
        {
            if (enableInput)
            {
                inventoryDisplay = FindFirstObjectByType<InventoryDisplay>();
                inventoryButton.playerInput = playerInput;
            }
        }

        void Update()
        {
            if (enableInput)
            {
                if (inventoryButton.IsDown())
                {
                    inventoryDisplay.Toggle();
                }
            }
        }

        public int Add(RPGEntity item, int quantity) => inventoryInstance.Add(item, quantity);
        public bool Add(RPGEntity item) => inventoryInstance.Add(item);
        public int Remove(RPGEntity item, int count) => inventoryInstance.Remove(item, count);
        public bool Remove(RPGEntity item) => inventoryInstance.Remove(item);
        public (RPGEntity, int) GetSlotContent(int slot) => inventoryInstance.GetSlotContent(slot);        
        public bool HasItem(RPGEntity item) => inventoryInstance.HasItem(item);
        public bool HasItems() => inventoryInstance.HasItems();
        public int GetItemCount(RPGEntity item) => inventoryInstance.GetItemCount(item);
        public IEnumerator<(int slot, RPGEntity item, int count)> GetEnumerator() => inventoryInstance.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool RemoveBySlot(int slotIndex, int count) => inventoryInstance.RemoveBySlot(slotIndex, count);

        public void SetOnSlot(int slotIndex, RPGEntity item, int count) => inventoryInstance.SetOnSlot(slotIndex, item, count);
    }
}
