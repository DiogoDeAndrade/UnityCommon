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
        _itemSet.Add(value);
        _itemList.Add(value);
    }

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
