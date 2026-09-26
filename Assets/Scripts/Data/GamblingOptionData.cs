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

    // 성공 시 등급 뽑기보다 먼저 굴리는 낮은 확률의 특정 유닛 보너스(원작 "유닛도박
    // 초급/중급"의 해적선 — UnitPortal.bonusUnit/bonusChancePercent와 같은 모양).
    // 비어 있으면(bonusUnit==null) 이 축을 안 쓴다 — 기존 도박 옵션은 그대로 동작한다.
    [Header("성공 시 보너스 — Unit 카테고리 전용. 등급 풀보다 먼저 이 확률로 시도한다")]
    public UnitData bonusUnit;
    [Range(0f, 100f)] public float bonusChancePercent;

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

    // ⚠️ 맨 뒤에 추가(2026-09-06, "항법" 5택1 연결, NAVIGATION_ROUTES_FULL.md) — 직렬화
    // 순서를 지킨다. 원작 다른세계 도박(Trig_Unit_Gemble_4)만 확인된 공식이다 —
    // 실패 시 럭키토큰 수량 = 1+Dobak_Tech_int(항법 "도박광" 선택 시 1, 아니면 0).
    // **다른 도박 옵션(고급도박 등)이 같은 변수를 쓰는지는 확인 안 됐다** — false가
    // 기본값이라 이 필드를 안 켠 기존 옵션은 회귀 없음. 이 필드가 켜진 옵션만
    // failureLuckyTokens를 "항법 선택 전 기본값"으로 두고, 실제 지급 시 코드가
    // NavigationChoice.Gambler면 +1 더한다(GamblingShop.RollUnit 참고).
    [Header("항법 '도박광' 연동 — 원작이 이 필드를 통해 확인된 옵션만 켠다(추측 금지)")]
    public bool scalesWithGamblerNavigation;

    // ⚠️ 맨 뒤에 추가(2026-09-07, "희귀함 리롤" A0VX, UNIQUE_REROLE_AND_SELL_FAMILY.md ⑦) —
    // A0VX는 원작에서 정확히 3곳(H0B0판매·h06D 고급유닛도박·스토리Tier4)의 "최종 폴백
    // 분기"(지정된 특별 결과가 아닌 일반 랜덤풀 결과)에서만 붙는다. 우리 도박소 옵션 중
    // 이 소스로 확인된 건 h06D=Gambling_고급도박 하나뿐이다(GAMBLING.md:191, goldCost
    // 2500 일치) — 다른 옵션(하급·중급·다른세계 도박)은 원작에 이 폴백-A0VX 연결 근거가
    // 없으므로 기본값 false로 둔다(추측 금지, scalesWithGamblerNavigation과 같은 원칙).
    // GamblingShop.TryRollUnit이 성공(bonusUnit 미적중, 즉 "이름 없는 일반 결과")일 때만 읽는다.
    [Header("희귀함 리롤(A0VX) 연동 — 원작이 이 옵션의 최종 폴백에서 A0VX를 준다고 확인된 경우만 켠다")]
    public bool grantsUniqueRerollOnGenericSuccess;

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

    // ⚠️ 맨 뒤에 추가(2026-09-26, 사장님 지시 「원랜디처럼 최대 N개까지 쌓이고 쿨타임마다 1개씩 충전」) —
    //    워크3 상점 재고(usma 최대 · usrg 충전 간격 · usin 시작 재고)를 그대로 옮긴다. 원작 w3u 직접 디코드:
    //      h06F 돈도박 초급  usrg 14 · usin 0 · usma 비어 있음 → hfoo 기본 3(UnitBalance.slk) — 원작 3(사장님 09-26 확정)
    //      H0AZ 돈도박 고급  usma 7 · usrg 12 · usin 0 · Rhse(R10 보스) 해금
    //      h0AK 목재 구입    usma 5 · usrg 3600 · usin 0 / H0B0 고급 유닛 생성 usma 1 · usrg 3600 · usin 0
    //    stockMax 0이면 재고를 안 쓴다(유닛 도박 등 — 예전과 같다). 해금 옵션은 해금 순간부터 stockInitial에서 충전을 시작한다.
    [Header("상점 재고 — 워크3 usma/usrg/usin. stockMax 0이면 재고 없음(무제한)")]
    public int stockMax;
    public float stockRegenSeconds;
    public int stockInitial;

    // 원작 돈도박 고급(Trig_Money_Gemble_3): 받은 돈(당첨 500~4500 · 실패 환급 300~400)을 누적해 **당첨 때** 35,000 이상이면
    //    「돈도박 골드획득 한계에 도달하여 돈도박-고급을 졸업합니다!」 — 도박소가 h08C(돈도박 초급·고급·물품지원 없음 ·
    //    특성 포인트 구매 · 고급 유닛 생성 · 목재 구입 · 다른세계)로 바뀐다. 0이면 졸업과 무관.
    [Header("졸업 — 원작 돈도박 고급 누적 35,000")]
    public int graduateAtCumulative;
    [Tooltip("졸업하면 이 칸이 사라진다(원작 h08C에 없는 것: 돈도박 초급·고급)")]
    public bool retiredOnGraduation;
    [Tooltip("졸업해야 나타난다(원작 h08C에만 있는 것: 목재 구입·고급 유닛 생성)")]
    public bool requiresGraduation;

    // 원작 h0AK 목재 구입: 10,000골드 → 목재 1(Trig_Money_trade). Money 카테고리에서 0보다 크면 골드 대신 이 자원을 준다.
    [Header("Money 카테고리 — 골드 대신 자원 지급(목재 구입)")]
    public ResourceType payoutResourceType = ResourceType.Wood;
    public int payoutResourceAmount;

    // 원작 H0B0 고급 유닛 생성: 4% h05X → 아니면 1/38로 특수함(UniqueSpecial) → 아니면 희귀함(Random4) + 희귀함 리롤.
    //    useSecondaryGrade의 표 가중치 대신 **정확한 확률**이 필요해 둔다. 0이면 예전처럼 GachaTable 가중치.
    [Header("둘째 등급 확률 — 0이면 GachaTable 가중치")]
    [Range(0f, 100f)] public float secondaryChancePercent;

    // 원작 H0B0 요구 연구 R02F는 Story_reward5가 준다. 우리 스토리 번호는 원작과 별개라(story-numbering-is-ours)
    //    「스토리를 N개 깼다」로 근사한다. 0이면 조건 없음.
    [Tooltip("스토리를 이만큼 깨야 굴릴 수 있다(원작 R02F 근사). 0이면 조건 없음")]
    public int requiresStoriesCleared;
}
