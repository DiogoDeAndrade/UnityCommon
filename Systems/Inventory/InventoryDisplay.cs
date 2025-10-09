using NaughtyAttributes;
using UnityEngine;
using static UC.ItemDisplay;

namespace UC
{

    public class InventoryDisplay : MonoBehaviour
    {
        public delegate void OnInventoryOpen(bool open);
        public event OnInventoryOpen onInventoryToggle;
        public delegate void OnInventorySelect(Item item);
        public event OnInventorySelect onInventorySelect;

        private enum OpenState { Unknown, Open, Close };

        [SerializeField] 
        private bool           isAlwaysOpen = true;
        [SerializeField, HideIf(nameof(isAlwaysOpen))] 
        private bool           startOpen;
        [SerializeField, ShowIf(nameof(isAlwaysOpen))] 
        private bool           disappearIfEmpty = true;
        [SerializeField]
        private float          fadeTime = 0.5f;
        [SerializeField] 
        private RectTransform  itemContainer;

        Inventory       inventory;
        ItemDisplay[]   itemDisplays;
        CanvasGroup     canvasGroup;
        OpenState       openState = OpenState.Unknown;

        void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            itemDisplays = itemContainer.GetComponentsInChildren<ItemDisplay>(true);
            foreach (var id in itemDisplays)
            {
                id.onClick += Id_onClick;
            }
            ClearDisplay();
            if (!isAlwaysOpen)
            {
                if (startOpen) Open(0.1f);
                else Close(0.1f);
            }
        }

        private void Id_onClick(ItemDisplay itemDisplay)
        {
            for (int i = 0; i < itemDisplays.Length; i++)
            {
                if (itemDisplays[i] == itemDisplay)
                {
                    (var item, var count) = inventory.GetSlotContent(i);

                    onInventorySelect?.Invoke(item);
                }
            }
        }

        private void OnDestroy()
        {
            SetInventory(null);
        }

        public void SetInventory(Inventory inventory)
        {
            if (this.inventory != null)
            {
                this.inventory.onChange -= UpdateInventory;
            }

            this.inventory = inventory;
            if (this.inventory)
            {
                this.inventory.onChange += UpdateInventory;

                UpdateInventory(true, null, -1);
            }
            else
            {
                if (isAlwaysOpen)
                {
                    if (disappearIfEmpty) Close(fadeTime);
                }
            }
        }

        private void UpdateInventory(bool add, Item modifiedItem, int slot)
        {
            if (inventory == null) return;

            for (int i = 0; i < itemDisplays.Length; i++)
            {
                (var item, var count) = inventory.GetSlotContent(i);

                itemDisplays[i].Set(item, count);
            }

            if (isAlwaysOpen)
            {
                if (inventory.HasItems())
                {
                    Open();
                }
                else
                {
                    if (disappearIfEmpty)
                    {
                        Close();
                    }
                    else
                    {
                        Open();
                    }
                }
            }
        }

        private void ClearDisplay()
        {
            foreach (var itemDisplay in itemDisplays)
            {
                itemDisplay.Set(null, 0);
            }

            if (isAlwaysOpen)
            {
                if (inventory)
                {
                    if (inventory.HasItems())
                    {
                        Open();
                    }
                    else
                    {
                        if (!disappearIfEmpty)
                            Close();
                    }
                }
                else if (disappearIfEmpty)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open(float time = 0.0f)
        {
            if (openState == OpenState.Open) return;

            canvasGroup.FadeIn((time == 0.0f) ? (fadeTime) : (time));
            openState = OpenState.Open;
            onInventoryToggle?.Invoke(true);
        }
        public void Close(float time = 0.0f)
        {
            if (openState == OpenState.Close) return;

            canvasGroup.FadeOut((time == 0.0f) ? (fadeTime) : (time));
            openState = OpenState.Close;
            onInventoryToggle?.Invoke(false);
        }

        public void Toggle(float time = 0.0f)
        {
            switch (openState)
            {
                case OpenState.Open:
                    Close(time);
                    break;
            case OpenState.Unknown:
            case OpenState.Close:
                    Open(time);
                    break;
                default:
                    break;
            }
        }
    }
}