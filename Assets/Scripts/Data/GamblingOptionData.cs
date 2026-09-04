using UnityEngine;

public enum GamblingCategory
{
    Money,  // 돈 도박 — 엔 소모, 엔 획득. 사용 횟수 제한·해금 조건은 옵션마다 다르다.
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

    [Header("비용 — Unit은 costResourceType 자원, Money는 엔 고정")]
    public ResourceType costResourceType = ResourceType.Wood;
    public int cost;

    // 유닛 도박은 원작에서 **골드와 목재를 같이** 받는다(하급 250골드+목재1, 중급 1500+2,
    // 고급 2500+4, 다른세계 3500+5). 목재만 받으면 사실상 공짜라 도박이 선택이 아니게 된다.
    // 0이면 자원만 받는다 — 돈 도박(Money)은 위 cost가 곧 엔이므로 이 칸을 쓰지 않는다.
    public int goldCost;

    [Header("성공 확률 — Unit 카테고리 전용 (Money는 성공/실패 구분 없이 항상 결과 범위에서 나온다)")]
    [Range(0f, 100f)] public float successChancePercent;

    [Header("성공 시 — Unit 카테고리. 등급이 둘이면 GachaTable에 이미 설정된 weight로 가중 추첨")]
    public UnitGrade primaryResultGrade;
    public bool useSecondaryGrade;
    public UnitGrade secondaryResultGrade;

    [Header("결과 — Money 카테고리 (예: 0~100엔). 0이 나올 수도 있다")]
    public int successGoldMin;
    public int successGoldMax;

    // 돈 도박도 원작은 성공/실패가 갈린다(10엔 64%, 500엔 59%). 실패해도 판돈 일부를
    // 돌려줘서 완전한 0이 나오지는 않는다 — 10엔 도박은 3~5엔을 돌려받는다.
    public int failureGoldMin;
    public int failureGoldMax;

    [Header("실패 시 — Unit 카테고리. 고급·다른세계 도박만 켠다 (GAMBLING.md)")]
    public bool grantFailureReward;
    public int failureLuckyTokens = 2;
    public int failureWood = 1;

    [Header("사용 제한 — Money 카테고리 전용")]
    [Tooltip("평생 사용 가능 횟수. 0이면 무제한")]
    public int maxUses;
    [Tooltip("켜져 있으면 GamblingProgress.IsUnlocked(this)가 true여야 굴릴 수 있다. " +
             "해금 자체는 이 옵션 밖의 다른 시스템(예: 보스 처치)이 GamblingProgress.Unlock(this)를 불러 연다")]
    public bool requiresUnlock;
    [Tooltip("잠겨 있을 때 툴팁에 보여줄 이유 (예: \"10라운드 보스 처치 후 해금\")")]
    public string unlockHint;
    [Tooltip("이 라운드의 보스(EnemyData.isBoss)가 죽으면 해금된다. 0이면 보스 해금 대상이 아니다 " +
             "(다른 방식으로 해금하려면 requiresUnlock만 켜고 이건 0으로 둔 채 Unlock을 직접 부른다)")]
    public int unlockRound;
}
