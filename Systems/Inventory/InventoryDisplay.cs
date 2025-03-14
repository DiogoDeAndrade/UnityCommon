using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class InventoryDisplay : MonoBehaviour
{
    [SerializeField] private RectTransform itemContainer;

    Inventory       inventory;
    ItemDisplay[]   itemDisplays;
    CanvasGroup     canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        itemDisplays = itemContainer.GetComponentsInChildren<ItemDisplay>(true);
        ClearDisplay();
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
            ClearDisplay();
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

        if (inventory.HasItems())
        {
            canvasGroup.FadeIn(0.5f);
        }
        else
        {
            canvasGroup.FadeOut(0.5f);
        }
    }

    private void ClearDisplay()
    {
        foreach (var itemDisplay in itemDisplays)
        {
            itemDisplay.Set(null, 0);
        }

        if (inventory)
        {
            if (inventory.HasItems())
            {
                canvasGroup.FadeIn(0.5f);
            }
            else
            {
                canvasGroup.FadeOut(0.5f);
            }
        }
        else
        {
            canvasGroup.FadeOut(0.5f);
        }
    }
}
