using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGachaTable", menuName = "GuilRandomDefense/Gacha Table")]
public class GachaTable : ScriptableObject
{
    [System.Serializable]
    public class GradeEntry
    {
        public UnitGrade grade;
        public float weight;
        public List<UnitData> pool;
    }

    public List<GradeEntry> entries;

    public UnitData Roll()
    {
        GradeEntry entry = RollGrade();
        if (entry == null) return null;

        if (entry.pool == null || entry.pool.Count == 0)
        {
            Debug.LogWarning($"GachaTable: {entry.grade} 등급 pool이 비어있습니다.");
            return null;
        }

        return entry.pool[Random.Range(0, entry.pool.Count)];
    }

    public UnitData RollFromGrade(UnitGrade grade)
    {
        GradeEntry entry = entries?.Find(e => e.grade == grade);
        if (entry == null)
        {
            Debug.LogWarning($"GachaTable: {grade} 등급 entry가 없습니다.");
            return null;
        }

        if (entry.pool == null || entry.pool.Count == 0)
        {
            Debug.LogWarning($"GachaTable: {grade} 등급 pool이 비어있습니다.");
            return null;
        }

        return entry.pool[Random.Range(0, entry.pool.Count)];
    }

    GradeEntry RollGrade()
    {
        if (entries == null || entries.Count == 0) return null;

        float totalWeight = 0f;
        foreach (GradeEntry entry in entries)
            totalWeight += entry.weight;

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (GradeEntry entry in entries)
        {
            cumulative += entry.weight;
            if (roll <= cumulative)
                return entry;
        }

        return entries[entries.Count - 1];
    }
}
