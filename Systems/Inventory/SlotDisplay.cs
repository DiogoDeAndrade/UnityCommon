using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using TMPro;
using UC.RPG;

namespace UC
{
    public class SlotDisplay : MonoBehaviour
    {
        public enum Source { RPGInventory, Inventory, RPGEquipment, Equipment };

        [SerializeField]
        private Source source;
        [SerializeField]
        private Hypertag sourceTag;
        [SerializeField, ShowIf(nameof(isEquipped))]
        private Hypertag equipmentSlot;
        [SerializeField, ShowIf(nameof(isInventory))]
        private int slotIndex;
        [SerializeField]
        private Image itemImage;
        [SerializeField]
        private TextMeshProUGUI itemText;

        UnityRPGEntity rpgEntity;
        Inventory inventory;
        Equipment equipment;
        string baseText;

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
            var item = GetItem();
            if (item.item == null)
            {
                if (itemImage) itemImage.enabled = false;
                if (itemText) itemText.enabled = false;
            }
            else
            {
                if (itemImage)
                {
                    itemImage.enabled = true;
                    itemImage.sprite = item.item.displaySprite;
                    itemImage.color = item.item.displaySpriteColor;
                }

                if (itemText)
                {
                    itemText.enabled = (item.count > 1);
                    itemText.text = string.Format(baseText, item.count);
                }
            }
        }

        private (UC.Item item, int count) GetItem()
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
    }
}