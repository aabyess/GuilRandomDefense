using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemInventory : MonoBehaviour
{
    List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<ItemData> Items => items;

    public event Action OnInventoryChanged;

    public void Add(ItemData item)
    {
        items.Add(item);
        OnInventoryChanged?.Invoke();
    }

    public bool Remove(ItemData item)
    {
        bool removed = items.Remove(item);
        if (removed)
        {
            OnInventoryChanged?.Invoke();
        }
        return removed;
    }
}
