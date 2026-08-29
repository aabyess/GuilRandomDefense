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
}

public static class UnitGradeExtensions
{
    // 동급 등급은 같은 Tier를 반환한다 (Docs/reference/COMBINE_SYSTEM.md 1장 참고).
    // RandomUnit은 조합 라인 밖(확률로만 획득)이라 -1.
    public static int Tier(this UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Common: return 0;
            case UnitGrade.Uncommon: return 1;
            case UnitGrade.Special: return 2;
            case UnitGrade.Rare:
            case UnitGrade.Hidden: return 3;
            case UnitGrade.Legendary: return 4;
            case UnitGrade.Limited: return 5;
            case UnitGrade.Transcendent:
            case UnitGrade.Immortal:
            case UnitGrade.Eternal:
            case UnitGrade.OtherWorld: return 6;
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

[CreateAssetMenu(fileName = "NewUnitData", menuName = "GuilRandomDefense/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitGrade grade;
    public DamageType damageType;
    public float hp;
    public float attackPower;
    public float attackRange;
    public float attackSpeed;   // 초당 공격 횟수 (1.2 = 1초에 1.2번). UnitAttacker에서 1/attackSpeed로 간격 환산
    public float moveSpeed;
    public SkillData skill;
    public GameObject prefab;
}
