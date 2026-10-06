using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemInventory : MonoBehaviour
{
    List<ItemData> items = new List<ItemData>();

    public IReadOnlyList<ItemData> Items => items;

    /// <summary>종이비행기 유물(초월 이재윤)을 이미 썼나 — 한 판에 한 번만(호스트 상태). 아이템은 인벤토리에 남는다.</summary>
    public bool PaperPlaneUsed;

    public event Action OnInventoryChanged;

    /// <summary>원작 영웅 인벤토리 6칸(사장님 확정 10-03) — 한 플레이어가 들 수 있는 아이템 수 상한.</summary>
    public const int MaxItems = 6;

    public bool IsFull => items.Count >= MaxItems;

    /// <summary>넣었으면 true. 6칸이 차 있으면 거절(호출부가 알린다).</summary>
    public bool Add(ItemData item)
    {
        if (items.Count >= MaxItems) return false;
        items.Add(item);
        OnInventoryChanged?.Invoke();
        return true;
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
