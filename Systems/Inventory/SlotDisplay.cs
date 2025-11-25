using NaughtyAttributes;
using System;
using TMPro;
using UC.RPG;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEditor.Progress;

namespace UC
{
    public class SlotDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        class SlotDisplayGrabData : CursorManager.ICursorGrabData
        {
            public SlotDisplay source;
            public Item item;
            public int  count;
        }

        public enum Source { RPGInventory, Inventory, RPGEquipment, Equipment };
        public enum DragMode { None, Click, Drag };

        [SerializeField]
        private Source          source;
        [SerializeField]
        private Hypertag        sourceTag;
        [SerializeField, ShowIf(nameof(isEquipped))]
        private Hypertag        equipmentSlot;
        [SerializeField, ShowIf(nameof(isInventory))]
        private int             slotIndex;
        [SerializeField]
        private Image           itemImage;
        [SerializeField]
        private TextMeshProUGUI itemText;
        [SerializeField]
        private bool            enableTooltip = false;
        [SerializeField, ShowIf(nameof(enableTooltip))]
        private Color           highlightColor = Color.yellow;
        [SerializeField, ShowIf(nameof(enableTooltip))]
        private float           highlightWidth = 1;
        [SerializeField]
        private DragMode        dragMode = DragMode.None;

        Inventory           inventory;
        Equipment           equipment;
        string              baseText;
        UIImageEffect       uiEffect;
        TooltipPanel        tooltip;
        Item                currentItem;
        int                 currentCount;
        InventoryInstance   inventoryInstance;
        EquipmentInstance   equipmentInstance;

        bool isEquipped() => source == Source.RPGEquipment || source == Source.Equipment;
        bool isInventory() => source == Source.RPGInventory || source == Source.Inventory;

        private void Start()
        {
            if (itemText)
            {
                baseText = itemText.text;
                if (string.IsNullOrEmpty(baseText)) baseText = "x{0}";
            }

            UpdateSlotUI();
        }

        public void UpdateSlotUI()
        {
            var prevItem = currentItem;
            (currentItem, currentCount) = GetItem();

            if ((currentItem != prevItem) && (tooltip))
            {
                tooltip.Remove();
                tooltip = null;
            }
            
            if (currentItem == null)
            {
                if (itemImage) itemImage.enabled = false;
                if (itemText) itemText.enabled = false;
            }
            else
            {
                if (itemImage)
                {
                    itemImage.enabled = true;
                    itemImage.sprite = currentItem.displaySprite;
                    itemImage.color = currentItem.displaySpriteColor;
                }

                if (itemText)
                {
                    itemText.enabled = (currentCount > 1);
                    itemText.text = string.Format(baseText, currentCount);
                }
            }
        }

        private (Item item, int count) GetItem()
        {
            switch (source)
            {
                case Source.RPGInventory:
                    {
                        if (inventoryInstance == null)
                        {
                            var tmp = sourceTag.FindFirst<UnityRPGEntity>();

                            if (tmp == null)
                                return (null, 0);

                            if (tmp.rpgEntity == null)
                                return (null, 0);

                            inventoryInstance = tmp.GetInventory();
                            if (inventoryInstance == null) return (null, 0);

                            inventoryInstance.onChange += SlotDisplay_onChange;
                        }
                        return inventoryInstance.GetSlotContent(slotIndex);
                    }
                case Source.Inventory:
                    {
                        if (inventory == null)
                        {
                            inventory = sourceTag.FindFirst<Inventory>();

                            if (inventory == null)
                                return (null, 0);

                            inventory.onChange += SlotDisplay_onChange;
                        }
                        return inventory.GetSlotContent(slotIndex);
                    }
                case Source.RPGEquipment:
                    {
                        if (equipmentInstance == null)
                        {
                            var tmp = sourceTag.FindFirst<UnityRPGEntity>();

                            if (tmp == null)
                                return (null, 0);

                            if (tmp.rpgEntity == null)
                                return (null, 0);

                            equipmentInstance = tmp.GetEquipment();
                            if (equipmentInstance == null) return (null, 0);

                            equipmentInstance.onChange += SlotDisplay_onChange;
                        }
                        var item = equipmentInstance.GetItem(equipmentSlot);
                        return (item, item != null ? 1 : 0);
                    }
                case Source.Equipment:
                    {
                        if (equipment == null)
                        {
                            equipment = sourceTag.FindFirst<Equipment>();

                            if (equipment == null)
                                return (null, 0);

                            equipment.onChange += SlotDisplay_onChange;
                        }
                        var item = equipment.GetItem(equipmentSlot);
                        return (item, item != null ? 1 : 0);
                    }
            }
            return (null, 0);
        }

