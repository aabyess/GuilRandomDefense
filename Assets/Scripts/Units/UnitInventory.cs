using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitInventory : MonoBehaviour
{
    List<UnitData> units = new List<UnitData>();

    public IReadOnlyList<UnitData> Units => units;

    public event Action OnInventoryChanged;

    public void Add(UnitData unit)
    {
        units.Add(unit);
        OnInventoryChanged?.Invoke();
    }

    public bool Remove(UnitData unit)
    {
        bool removed = units.Remove(unit);
        if (removed)
        {
            OnInventoryChanged?.Invoke();
        }
        return removed;
    }
}
