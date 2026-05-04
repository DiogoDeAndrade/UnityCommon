using UC;
using UnityEngine;
using System.Collections.Generic;

public class UIList : BaseUIControl
{
    public delegate void OnSelectItem(UIListItem item);
    public event OnSelectItem onItemSelect;

    [SerializeField] private RectTransform  containerTransform;

    private UIListProvider provider;
    private readonly List<UIListItem> items = new List<UIListItem>();

    public interface UIListProvider
    {
        int itemCount { get; }

        void        Refresh();
        UIListItem  CreateItem(int index, RectTransform container);
    }

    protected override void Start()
    {
        base.Start();        

        provider = GetComponent<UIListProvider>();
        if (provider == null)
        {
            Debug.LogError($"UIList on '{name}' requires a component implementing UIListProvider.", this);
            return;
        }

        Refresh();
    }

    public override void NotifyEnable()
    {
        base.NotifyEnable();
        Refresh();
    }

    public void Refresh()
    {
        Clear();
        if (provider == null) return;

        provider.Refresh();

        int count = provider.itemCount;
        for (int i = 0; i < count; i++)
        {
            UIListItem item = provider.CreateItem(i, containerTransform);
            if (item == null) continue;

            item.index = i;
            item.ownerList = this;
            items.Add(item);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                Destroy(items[i].gameObject);
        }
        items.Clear();
    }

    public void SelectItem(UIListItem item)
    {
        onItemSelect?.Invoke(item);
    }
}
