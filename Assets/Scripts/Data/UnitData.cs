using System.Collections.Generic;
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
    /// 사장님 확정 순서 (2026-09-05, 00번 결정 — "랜덤유닛 서열을 원작대로 최상위로 올려라"에서
    /// 시작해 구현담당2·리서치담당·PM이 원작 데이터로 검증해 최종 확정한 순서):
    /// <code>흔함 &lt; 안흔함 &lt; 특별함 &lt; 희귀함 &lt; 히든≒특수함 &lt; 전설≒변화됨 &lt; 영원 &lt; 랜덤유닛 &lt; 다른세계 &lt; 초월 &lt; 제한됨 &lt; 불멸</code>
    ///
    /// 원작 등급별 DPS 중앙값 (출처: Docs/reference/ORIGINAL_UNIT_STATS_BY_GRADE.json,
    /// 히든만 Docs/reference/HIDDEN_GRADE_STATS.json):
    /// 흔함 26.0(n=9) / 안흔함 115.5(n=14) / 특별함 575.0(n=33) / 희귀함 5,502.3(n=42) /
    /// 히든 30,122.3(n=27) / 특수함 14,518.2(n=4, 얇음) / 전설 41,423.6(n=40) / 변화됨 46,668.3(n=9) /
    /// 영원 56,339.4(n=11, 얇음) / 다른세계 63,473.9(n=10, 얇음, ="랜덤전용[제한됨]") /
    /// 초월 67,190.6(n=29) / 제한됨 67,971.9(n=11, 얇음) / 불멸 125,001.7(n=11) /
    /// 랜덤유닛 107,272.4(n=4, 매우 얇음).
    ///
    /// ⚠️ 초월(67,190.6)과 제한됨(67,971.9)의 차이는 781(1.2%)뿐이다 — 표본(29 vs 11)을
    /// 감안하면 표본 하나로 뒤집힐 수 있는 거리라 "약한 근거"다. 그래도 분리 유지하는 이유는
    /// 원작에서 둘의 획득 방법 자체가 다르기 때문이다(초월=조합, 제한됨=화이트리스트/채팅코드) —
    /// DPS 격차 크기를 증명하려는 게 아니라 원작이 붙인 등급 라벨을 따르는 것이다.
    ///
    /// ⚠️ 랜덤유닛은 DPS만 보면 영원·다른세계·초월·제한됨(56,339~67,972) 넷보다 높은데도
    /// 그 아래(다른세계 바로 밑)에 있다 — 의도된 역전이다. 원작 조합표(RECIPES.md 다른세계 섹션,
    /// "랜덤전용유닛 1기")에서 랜덤유닛이 다른세계의 재료로 직접 쓰이는 게 확인됐다
    /// (우리 자산 Assets/Data/Recipes/다른세계_*.asset 9개도 GUID로 대조 확인함).
    /// 재료가 결과보다 위 Tier일 수 없어 다른세계 바로 아래로 내렸다. 초월·불멸·영원 조합에는
    /// 랜덤유닛이 재료로 쓰이지 않는 것도 확인했으니 그 셋보다 아래로 내릴 근거는 없다
    /// (최소침습 배치, "ⓒ'" 안).
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
            // 특이 케이스라 사장님이 순서 목록에서 빼셨지만, 급은 여기가 맞다고 하셨다
            // (2026-09-02 확정, 00번 재검토 대상 아니었음).
            case UnitGrade.Hidden:
            case UnitGrade.Superior: return 4;

            // 변화됨은 전설·희귀함에 목재 10을 얹어 만든 것이라 전설과 같은 급에 둔다
            // (사장님 확정 2026-09-02: "변화됨은 전설쪽에 배치", 00번 재검토 대상 아니었음).
            case UnitGrade.Legendary:
            case UnitGrade.Transformed: return 5;

            case UnitGrade.Eternal: return 6;

            // 랜덤유닛은 다른세계의 조합 재료다 — 다른세계보다 아래여야 사다리가 성립한다.
            // 위 요약 참고. DPS(107,272)는 영원·다른세계·초월·제한됨보다 높지만 의도된 역전이다.
            case UnitGrade.RandomUnit: return 7;

            case UnitGrade.OtherWorld: return 8;
            case UnitGrade.Transcendent: return 9;
            case UnitGrade.Limited: return 10;
            case UnitGrade.Immortal: return 11;

            // 초월위습은 **등급이 아니다**(사장님 확정 2026-09-02) — 등급 목록·표·순서
            // 어디에도 올리지 않는다. 여기 값이 필요한 이유는 연금술 분해 방지 하나뿐이라
            // 모든 실제 등급보다 위(99)에 고정한다 — 등급이 추가/재배치돼도 항상 최상단에
            // 남아서 "분해 가능한 최상위 등급"이 실수로 여길 집어삼키는 일이 없게 한다.
            case UnitGrade.TranscendentWisp: return 99;

            // 새 등급을 더하고 여기에 case를 안 넣으면 -1로 떨어져서 연금술에 분해된다.
            // 등급을 추가할 때는 반드시 여기도 같이 손볼 것.
            default: return -1;
        }
    }

    /// <summary>
    /// 이 등급이 "등급별 화력이 우상향하는가"를 재는 사다리(측정 대상)에 속하는가.
    /// <see cref="Tier"/>가 서열(로스터 정렬·해체 상한)과 측정 대상을 같이 떠맡고 있어서
    /// 사장님 03번 확정("RandomUnit·OtherWorld를 사다리에서 뺀다") 때 둘을 갈랐다
    /// (PM 지시, 2026-09-06). <c>Tier()</c> 자체는 값·순서·주석 전부 그대로 둔다 — 조밀한
    /// 정수열이라 빼면 유령 티어나 재번호매기기 대가가 크고, GameHud 정렬·SupportShop
    /// 해체 상한처럼 서열이 필요한 소비처가 여전히 있기 때문이다. 사다리(등급별 DPS·
    /// 발동률 집계) 쪽만 이 메서드로 걸러낸다.
    ///
    /// false인 셋:
    /// - RandomUnit: 원작 "랜덤전용"은 등급이 아니라 작품 밖 콜라보 **카테고리**다.
    ///   결정적 증거 — "랜덤전용[제한됨]" 10기(등급이면 둘이 겹칠 수 없다).
    /// - OtherWorld: 원작 "다른세계"는 등급이 아니라 **도박 버튼 이름**이다(목재 7, 27%).
    ///   그 도박이 뽑는 14기가 전부 랜덤전용이다.
    /// - TranscendentWisp: 유닛이 아니라 조합 재료(재화)다 — 애초에 등급이 아니다
    ///   (Tier() 주석 참고, 사장님 확정 2026-09-02).
    /// </summary>
    public static bool IsLadderGrade(this UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.RandomUnit:
            case UnitGrade.OtherWorld:
            case UnitGrade.TranscendentWisp:
                return false;
            default:
                return true;
        }
    }
}