        private void SlotDisplay_onChange(bool equip, Hypertag slot, Item item)
        {
            if (slot == equipmentSlot)
            {
                UpdateSlotUI();
            }
        }

        private void SlotDisplay_onChange(bool add, Item item, int slot)
        {
            if (slot == slotIndex)
            {
                UpdateSlotUI();
            }   
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enableTooltip) return;
            if (currentItem == null) return;

            if (uiEffect == null) uiEffect = itemImage.GetComponent<UIImageEffect>();
            uiEffect?.SetOutline(highlightWidth, highlightColor);

            if (tooltip == null)
            {
                tooltip = TooltipManager.CreateTooltip() as TooltipPanel;
            }
            tooltip?.Set(currentItem);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!enableTooltip) return;

            if (uiEffect == null) uiEffect = itemImage.GetComponent<UIImageEffect>();
            uiEffect?.SetOutline(0.0f, highlightColor);

            if (tooltip)
            {
                tooltip.Remove();
                tooltip = null;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragMode != DragMode.Click) return;

            // Has the player any item on the cursor?
            if (CursorManager.instance.cursorGrabData == null)
            {
                var item = GetItem();
                if (item.item == null)
                {
                    // Nothing to grab
                    return;
                }

                // Grab the item
                GrabItem();
            }
            else
            {
                var grabData = CursorManager.instance.cursorGrabData as SlotDisplayGrabData;
                if (grabData != null)
                {
                    // Data created by a slot
                    CursorManager.instance.SetCursor(true);
                    CursorManager.instance.AttachToCursor(null, Color.white);

                    CursorManager.instance.cursorGrabData = null;

                    // Grab item on slot, if there is something there
                    var item = GetItem();
                    if (item.item != null)
                    {
                        // Grab the item
                        GrabItem();
                    }

                    // Add whatever is on the cursor to the slot
                    switch (source)
                    {
                        case Source.RPGInventory:
                            inventoryInstance.SetOnSlot(slotIndex, grabData.item, grabData.count);
                            break;
                        case Source.Inventory:
                            inventory.SetOnSlot(slotIndex, grabData.item, grabData.count);
                            break;
                        case Source.RPGEquipment:
                            equipmentInstance.Equip(equipmentSlot, grabData.item, Time.time);
                            break;
                        case Source.Equipment:
                            equipment.Equip(equipmentSlot, grabData.item);
                            break;
                        default:
                            break;
                    }

                    UpdateSlotUI();
                    // This gets updated because we're on top of this item, so we can consider it as being on top
                    OnPointerEnter(null);
                }
            }

        }

        void GrabItem()
        {
            var item = GetItem();
            CursorManager.instance.cursorGrabData = new SlotDisplayGrabData()
            {
                source = this,
                item = item.item,
                count = item.count
            };
            CursorManager.instance.SetCursor(false);
            CursorManager.instance.AttachToCursor(item.item.displaySprite, item.item.displaySpriteColor);

            // Remove item from slot, it's on the mouse
            switch (source)
            {
                case Source.RPGInventory:
                    inventoryInstance.RemoveBySlot(slotIndex, item.count);
                    break;
                case Source.Inventory:
                    inventory.RemoveBySlot(slotIndex, item.count);
                    break;
                case Source.RPGEquipment:
                    equipmentInstance.Unequip(equipmentSlot);
                    break;
                case Source.Equipment:
                    equipment.Unequip(equipmentSlot);
                    break;
            }
            UpdateSlotUI();
            // This gets updated because we're on top of this item, so we can consider it as being on top
            OnPointerEnter(null);
        }
    }
}