using System.Collections.Generic;
using UnityEngine;

public class CombineSystem : MonoBehaviour
{
    const int RequiredCount = 3;

    [SerializeField] UnitInventory inventory;
    [SerializeField] GachaTable gachaTable;

    UnitInventory Inventory => inventory != null ? inventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;

    public bool TryCombine(UnitData unit)
    {
        UnitInventory targetInventory = Inventory;
        if (targetInventory == null)
        {
            Debug.LogError("CombineSystem: inventory 참조가 비어있습니다 (인스펙터 미할당, PlayerContext.Local도 없음).", this);
            return false;
        }

        if (unit == null) return false;

        if (unit.grade == UnitGrade.Legendary)
        {
            Debug.Log($"{unit.unitName}: 이미 최고 등급이라 더 이상 조합할 수 없습니다.");
            return false;
        }

        if (CountOf(targetInventory, unit) < RequiredCount) return false;

        UnitGrade nextGrade = unit.grade + 1;
        UnitData result = gachaTable != null ? gachaTable.RollFromGrade(nextGrade) : null;

        if (result == null)
        {
            Debug.LogWarning($"CombineSystem: {nextGrade} 등급에서 뽑을 유닛을 찾지 못했습니다.");
            return false;
        }

        for (int i = 0; i < RequiredCount; i++)
        {
            targetInventory.Remove(unit);
        }

        targetInventory.Add(result);
        return true;
    }

    public List<UnitData> GetCombinableUnits()
    {
        List<UnitData> combinable = new List<UnitData>();

        UnitInventory targetInventory = Inventory;
        if (targetInventory == null)
        {
            Debug.LogError("CombineSystem: inventory 참조가 비어있습니다 (인스펙터 미할당, PlayerContext.Local도 없음).", this);
            return combinable;
        }

        HashSet<UnitData> seen = new HashSet<UnitData>();

        foreach (UnitData unit in targetInventory.Units)
        {
            if (unit == null || seen.Contains(unit)) continue;
            seen.Add(unit);

            if (CountOf(targetInventory, unit) >= RequiredCount)
            {
                combinable.Add(unit);
            }
        }

        return combinable;
    }

    int CountOf(UnitInventory targetInventory, UnitData unit)
    {
        int count = 0;
        foreach (UnitData u in targetInventory.Units)
        {
            if (u == unit) count++;
        }
        return count;
    }
}
