using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializedHashSet<T> : IEnumerable<T>
{
    [SerializeField]
    private List<T>     _itemList = new();

    private HashSet<T>  _itemSet;

    public SerializedHashSet()
    {
        _itemList = new();
    }

    public SerializedHashSet(SerializedHashSet<T> src)
    {
        _itemList = new(src._itemList);
        if (src._itemSet != null) _itemSet = new(src._itemSet);
    }

    private HashSet<T>  items
    {
        get
        {
            if (_itemSet == null)
            {
                if (_itemList == null) _itemSet = new();
                else _itemSet = new(_itemList);
            }
            return _itemSet;
        }
    }

    public void Add(T value)
    {
        if (items.Contains(value)) return;
        _itemList.Add(value);
        _itemSet.Add(value);
    }

    public void Remove(T value)
    {
        _itemList.Remove(value);
        _itemSet?.Remove(value);
    }

    public int Count => _itemList.Count;

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var i in items)
        { 
            yield return i;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
