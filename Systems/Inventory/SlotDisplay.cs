using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using TMPro;
using UC.RPG;
using UnityEngine.EventSystems;

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

        UnityRPGEntity  rpgEntity;
        Inventory       inventory;
        Equipment       equipment;
        string          baseText;
        UIImageEffect   uiEffect;
        TooltipPanel    tooltip;
        Item            currentItem;
        int             currentCount;

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
                        if (rpgEntity == null)
                        {
                            rpgEntity = sourceTag.FindFirst<UnityRPGEntity>();

                            if (rpgEntity == null)
                                return (null, 0);
                        }
                        if (rpgEntity.rpgEntity == null)
                            return (null, 0);
                        var inventory = rpgEntity.rpgEntity.inventory;
                        if (inventory == null) return (null, 0);
                        return inventory.GetSlotContent(slotIndex);
                    }
                case Source.Inventory:
                    {
                        if (inventory == null)
                        {
                            inventory = sourceTag.FindFirst<Inventory>();

                            if (inventory == null)
                                return (null, 0);
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
                        }
                        var item = equipment.GetItem(equipmentSlot);
                        return (item, item != null ? 1 : 0);
                    }
            }
            return (null, 0);
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