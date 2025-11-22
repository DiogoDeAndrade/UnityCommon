using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UC.RPG;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    public class Inventory : MonoBehaviour, IEnumerable<(int slot, Item item, int count)>
    {
        public delegate void OnChange(bool add, Item item, int slot);
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

                    InventoryDisplay    inventoryDisplay;
                    InventoryInstance   inventoryInstance;
        protected   bool                _fromInstance;

        public InventoryInstance instance
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

        private void InventoryInstance_onChange(bool add, Item item, int slot)
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

        public int Add(Item item, int quantity) => inventoryInstance.Add(item, quantity);
        public bool Add(Item item) => inventoryInstance.Add(item);
        public int Remove(Item item, int count) => inventoryInstance.Remove(item, count);
        public bool Remove(Item item) => inventoryInstance.Remove(item);
        public (Item, int) GetSlotContent(int slot) => inventoryInstance.GetSlotContent(slot);        
        public bool HasItem(Item item) => inventoryInstance.HasItem(item);
        public bool HasItems() => inventoryInstance.HasItems();
        public int GetItemCount(Item item) => inventoryInstance.GetItemCount(item);
        public IEnumerator<(int slot, Item item, int count)> GetEnumerator() => inventoryInstance.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
