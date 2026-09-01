using System.Collections.Generic;
using UnityEngine;

public enum SupportSkillTargetKind
{
    Ground,   // 커서로 지점을 찍는다 (폭우/지진/해적선충돌/해루석/출항이다/버스터콜/흡수/마나포션)
    Unit,     // 유닛을 직접 지정한다 (연금술)
}

public enum SupportSkillEffect
{
    Damage,        // 즉발 광역 피해 (duration>0이면 맞은 적을 그만큼 묶어둔다 — 해적선충돌의 스턴)
    Root,          // 구속 + 지속 마법피해 (해루석)
    Buff,          // 아군 버프 (출항이다)
    ManaRestore,   // 자기 마나 즉시 회복 (마나포션)
    UnitDismantle, // 지정한 자기 유닛을 분해해 마나로 환급 (연금술)
}

[System.Serializable]
public class GradeManaRefund
{
    public UnitGrade grade;
    public int manaRefund;
}

// 도움소 스킬 하나. 수치는 전부 여기서 나온다 — 코드에는 공식만 있고 값은 없다.
[CreateAssetMenu(fileName = "NewSupportSkillData", menuName = "GuilRandomDefense/Support Skill Data")]
public class SupportSkillData : ScriptableObject
{
    public string skillName;

    // 툴팁용 한 줄 효과 서술. 마나·쿨다운·피해량·범위·지속시간 같은 수치는 코드가 이 필드와
    // 별개로 다른 필드에서 직접 채운다 — 여기엔 "무엇을 하는 스킬인가"만 적는다.
    [TextArea] public string description;

    public SupportSkillTargetKind targetKind = SupportSkillTargetKind.Ground;
    public SupportSkillEffect effect = SupportSkillEffect.Damage;

    [Header("비용")]
    public int manaCost;
    public int goldCost;          // 마나포션 전용 — 나머지는 0
    public float cooldownSeconds;

    [Header("범위 (targetKind == Ground)")]
    public float radius;
    public bool mapWide;           // true면 radius 대신 자기 소유 유닛 전체(출항이다)

    [Header("피해량 = damageBase + damagePerRound * 현재라운드")]
    public float damageBase;
    public float damagePerRound;

    [Header("지속시간 — Root(구속+DoT 지속), Buff(지속), Damage(duration>0이면 스턴 지속)")]
    public float duration;

    [Header("흡수 전용 — 맞은 적 1기당 마나 환급, 상한 있음")]
    public int manaRefundPerHit;
    public int manaRefundCap;

    [Header("마나포션 전용")]
    public int manaRestoreAmount;

    [Header("출항이다 전용")]
    public float buffAttackSpeedMultiplier = 1f;

    [Header("연금술 전용 (targetKind == Unit)")]
    public UnitGrade maxDismantleGrade = UnitGrade.Rare;
    public List<GradeManaRefund> dismantleRefunds;

    public float ComputeDamage(int currentRound)
    {
        return Mathf.Max(0f, damageBase + damagePerRound * currentRound);
    }
}
