using UnityEngine;

// "희귀함 리롤"(원작 A0VX) 수치 — 전부 여기서 나온다, 코드에는 공식만 있고 값은 없다
// (GamblingOptionData·SelfUpgradeAbilityData와 같은 결). Docs/reference/
// UNIQUE_REROLE_AND_SELL_FAMILY.md(리서치담당, 5943f1c) 원문 전수 확정 값을 기본값으로 둔다.
[CreateAssetMenu(fileName = "NewUniqueRerollAbilityData", menuName = "GuilRandomDefense/Unique Reroll Ability Data")]
public class UniqueRerollAbilityData : ScriptableObject
{
    [Header("시도 비용 — 성공/실패 무관하게 항상 차감(원작 확정)")]
    public int woodCost = 2;

    [Header("한도 — 플레이어당 판 전체 누적(라운드·유닛 단위 아님)")]
    public int baseLimit = 2;
    // 항법 "도박광"(NavigationChoice.Gambler) 선택 시 이 값으로 대체(가산이 아니라 대체 —
    // 원작 공식 "2+Dobak_Tech_int"를 base/gambler 두 값으로 풀어 쓴 것, 결과는 동일).
    public int gamblerLimit = 3;

    [Header("실패 확률(%) — 도박광 선택 시 0%(원작: 20-Dobak_Tech_int*80, 대입하면 -60→항상 미달)")]
    [Range(0f, 100f)] public float baseFailChancePercent = 20f;
    [Range(0f, 100f)] public float gamblerFailChancePercent = 0f;

    [Header("성공 시 교체 결과 — 원작 udg_Random4(리전스캔) 풀의 근사. 등급으로 대체함(이름 매핑 불가 원칙)")]
    public GachaTable gachaTable;
    public UnitGrade resultGrade = UnitGrade.Rare; // Rare = 희귀함(GamblingShop.GradeColor·Gambling_고급도박.primaryResultGrade=3과 동일 대응)
}
