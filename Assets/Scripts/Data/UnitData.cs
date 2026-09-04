using UnityEngine;

public enum UnitGrade
{
    Common,
    Uncommon,
    Special,
    Rare,
    Hidden,
    Legendary,
    Limited,
    Transcendent,
    Immortal,
    Eternal,
    RandomUnit,
    OtherWorld,
    Superior,   // 특수함 — 희귀함과 전설적인 사이. enum 순서가 아니라 Tier()가 강함을 결정한다.

    // ⚠️ 이것은 **등급이 아니다**(사장님 확정 2026-09-02). 등급 목록·등급표·강함 순서
    // 어디에도 올리지 말 것 — 조합 재료 한 종류일 뿐이다. enum 값을 쓰는 건 순전히
    // 라우팅·분해 검사 때문이고, 그래서 등급 자리에 앉아 있을 뿐이다.
    //
    // 초월 조합 재료 전용(원작의 "쿠마 초월함 위습"). 유닛처럼 필드에 서 있지만 싸우지 않고,
    // 초월 24종의 마지막 재료로만 쓰인다. 일반 유닛 박은석과는 별개다(RECIPES_LOW.md "박은석 = 초월 위습").
    //
    // 기존 등급을 재활용하지 않고 값을 새로 붙인 이유: 등급 하나를 공유하는 순간 그 등급의
    // 뽑기 풀·전시 칸·조합식 표에 같이 끌려 들어간다. 어느 목록에도 안 걸리는 값이 필요하다.
    // 반드시 맨 뒤에 둘 것 — 중간에 끼우면 이미 직렬화된 등급 값이 전부 한 칸씩 밀린다.
    TranscendentWisp,

    // 변화됨 — 뽑기로 나오는 등급이 아니라 **업그레이드 결과**다(사장님 확정 2026-09-02):
    // "변화됨은 박은석 전설로 목재 10개주고 변화됨으로 업그레이드할거임".
    // 전설적인 유닛 + 목재 10 → 변화됨. 그래서 GachaTable 풀에는 넣지 않는다 —
    // 뽑기로 나오면 목재 10을 우회하게 된다.
    // TranscendentWisp와 같은 이유로 맨 뒤에 붙인다.
    Transformed,
}

public static class UnitGradeExtensions
{
    // 동급 등급은 같은 Tier를 반환한다 (Docs/reference/COMBINE_SYSTEM.md 1장 참고).
    // RandomUnit은 조합 라인 밖(확률로만 획득)이라 -1.
    // 화면 표기용 한글 등급명. 로스터 에셋 이름의 접두사와 같은 표기를 쓴다.
    /// <summary>
    /// 등급을 나타내는 색. 조합표의 칸·벽과 하단 명령 그리드가 <b>같은 곳에서</b> 가져간다 —
    /// 두 군데가 각자 정의하면 같은 등급이 화면마다 다른 색으로 보이고, 색으로 등급을 읽는
    /// 조합표에서는 그게 곧 오독이 된다.
    ///
    /// 아래 여섯(전설적인·특별함·희귀함·흔함·안흔함·히든)은 원본 조합표 이미지의 글자색을
    /// 실측해서 맞춘 값이다(`Docs/reference/RECIPE_AUDIT.md`). 나머지는 근거가 없다 —
    /// 상위 등급은 이미지에서 <b>결과로만 나오고 재료 글자로는 안 나와서</b> 잰 적이 없다.
    /// 그래서 그쪽은 "서로 구분되고 위로 갈수록 뜨거워 보인다"는 기준으로 우리가 정했다.
    /// </summary>
    public static Color Color(this UnitGrade grade)
    {
        switch (grade)
        {
            // 실측값
            case UnitGrade.Common:
            case UnitGrade.Uncommon:        return new Color(0.36f, 0.70f, 0.40f);   // 초록
            case UnitGrade.Special:         return new Color(0.88f, 0.78f, 0.28f);   // 금
            case UnitGrade.Rare:            return new Color(0.60f, 0.36f, 0.78f);   // 짙은 보라
            case UnitGrade.Hidden:          return new Color(0.30f, 0.52f, 0.86f);   // 하늘
            case UnitGrade.Legendary:       return new Color(0.82f, 0.24f, 0.24f);   // 빨강
            case UnitGrade.Transformed:     return new Color(0.80f, 0.55f, 0.79f);   // 밝은 핑크 (204,141,201)

            // 우리가 정한 값
            case UnitGrade.Limited:         return new Color(0.92f, 0.52f, 0.18f);   // 주황
            case UnitGrade.Transcendent:    return new Color(0.20f, 0.80f, 0.76f);   // 청록
            case UnitGrade.Immortal:        return new Color(0.95f, 0.93f, 0.80f);   // 상아
            case UnitGrade.Eternal:         return new Color(0.22f, 0.28f, 0.72f);   // 남색
            case UnitGrade.OtherWorld:      return new Color(0.85f, 0.35f, 0.62f);   // 자홍
            case UnitGrade.Superior:        return new Color(0.55f, 0.85f, 0.30f);   // 연두
            case UnitGrade.RandomUnit:      return new Color(0.55f, 0.60f, 0.68f);   // 청회색
            case UnitGrade.TranscendentWisp: return new Color(0.45f, 0.80f, 0.95f);  // 밝은 하늘

            default:                        return new Color(0.62f, 0.62f, 0.62f);
        }
    }