// AD = 물리공격 / AP = 마법공격 (사장님 확정 2026-09-03).
// ⚠️ 읽는 코드가 있다(2026-09-05 확인) — 위 주석은 방어력 시스템이 생기기 전에 적힌 것으로
// 지금은 낡았다. 실제 경로: UnitAttacker.DamageTypeOf가 이 필드를 그대로 돌려주고,
// UnitAttacker.Update()/ApplyCritIfTriggered가 target.TakeDamage(..., DamageTypeOf, ...)로
// 넘기면 EnemyDummy.TakeDamage → EnemyDummy.MitigatedDamage가 방어력 감폭·상성표 계산에
// 실제로 쓴다. 239종 전부 값이 채워져 있고 전부 도달한다.
//
// ⚠️ 2026-09-05 사장님 확정으로 정정: **AP가 물리 방어력을 무시하는 건 유닛 평타가 아니라
// 스킬 피해(도움소 등, AttackType.Spells)뿐이다.** 유닛 평타의 AP는 이제 AD와 똑같이
// 방어력 감폭을 탄다 — "AP는 방어력을 무시한다"였던 옛 규칙은 우리가 만든 것이었지 원작이
// 아니었다(원작 플레이어 유닛에 마법 공격타입 자체가 없다). 자세한 분기는
// EnemyDummy.MitigatedDamage 참고.
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

