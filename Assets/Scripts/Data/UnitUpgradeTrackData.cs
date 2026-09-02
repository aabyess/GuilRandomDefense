using System.Collections.Generic;
using UnityEngine;

// 강화소(유닛/다른세계/영원함 공용) 트랙 1개 = 등급 묶음 1개를 강화하는 버튼 1칸.
// 유닛강화소는 이 에셋 8개(흔함&안흔함 묶음 포함), 다른세계·영원함 강화소는 각각 1개만 물린다
// (구현담당2와 합의 — UnitUpgradeShop.cs 참고).
//
// 수치는 리서치담당의 [제안](Docs/reference/UPGRADE_SHOP.md "2차 조사") 그대로 옮겼다 — 원작에
// "등급 전체 강화" 시스템 자체가 없어서(원작 특성강화는 유닛별 개별 효과) 사용자 확정 전까지 가제다.
// 레벨당 공격력 ×1.1(10레벨 누적 +159%), 비용은 costBase × 1.5^레벨로 기하급수 증가.
[CreateAssetMenu(fileName = "NewUnitUpgradeTrackData", menuName = "GuilRandomDefense/Unit Upgrade Track Data")]
public class UnitUpgradeTrackData : ScriptableObject
{
    public string trackName;
    [TextArea] public string description;

    // 이 트랙이 강화하는 등급들. 흔함&안흔함처럼 여러 등급을 한 트랙에 묶을 수 있다.
    public List<UnitGrade> targetGrades = new List<UnitGrade>();

    public int maxLevel = 10;
    public int costBase = 100;          // 1레벨(레벨0→1) 비용, 단위는 엔
    public float costGrowthPerLevel = 1.5f;   // 다음 레벨 비용 = costBase * costGrowthPerLevel^현재레벨
    public float statGrowthPerLevel = 1.1f;   // 레벨당 공격력 배율 = statGrowthPerLevel^레벨

    public Color slotColor = Color.white;

    public int CostForLevel(int level) => Mathf.RoundToInt(costBase * Mathf.Pow(costGrowthPerLevel, level));

    public float MultiplierForLevel(int level) => Mathf.Pow(statGrowthPerLevel, level);
}
