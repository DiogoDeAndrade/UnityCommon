using NaughtyAttributes;
using TMPro;
using UC.RPG;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UC
{
    public class SlotDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        class SlotDisplayGrabData : CursorManager.ICursorGrabData
        {
            public SlotDisplay  source;
            public RPGEntity    entity;
            public int          count;

            public bool Return()
            {
                source.Return(this);
                return true;
            }
        }

        struct SlotContent
        {
            public RPGEntity    item;
            public int          count;

            public bool IsEmpty => ((item == null) || (count <= 0));
        }

        public enum Source { RPGInventory, Inventory, RPGEquipment, Equipment }
        public enum DragMode { None, Click }

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

        InventoryRPG            inventory;
        EquipmentRPG            equipment;
        string                  baseText;
        UIImageEffect           uiEffect;
        TooltipPanel            tooltip;
        RPGEntity               currentItem;
        int                     currentCount;
        RPGEntity               _entity;
        InventoryRPGInstance    inventoryInstance;
        EquipmentRPGInstance    equipmentInstance;
        bool isEquipped => source == Source.RPGEquipment || source == Source.Equipment;
        bool isInventory => source == Source.RPGInventory || source == Source.Inventory;

        RPGEntity entity
        {
            get
            {
                if (_entity == null)
                {
                    switch (source)
                    {
                        case Source.RPGInventory:
                        case Source.RPGEquipment:
                            var tmp = sourceTag.FindFirst<UnityRPGEntity>();
                            if (tmp) _entity = tmp.rpgEntity;
                            break;
                    }
                }

                return _entity;
            }
        }

        SlotDisplayGrabData CurrentGrab => CursorManager.instance.cursorGrabData as SlotDisplayGrabData;

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
            var slot = GetSlotContent();

            currentItem = slot.item;
            currentCount = slot.count;

            if ((currentItem != prevItem) && tooltip)
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
                    itemImage.sprite = currentItem.item.displaySprite;
                    itemImage.color = currentItem.item.displaySpriteColor;
                }

                if (itemText)
                {
                    itemText.enabled = (currentCount > 1);
                    itemText.text = string.Format(baseText, currentCount);
                }
            }
        }

        private (RPGEntity item, int count) GetItemRaw()
        {
            switch (source)
            {
                case Source.RPGInventory:
                    {
                        if (inventoryInstance == null)
                        {
                            var tmp = sourceTag.FindFirst<UnityRPGEntity>();
                            if (tmp == null || tmp.rpgEntity == null)
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
                            inventory = sourceTag.FindFirst<InventoryRPG>();
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
                            if (tmp == null || tmp.rpgEntity == null)
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
                            equipment = sourceTag.FindFirst<EquipmentRPG>();
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

        private SlotContent GetSlotContent()
        {
            var (item, count) = GetItemRaw();
            return new SlotContent { item = item, count = count };
        }

        private void WriteSlot(RPGEntity item, int count)
        {
            // Make sure backing instances exist
            GetItemRaw();

            switch (source)
            {
                case Source.RPGInventory:
                    inventoryInstance.SetOnSlot(slotIndex, item, item == null ? 0 : count);
                    break;

                case Source.Inventory:
                    inventory.SetOnSlot(slotIndex, item, item == null ? 0 : count);
                    break;

                case Source.RPGEquipment:
                    if (item == null || count <= 0)
                        equipmentInstance.Unequip(equipmentSlot);
                    else
                        equipmentInstance.Equip(equipmentSlot, item, Time.time);
                    break;

                case Source.Equipment:
                    if (item == null || count <= 0)
                        equipment.Unequip(equipmentSlot);
                    else
                        equipment.Equip(equipmentSlot, item);
                    break;
            }
        }

        private int GetMaxStackForSlot(Item item)
        {
            if (item == null) return 0;

            // Equipment slots always behave as "single item" slots.
            if (isEquipped) return 1;

            if (!item.isStackable) return 1;

            return Mathf.Max(1, item.maxStack);
        }

        private void SlotDisplay_onChange(bool equip, Hypertag slot, RPGEntity item)
        {
            if (slot == equipmentSlot)
            {
                UpdateSlotUI();
            }
        }

        private void SlotDisplay_onChange(bool add, RPGEntity item, int slot)
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

        private void ClearCursor()
        {
            CursorManager.instance.cursorGrabData = null;
            CursorManager.instance.SetCursor(true);
            CursorManager.instance.AttachToCursor(null, Color.white);
        }

        private void ApplyCursorVisuals(SlotDisplayGrabData grab)
        {
            if (grab == null)
            {
                ClearCursor();
                return;
            }

            CursorManager.instance.cursorGrabData = grab;
            CursorManager.instance.SetCursor(false);

            var attachedObject = CursorManager.instance.AttachToCursor(grab.entity.item.displaySprite, grab.entity.item.displaySpriteColor);

            var txt = attachedObject.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                if (grab.count > 1)
                {
                    txt.text = $"x{grab.count}";
                    txt.enabled = true;
                }
                else
                {
                    txt.enabled = false;
                }
            }
        }

        private SlotDisplayGrabData CreateGrabFromSlot(SlotContent slot)
        {
            return new SlotDisplayGrabData
            {
                source = this,
                entity = slot.item,
                count = slot.count
            };
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (dragMode != DragMode.Click) return;

            var slot = GetSlotContent();
            var grab = CurrentGrab;

            if (grab == null)
            {
                HandleClickWithoutCursor(slot);
            }
            else
            {
                HandleClickWithCursor(slot, grab);
            }

            UpdateSlotUI();

            if (enableTooltip)
            {
                OnPointerEnter(null);
            }
        }

        private void HandleClickWithoutCursor(SlotContent slot)
        {
            if (slot.IsEmpty) return;

            var grab = CreateGrabFromSlot(slot);

            ApplyCursorVisuals(grab);
            WriteSlot(null, 0);
        }

        private void HandleClickWithCursor(SlotContent slot, SlotDisplayGrabData grab)
        {
            if (isEquipped)
            {
                var gear = grab.entity.item as Gear;
                if (gear == null) return;

                if (entity != null && !gear.CanEquip(equipmentSlot, entity))
                    return;
            }

            if (slot.IsEmpty)
            {
                PlaceIntoEmptySlot(grab);
                return;
            }

            if (slot.item != grab.entity)
            {
                SwapDifferentItems(slot, grab);
                return;
            }

            if (!grab.entity.item.isStackable)
            {
                SwapDifferentItems(slot, grab);
                return;
            }

            HandleStackableSameItem(slot, grab);
        }

        private void PlaceIntoEmptySlot(SlotDisplayGrabData grab)
        {
            int maxHere = GetMaxStackForSlot(grab.entity.item);
            int toPlace = Mathf.Min(maxHere, grab.count);

            WriteSlot(grab.entity, toPlace);

            grab.count -= toPlace;
            if (grab.count <= 0)
            {
                ClearCursor();
            }
            else
            {
                ApplyCursorVisuals(grab);
            }
        }

        private void SwapDifferentItems(SlotContent slot, SlotDisplayGrabData grab)
        {
            var oldItem = slot.item;
            var oldCount = slot.count;

            int maxHere = GetMaxStackForSlot(grab.entity.item);
            int toPlace = Mathf.Min(maxHere, grab.count);

            WriteSlot(grab.entity, toPlace);

            int leftover = grab.count - toPlace;

            grab.entity = oldItem;
            grab.count = oldCount + Mathf.Max(0, leftover);

            if (grab.entity == null || grab.count <= 0)
                ClearCursor();
            else
                ApplyCursorVisuals(grab);
        }

        private void HandleStackableSameItem(SlotContent slot, SlotDisplayGrabData grab)
        {
            int maxHere = GetMaxStackForSlot(grab.entity.item);
            int slotCount = slot.count;
            int cursorCount = grab.count;

            if (slotCount < maxHere)
            {
                int free = maxHere - slotCount;
                int toMove = Mathf.Min(free, cursorCount);

                slotCount += toMove;
                cursorCount -= toMove;

                WriteSlot(grab.entity, slotCount);

                grab.count = cursorCount;
                if (cursorCount <= 0)
                    ClearCursor();
                else
                    ApplyCursorVisuals(grab);
            }
            else
            {
                int tmp = slotCount;
                slotCount = cursorCount;
                cursorCount = tmp;

                WriteSlot(grab.entity, slotCount);

                grab.count = cursorCount;
                if (cursorCount <= 0)
                {
                    ClearCursor();
                }
                else
                {
                    ApplyCursorVisuals(grab);
                }
            }
        }

        private void Return(SlotDisplayGrabData grabData)
        {
            WriteSlot(grabData.entity, grabData.count);
            ClearCursor();
            UpdateSlotUI();
        }
    }
}