// 05번 「고대의 배」도박 능력 한 항목 (UnitData.gambleOptions 참고).
[System.Serializable]
public class UnitGambleOption
{
    public string abilityId;          // 원작 능력 문자열(A023 등) — 참고용 태그일 뿐, 로직이 읽지 않는다.
    public int woodCost;
    public float successChance;       // 0~1.
    // 특정 유닛 결과(A023·A0OD처럼 결과가 하나로 정해진 경우). resultPool과 동시에 채우지
    // 않는다 — resultUnit이 있으면 그걸 쓰고, 없으면 resultPool에서 고른다.
    public UnitData resultUnit;
    // 풀에서 랜덤 결과(A0OC처럼 여러 유닛 중 하나). 비어 있으면 해당 항목은 성공해도
    // 아무 유닛도 안 나온다(목재·유닛은 이미 소모된 뒤이므로 조용히 끝난다, 지어내지
    // 않는다) — GameHud가 이 상태의 버튼 자체를 숨긴다(hasResult 가드).
    //
    // A0OC(다른세계유닛 도박) — 원작 Modelpack_R_unit(gg_rct_Model_Pack_R1Unit 리전) 대응
    // 풀은 이름 매핑 없이 등급으로 확정했다(PLAYER7_NEUTRAL_POOL_CENSUS.md, 2026-09-07):
    // 원작 14종 전부 "랜덤전용" 등급 크로스오버 캐릭터였고, 우리 로스터도 마침 랜덤유닛
    // 등급이 정확히 14종이라 근사 없이 1:1로 채웠다(Unit_고대의배_h05Y.asset 참고,
    // name-mapping-impossible 원칙대로 이름이 아니라 등급으로 대응시켰다).
    public List<UnitData> resultPool = new List<UnitData>();
}

