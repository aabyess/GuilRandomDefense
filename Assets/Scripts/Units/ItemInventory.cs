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

    /// <summary>MP 클라: 호스트가 복제해 준 보유 목록으로 통째 교체한다(이벤트는 바뀌었을 때 한 번).</summary>
    public void ApplyReplicated(IList<ItemData> held)
    {
        bool same = held.Count == items.Count;
        for (int i = 0; same && i < held.Count; i++) if (held[i] != items[i]) same = false;
        if (same) return;
        items.Clear();
        items.AddRange(held);
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
