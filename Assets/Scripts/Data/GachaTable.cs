using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGachaTable", menuName = "GuilRandomDefense/Gacha Table")]
public class GachaTable : ScriptableObject
{
    [System.Serializable]
    public class GradeEntry
    {
        public UnitGrade grade;

        // 지금 이 값을 읽는 Roll()을 부르는 곳이 코드에 0건이다(2026-09-05 감사) — 실제
        // 지급 경로(UnitPortal.RollReward)는 RollFromGrade로 등급을 직접 지정해서 weight를
        // 아예 안 본다. MainGachaTable.asset은 grade 4(히든) 이상 전부 weight=0으로 저장돼
        // 있다 — 지금은 죽은 값이라 문제 없지만, Roll()을 실제로 쓰기 시작하면 그 순간
        // 고등급이 하나도 안 나온다. 쓰기 전에 채울 것 — 확률은 사장님 콘텐츠라 값은 안 건드림.
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