[CreateAssetMenu(fileName = "NewUnitData", menuName = "GuilRandomDefense/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitGrade grade;

    // ⚠️ 평타 전용 필드로 못박는다(2026-09-05, PM/사장님 B안 확정) — 원작 플레이어 유닛
    // 431종의 평타 공격타입을 전수조사하면 normal 127·siege 89·hero 28·pierce 25·chaos 1·
    // **magic 0건**이다. 즉 **원작에서 평타는 항상 물리다.** 마법은 스킬(트리거) 피해에만
    // 있다(SkillEffect.damageType/attackType 참고, 715건 중 62건).
    //
    // ⚠️ 2026-09-05 갱신: 지금 이 필드는 AD 229 / AD+AP 9(damageType=3) / None 1이고
    // **순수 AP(damageType=2)는 0종이다** — 원작 근거대로 정리가 이미 끝났다(위 comment의
    // "AP 40" 서술은 낡았다). 순수 AP가 다시 생기면 데이터 오류다 — 원작 평타에 마법이
    // 0종이기 때문이다(check_required_fields.py #12가 이걸 잡는다). AD+AP(9종)는 지금
    // EnemyDummy.MitigatedDamage에서 AD와 동일 취급(순수 AP 판정이 거짓이라 방어 무시가
    // 안 걸린다) — 이건 원작과도 맞다.
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

    // ⚠️ 이 필드도 아래 skills도 직접 읽지 말 것 — SkillCount/SkillAt(index)로만 통한다
    // (PM 지시 2026-09-05). 읽는 곳마다 "skills가 비면 skill로 폴백"을 각자 다시 짜면
    // 그중 하나가 안 하는 게 사고 지점이 된다.
    public SkillData skill;

    // 원작 스킬 채널이 유닛 하나당 최대 6개까지 나온다(MULTI_SKILL_IMPACT.md — 절대쿨
    // 게이트 21건 + 게이지·확률 게이트 200건, 96종이 이미 1·2채널 스킬 위에 최대 5개를
    // 더 받는다). 기존 238종 에셋의 skill 한 줄은 안 건드린다(하위호환) — 이 리스트가
    // 비어 있으면 SkillCount/SkillAt이 skill 하나를 "길이 1짜리 목록"으로 흡수한다.
    public List<SkillData> skills = new List<SkillData>();

    /// <summary>이 유닛이 가진 스킬 개수 — skills가 채워져 있으면 그 길이, 비어 있으면
    /// skill이 있을 때만 1(없으면 0). skill/skills를 직접 보지 말고 이거+SkillAt만 쓸 것.</summary>
    public int SkillCount => (skills != null && skills.Count > 0) ? skills.Count : (skill != null ? 1 : 0);

    /// <summary>index번째 스킬. skills가 비어 있으면 index는 항상 0이고 skill을 돌려준다
    /// (호출부가 SkillCount로 범위를 이미 확인했다고 가정 — 여기선 인덱스를 다시 안 잠근다,
    /// 매 프레임 불려서 새 리스트를 만들지 않으려는 것과 같은 이유로 방어 코드를 최소로
    /// 뒀다).</summary>
    public SkillData SkillAt(int index) => (skills != null && skills.Count > 0) ? skills[index] : skill;

    // 이 유닛을 대상으로 하는 특성강화(06번). UnitTraitData.targetUnit의 역참조다 — 골드/조합
    // 재료처럼 "이 유닛이 곧 그 자체로 대상"이라 별도 레지스트리를 두지 않고 skill과 같은
    // 방식(유닛→에셋 직접 참조)으로 둔다. 원작 26분기만 채워져 있고(06번①) 나머지 213종은
    // null이다 — GameHud의 특성강화 버튼이 null이면 버튼 자체를 숨긴다.
    public UnitTraitData trait;

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

    // 실제 등급이 있는 플레이어 유닛이 아니다(해적단 퀘스트 토큰 등) — grade는 배치 로직
    // (LaneMarker의 흔함 로스터 칸 배정) 때문에 여전히 값이 있지만, "등급으로서" 취급하면
    // 안 되는 자리에서는 이 플래그로 걸러야 한다.
    //
    // ⚠️ grade 자체를 바꾸는 대신 이 플래그를 새로 둔 이유(2026-09-05, WIRING_AUDIT.md §⑧):
    // grade를 다른 값으로 옮기면 그 값의 분해·조합 와일드카드 시스템으로 같은 문제가 그대로
    // 옮겨갈 뿐이다. UnitGrade에 새 값을 추가하는 것도 검토했으나, Tier()에 case를 안 넣으면
    // -1로 떨어져 연금술 분해 방지 검사(Rare 이하만 분해)를 우회하게 되고(지금 막으려는 바로
    // 그 사고가 다른 문으로 들어온다), 등급표·뽑기 풀·조합 와일드카드 등 등급을 순회하는
    // 모든 곳을 새로 확인해야 해서 위험 대비 이득이 안 맞는다는 PM 판단. 기본값 false — 기존
    // 239종은 전부 무영향이다.
    public bool isSystemUnit;

    // 01번 영웅 스탯(STR/AGI/INT) — 사장님 결정 2026-09-06. 초월함 스킬 4건("영웅 능력치 ×
    // 계수", 2,000×스탯~21,000×스탯)이 이 축이 없어서 비례항을 통째로 버리고 상수만
    // 옮겨져 있었다(Docs/reference/TRANSCENDENT_DAMAGE_INVESTIGATION_2026-09-06.md,
    // OUT_OF_AXIS_CLASSIFY.md). 원작은 도움소에서 사고(ADD) 시작값을 올리고, 적을 죽일
    // 때마다 레벨이 올라 레벨당 STR+0.85(타시기는 +0.42 — 유닛마다 달라 필드로 둔다)씩
    // 성장한다 — 정확한 시작값·성장치는 리서치담당 조사 대기 [미확인], 전부 기본값
    // 0f이라 지금 채워도 SkillEffectBasis.CasterStrength/Agility/Intelligence 배율에
    // 아무 영향이 없다(회귀 없음). 도움소 구매 경로는 아직 안 만든다(4번, 가격표 대기).
    public float baseStrength;
    public float baseAgility;
    public float baseIntelligence;
    public float strengthPerLevel;
    public float agilityPerLevel;
    public float intelligencePerLevel;

    // 05번 「고대의 배」도박 능력 목록(사장님 확정 2026-09-06, Docs/reference/
    // ANCIENT_SHIP_SPEC_2026-09-06.md + PLAYER7_NEUTRAL_POOL_CENSUS.md) — h05Y 전용.
    // 처음엔 "isAncientShip(bool)+ancientShipResultUnit(단일)" 하드코딩 페어로 만들었으나,
    // 리서치가 h05Y 하나가 도박 능력을 **셋**(A023 해적선도박·A0OD 레일리도박·A0OC
    // 다른세계유닛도박) 가진다는 걸 확인해 목록으로 바꿨다 — "나중에 늘 것 같아서"가
    // 아니라 "지금 이미 셋이라서"다(RewardDistributor.startingSpecialUnit 때와 같은 원칙,
    // 반대 결론). GameHud가 항목마다 버튼을 하나씩 띄운다: 목재 woodCost 소모(부족하면
    // 아무 일도 안 남 — 원작 stop 명령과 같다, 실패로 취급 안 함) → 유닛 소모(성공·실패
    // 무관, RemoveUnit과 같다, A023 기준으로 확인된 규칙을 셋 다에 같은 경로로 적용 —
    // 나머지 둘의 소모 규칙이 다르다는 근거는 없다) → successChance 확률로 resultUnit(또는
    // resultPool에서 랜덤)을 조합 구역 중심에 생성. 목록이 비면(기본값) 지금까지처럼
    // 버튼이 하나도 안 뜬다 — 기존 239종 전부 무영향(회귀 없음).
    public List<UnitGambleOption> gambleOptions = new List<UnitGambleOption>();

    // "유닛 판매" — 원작 판매(GetSoldUnit) 대응, PM 지시(2026-09-06). 이 유닛을 팔면
    // (GameHud 판매 버튼) 정의된 보상을 주고 유닛 자체는 사라진다(UnitIdentity.Consume,
    // 원작 RemoveUnit과 같다). 기본값(null/0)이면 "판매 버튼 자체가 안 뜬다" — 기존
    // 240종 전부 무영향(회귀 없음).
    //
    // 첫 사용처: h05X(레일리, "판매-특수" A0OE 툴팁 원문 확정) → sellRewardWisp=흔함
    // 선택위습, sellRewardTraitPoints=1.
    //
    // ⚠️ h05Y(고대의 배)는 겉보기에 같은 부류(isSystemUnit)지만 판매 보상을 안 채운다 —
    // "고대의 배를 팔면 보상을 준다"는 원작 근거가 없다(리서치 전수 확인, 2026-09-06.
    // 예전에 그렇게 적혀 있던 문서는 접근 불가한 도박 능력 A0OD와 이름이 겹쳐 생긴
    // 혼동이었다). h05Y는 isAncientShip 경로(위)로만 소모된다 — 판매 경로와 안 겹친다.
    public WispData sellRewardWisp;
    public int sellRewardTraitPoints;
}
