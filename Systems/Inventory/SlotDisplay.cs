using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using TMPro;
using UC.RPG;
using UnityEngine.EventSystems;
using System;

namespace UC
{
    public class SlotDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Source { RPGInventory, Inventory, RPGEquipment, Equipment };

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

        UnityRPGEntity      rpgEntity;
        Inventory           inventory;
        Equipment           equipment;
        string              baseText;
        UIImageEffect       uiEffect;
        TooltipPanel        tooltip;
        Item                currentItem;
        int                 currentCount;
        InventoryInstance   inventoryInstance;

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
            (currentItem, currentCount) = GetItem();
            
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
                        throw new System.NotImplementedException();
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

            if ((tooltip) && (tooltip.CurrentItem == currentItem))
            {
                tooltip.Remove();
                tooltip = null;
            }
        }
    }
}