    public static string KoreanName(this UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Common: return "흔함";
            case UnitGrade.Uncommon: return "안흔함";
            case UnitGrade.Special: return "특별함";
            case UnitGrade.Rare: return "희귀함";
            case UnitGrade.Hidden: return "히든";
            case UnitGrade.Superior: return "특수함";
            case UnitGrade.Legendary: return "전설적인";
            case UnitGrade.Limited: return "제한됨";
            case UnitGrade.Transcendent: return "초월함";
            case UnitGrade.Immortal: return "불멸";
            case UnitGrade.Eternal: return "영원함";
            case UnitGrade.OtherWorld: return "다른세계";
            case UnitGrade.RandomUnit: return "랜덤유닛";
            case UnitGrade.TranscendentWisp: return "초월위습";
            case UnitGrade.Transformed: return "변화됨";
            default: return grade.ToString();
        }
    }

    /// <summary>
    /// 등급의 강함. **enum 선언 순서가 아니라 이 값이 강함을 결정한다** —
    /// 특수함은 선언이 맨 뒤(12)지만 실제로는 희귀함 바로 위다.
    ///
    /// 사장님 확정 순서 (2026-09-02):
    /// <code>흔함 &lt; 안흔함 &lt; 특별함 &lt; 희귀함 &lt; 히든 ≤ 전설 &lt; 랜덤유닛 &lt; 제한됨 &lt; 초월 ≤ 불멸 ≤ 다른세계</code>
    /// 여기에 "특수함은 희귀함 윗단계"가 더해진다. 엄밀한 전투력 서열이 아니라
    /// 대략의 급을 나타내는 값이라고 하셨다.
    /// </summary>
    public static int Tier(this UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Common: return 0;
            case UnitGrade.Uncommon: return 1;
            case UnitGrade.Special: return 2;
            case UnitGrade.Rare: return 3;

            // 둘 다 희귀함 바로 윗단계다. 특수함은 조합식이 없고 뽑기 보너스로만 나오는
            // 특이 케이스라 사장님이 순서 목록에서 빼셨지만, 급은 여기가 맞다고 하셨다.
            case UnitGrade.Hidden:
            case UnitGrade.Superior: return 4;

            // 변화됨은 전설·희귀함에 목재 10을 얹어 만든 것이라 전설과 같은 급에 둔다
            // (사장님 확정 2026-09-02: "변화됨은 전설쪽에 배치").
            case UnitGrade.Legendary:
            case UnitGrade.Transformed: return 5;

            // 사장님 순서에서 전설과 제한됨 사이다. 예전에는 -1이었는데, 그 값이면
            // 연금술의 "희귀함(3) 이하만 분해" 검사를 통과해서 분해할 수 있었다.
            // 이제 전설보다 위라 분해되지 않는다 — 의도된 변화다.
            case UnitGrade.RandomUnit: return 6;

            case UnitGrade.Limited: return 7;

            // 초월 ≤ 불멸 ≤ 다른세계 — 같은 급으로 묶는다.
            // ⚠️ 영원함은 사장님 순서 목록에 없어서 우리가 여기에 뒀다.
            //
            // 초월위습은 **등급이 아니다**(사장님 확정 2026-09-02) — 등급 목록·표·순서
            // 어디에도 올리지 않는다. 그런데도 여기 값이 필요한 이유는 연금술 하나뿐이다:
            // 분해 검사가 Tier()로 비교하는데, 값이 낮으면 초월 조합 24개가 요구하는
            // 그 재료를 마나 몇 점에 녹일 수 있게 되고 다시 구할 방법이 없다.
            case UnitGrade.Transcendent:
            case UnitGrade.Immortal:
            case UnitGrade.OtherWorld:
            case UnitGrade.Eternal:
            case UnitGrade.TranscendentWisp: return 8;

            // 새 등급을 더하고 여기에 case를 안 넣으면 -1로 떨어져서 연금술에 분해된다.
            // 등급을 추가할 때는 반드시 여기도 같이 손볼 것.
            default: return -1;
        }
    }
}

