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

    // 초월 조합 재료 전용(원작의 "쿠마 초월함 위습"). 유닛처럼 필드에 서 있지만 싸우지 않고,
    // 초월 24종의 마지막 재료로만 쓰인다. 일반 유닛 박은석과는 별개다(RECIPES_LOW.md "박은석 = 초월 위습").
    //
    // 기존 등급을 재활용하지 않고 값을 새로 붙인 이유: 등급 하나를 공유하는 순간 그 등급의
    // 뽑기 풀·전시 칸·조합식 표에 같이 끌려 들어간다. 어느 목록에도 안 걸리는 값이 필요하다.
    // 반드시 맨 뒤에 둘 것 — 중간에 끼우면 이미 직렬화된 등급 값이 전부 한 칸씩 밀린다.
    TranscendentWisp,
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
            default: return grade.ToString();
        }
    }

    public static int Tier(this UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Common: return 0;
            case UnitGrade.Uncommon: return 1;
            case UnitGrade.Special: return 2;
            case UnitGrade.Rare:
            case UnitGrade.Hidden: return 3;
            case UnitGrade.Superior: return 4;
            case UnitGrade.Legendary: return 5;
            case UnitGrade.Limited: return 6;
            case UnitGrade.Transcendent:
            case UnitGrade.Immortal:
            case UnitGrade.Eternal:
            case UnitGrade.OtherWorld:
            // 초월 재료라 초월과 같은 티어로 둔다. RandomUnit처럼 -1로 두면 연금술의
            // "maxDismantleGrade(희귀함, Tier 3) 이하만 분해" 검사를 통과해버려서,
            // 스토리로만 나오는 재료를 마나 몇 점에 분해할 수 있게 된다.
            case UnitGrade.TranscendentWisp: return 7;
            case UnitGrade.RandomUnit: return -1;
            default: return -1;
        }
    }
}

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
    public MovementAbility movementAbility;
    public float hp;
    public float attackPower;
    public float attackRange;
    public float attackSpeed;   // 초당 공격 횟수 (1.2 = 1초에 1.2번). UnitAttacker에서 1/attackSpeed로 간격 환산
    public float moveSpeed;
    public SkillData skill;
    public GameObject prefab;
}
