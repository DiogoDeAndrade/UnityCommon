using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class InventoryDisplay : MonoBehaviour
{    
    Inventory     inventory;
    ItemDisplay[] itemDisplays;

    void Awake()
    {
        itemDisplays = GetComponentsInChildren<ItemDisplay>(true);
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
    }

    private void ClearDisplay()
    {
        foreach (var itemDisplay in itemDisplays)
        {
            itemDisplay.Set(null, 0);
        }
    }
}