// AD = 물리공격 / AP = 마법공격 (사장님 확정 2026-09-03).
// ⚠️ 읽는 코드가 있다(2026-09-05 확인) — 위 주석은 방어력 시스템이 생기기 전에 적힌 것으로
// 지금은 낡았다. 실제 경로: UnitAttacker.DamageTypeOf가 이 필드를 그대로 돌려주고,
// UnitAttacker.Update()/ApplyCritIfTriggered가 target.TakeDamage(..., DamageTypeOf, ...)로
// 넘기면 EnemyDummy.TakeDamage → EnemyDummy.MitigatedDamage가 방어력 감폭·상성표 계산에
// 실제로 쓴다(AP는 방어력을 무시하는 등). 239종 전부 값이 채워져 있고 전부 도달한다.
[System.Flags]
public enum DamageType
{
    None = 0,
    AD = 1,
    AP = 2,
}

[System.Flags]
public enum MovementAbility
{
    Ground = 0,        // 기본: 육지만
    Flying = 1,        // 비행 — 바다 통과 가능
    WaterWalk = 2,     // 수상보행 — 바다 통과 가능
    Teleport = 4,      // 텔레포트(경로 무시) — 필드만, 로직은 나중에 구현
}

[CreateAssetMenu(fileName = "NewUnitData", menuName = "GuilRandomDefense/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitGrade grade;
    public DamageType damageType;

    // 평타의 공격 타입(원작의 normal/pierce/siege/hero/chaos). damageType과 **직교한다** —
    // damageType은 물리냐 마법이냐, attackType은 그 물리가 어느 종류냐다.
    // ⚠️ "239종 어디에도 안 붙어서 전부 Unassigned"는 낡은 서술이다(2026-09-05 확인) —
    // damageType=AP인 40종엔 이미 Magic이 붙어 있다(668ed8c). 물리(AD, 나머지 199종)의
    // 세부 타입(normal/pierce/siege/hero/chaos) 배정만 아직 안 됐다 — 등급이 아니라
    // damageType으로 갈리는 진행 상태다.
    public AttackType attackType = AttackType.Unassigned;
    public MovementAbility movementAbility;

    // 읽는 코드가 없는 게 정상이다(2026-09-05 확인) — 원작도 아군 유닛 체력은 사실상 의미가
    // 없다(전부 10, UNIT_STATS_RESEARCH.md:569). 원작은 "아군이 한 대도 안 맞는다"는 전제로
    // 짜인 밸런스라 유닛이 반격당해 죽는 개념 자체가 없다(ORIGINAL_VALUES.md). 우리도 마찬가지로
    // UnitIdentity에 체력 필드가 없고, HealthBarLayer도 EnemyDummy만 돈다 — 플레이어 유닛
    // 체력바를 만들려는 사람은 이 필드부터 채우는 게 아니라 "정말 필요한가"를 먼저 사장님께
    // 물어야 한다. 239종에 값은 다 채워져 있다(100~6,122) — 죽은 배선이 아니라 안 쓰는 배선이다.
    public float hp;
    public float attackPower;
    public float attackRange;
    public float attackSpeed;   // 초당 공격 횟수 (1.2 = 1초에 1.2번). UnitAttacker에서 1/attackSpeed로 간격 환산
    public float moveSpeed;
    public SkillData skill;
    public GameObject prefab;

    // 평타 강화(원작 Bash, war3map.w3a ACbh 기반 469개 중 실효 137개). 평타가 적중할 때마다
    // 별개의 추가 피해 인스턴스가 확률로 한 번 더 들어간다 — 평타를 대체하지 않고 얹힌다.
    // UnitAttacker.Update()가 읽는다. chance가 0(기본값)이면 완전히 비활성 — 239종을 전부
    // 안 채운 채로 커밋해도 게임 동작이 그대로다(Docs/reference/AUTO_ATTACK_CRIT_DESIGN.md).
    [Header("평타 강화(원작 Bash) — chance가 0이면 완전히 비활성")]
    public float critChance;                // 0~1. 기본 0.
    // 항상 1이다(PM 확정, 2026-09-05, AUTO_ATTACK_CRIT_DESIGN.md §6.2 "후자") — 원작 배수
    // (Hbh2)분은 이미 critBonusDamage에 "평타 대비 비율"로 흡수돼 있다. 여기에 값을 넣으면
    // (AttackDamage*multiplier + critBonusDamage에서) 배수 효과가 두 번 곱해진다. 채우지 말 것.
    public float critDamageMultiplier = 1f; // 배수. chance=0이면 안 쓰이지만 안전하게 1로 둔다.
    public float critBonusDamage;           // 고정 추가피해. 기본 0.
    public float critStunDuration;          // 발동 시 대상 기절 시간(초). 기본 0 = 기절 없음.
}
