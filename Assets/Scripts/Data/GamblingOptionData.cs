using UnityEngine;

public enum GamblingCategory
{
    Money,  // 돈 도박 — 골드 소모, 골드 획득. 누적 졸업 상한의 대상 (GamblingShop이 추적).
    Unit,   // 유닛 도박 — 지정 자원(목재) 소모, 유닛 지급.
}

// 도박소 옵션 하나. 수치는 전부 여기서 나온다 — 코드에는 공식만 있고 값은 없다.
// SupportSkillData와 같은 결.
[CreateAssetMenu(fileName = "NewGamblingOptionData", menuName = "GuilRandomDefense/Gambling Option Data")]
public class GamblingOptionData : ScriptableObject
{
    public string optionName;

    // 툴팁용 한 줄 설명. 정확한 확률·수치는 다른 필드에서 코드가 직접 읽는다.
    [TextArea] public string description;

    public GamblingCategory category;

    [Header("비용 — Unit은 costResourceType 자원, Money는 골드 고정")]
    public ResourceType costResourceType = ResourceType.Wood;
    public int cost;

    [Header("성공 확률")]
    [Range(0f, 100f)] public float successChancePercent;

    [Header("성공 시 — Unit 카테고리. 등급이 둘이면 GachaTable에 이미 설정된 weight로 가중 추첨")]
    public UnitGrade primaryResultGrade;
    public bool useSecondaryGrade;
    public UnitGrade secondaryResultGrade;

    [Header("성공 시 — Money 카테고리 (예: 7~50골드 획득)")]
    public int successGoldMin;
    public int successGoldMax;

    [Header("실패 시 — Unit 카테고리. 고급·다른세계 도박만 켠다 (GAMBLING.md)")]
    public bool grantFailureReward;
    public int failureLuckyTokens = 2;
    public int failureWood = 1;

    [Header("실패 시 — Money 카테고리 (예: 3~5골드 반환)")]
    public int failureGoldMin;
    public int failureGoldMax;
}
