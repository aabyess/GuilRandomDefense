using System.Collections.Generic;
using UnityEngine;

public enum SupportSkillTargetKind
{
    Ground,   // 커서로 지점을 찍는다 (폭우/지진/불비/해루석/출항이다/버스터콜/마나포션)
    Unit,     // 유닛을 직접 지정한다 (연금술 — 자기 유닛 / 흡수 — 적 유닛)
}

public enum SupportSkillEffect
{
    // 즉발 광역 피해. duration>0이면 맞은 적을 그만큼 묶어둔다(지진 — 원작은 피해 없는 순수
    // 스턴이라 damageBase/damagePerRound를 0으로 두고 이 duration만 쓴다). waveCount>1이면
    // 그 지속시간에 걸쳐 파도처럼 나눠 때린다(불비).
    Damage,
    Root,          // 구속 + 지속 마법피해 (해루석)
    Buff,          // 아군 버프 (출항이다)
    ManaRestore,   // 자기 마나 즉시 회복 (마나포션)
    UnitDismantle, // 지정한 자기 유닛을 분해해 마나로 환급 (연금술 — 2026-09-04 삭제, 코드는 보존)
    // 지정한 적 유닛 하나를 즉시 제거한다(흡수). 원작 확인(2026-09-04): RemoveUnit 방식이라
    // 방어력·저항을 안 타고 보스·스토리 유닛엔 안 먹힌다 — EnemyDummy.RemoveInstantly가 그대로다.
    InstantKill,
    // 지정한 적 유닛 하나에 즉발 피해(+duration>0이면 스턴)를 준다(낙뢰). InstantKill과 targetKind는
    // 같은 Unit이지만 대상을 죽이지 않고 ComputeDamage만큼만 때린다는 점이 다르다.
    SingleTargetDamage,
    // 대상 지정 없이 흔함 선택위습 1기를 필드에 만들어낸다(선택위습제조). ManaRestore와 같은
    // "자기 자신에게 즉발"류라 targetKind는 의미 없다(원작 능력 데이터도 atar가 none이었다).
    CraftChosenWisp,
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

    // GamblingProgress(GamblingOptionData.maxUses/UsesSoFar)와 같은 방식 — 원작 커스텀 게임은
    // 세이브/로드가 없어 한 판이 곧 "평생"이라, 판당 제한과 평생 제한이 같은 것이다.
    // 0(기본값)이면 무제한 — 기존 스킬은 전부 이 값이라 동작이 안 바뀐다.
    [Header("사용 횟수 상한 — 0이면 무제한(선택위습제조는 3)")]
    public int maxUses;

    [Header("범위 (targetKind == Ground)")]
    public float radius;
    public bool mapWide;           // true면 radius 대신 자기 소유 유닛 전체(출항이다)

    [Header("피해량(웨이브 1회분) = damageBase + damagePerRound * 현재라운드")]
    public float damageBase;
    public float damagePerRound;

    // 불비 전용 — 원작 "1웨이브당 5만뎀, 총 4웨이브"를 표현하려고 추가했다. 1(기본값)이면
    // 기존 스킬(지진·폭우·버스터콜)과 완전히 같은 즉발 1회 동작 — 이 필드가 없던 시절과 동작이
    // 똑같다. 원작의 "맞으면 초당 2.8만뎀 6초 화상"은 이번엔 생략했다(알려진 단순화 — 아래 참고).
    [Header("다단 히트(Damage 전용) — 1(기본)이면 즉발 1회, 그 이상이면 duration에 걸쳐 나눠 친다")]
    public int waveCount = 1;

    [Header("지속시간 — Root(구속+DoT 지속), Buff(지속), Damage(duration>0이면 스턴 지속)")]
    public float duration;

    // 이 이름의 스킬을 쓰던 예전 "흡수"(피해+마나환급)는 InstantKill로 바뀌면서 더 안 쓴다 —
    // 지우진 않았다, 나중에 비슷한 결의 스킬(맞은 수만큼 환급)이 생기면 재사용할 수 있어서.
    [Header("(미사용) 맞은 적 1기당 마나 환급, 상한 있음")]
    public int manaRefundPerHit;
    public int manaRefundCap;

    [Header("마나포션 전용")]
    public int manaRestoreAmount;

    [Header("선택위습제조 전용 — 지급할 위습(흔함 선택위습)")]
    public WispData craftedWisp;

    [Header("출항이다 전용 — 원작은 공격력 버프다(우리가 예전에 공격속도로 잘못 만들었었다)")]
    public float buffAttackPowerMultiplier = 1f;
    // (미사용, 위와 같은 이유로 보존) 예전엔 이 스킬이 공격속도를 올리는 걸로 잘못 구현돼 있었다.
    public float buffAttackSpeedMultiplier = 1f;

    [Header("해루석 전용 — 첫 타격이 대상 최대 체력의 이 비율만큼 추가 피해(0~1). 원작 \"전체 체력의 7%\"")]
    public float firstHitMaxHpPercent;

    [Header("연금술 전용 (targetKind == Unit)")]
    public UnitGrade maxDismantleGrade = UnitGrade.Rare;
    public List<GradeManaRefund> dismantleRefunds;

    public float ComputeDamage(int currentRound)
    {
        return Mathf.Max(0f, damageBase + damagePerRound * currentRound);
    }
}
