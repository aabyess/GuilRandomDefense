using System;
using System.Collections.Generic;

/// <summary>
/// 조합 도우미 능력 꼬리표 — 유닛의 스킬(SkillData.levels[0].effects·트리거)·UnitData 필드에서 **규칙표로 자동 유도**한다. 손으로 붙이지 않는다(설계표 §2, PM 확정 §11).
/// 에셋을 고치면 꼬리표가 따라 바뀐다. 데이터에 근거가 없는 tmo 항목(광폭화·보조딜·마법뎀증가류·순간이동 …)은 만들지 않는다 — 필터 칩은 「그 꼬리표를 가진 유닛이 1기 이상」일 때만 뜬다.
/// 용어는 사장님 정의 우선: 아머브레이크 = 평타 발동 단일 대상 방어 −N(암브) · 끝딜 = 확률 발동 최대체력 %·마나 스킬 잃은 체력 %·확률 처형 · 범위 % = 범위 안 적 전체 체력 비례(나무위키 범퍼뎀).
/// </summary>
[Flags]
public enum SkillTag : long
{
    None = 0,
    Slow = 1L << 0,             // 이동속도 감소(상시 오라)
    SlowOnHit = 1L << 1,        // 발동이감
    Stun = 1L << 2,
    ArmorReduce = 1L << 3,      // 방어력 감소(전체)
    ArmorReduceOnHit = 1L << 4, // 발동방깎
    ArmorBreak = 1L << 5,       // 아머브레이크(사장님 「암브」: 발동 단일 방깎)
    StackingArmor = 1L << 6,    // 중첩방깎(스택 계열·누적)
    AttackUp = 1L << 7,         // 공격력 증가(상시·오라)
    AttackUpOnHit = 1L << 8,    // 발동공격력증가
    AttackSpeedUp = 1L << 9,    // 공격속도 증가
    HealRegen = 1L << 10,       // 체력 재생(아군)
    ManaRegen = 1L << 11,       // 마나 재생(오라)
    Flying = 1L << 12,          // 공중이동
    Single = 1L << 13,          // 단일(현재 체력 비례 단일 대상)
    Finisher = 1L << 14,        // 끝딜
    AreaMaxPct = 1L << 15,      // 범위 전체 체력 퍼센트
    AreaCurrentPct = 1L << 16,  // 범위 현재 체력 퍼센트
    AreaMissingPct = 1L << 17,  // 범위 잃은 체력 퍼센트
    IgnoreArmor = 1L << 18,     // 방무뎀
    UnitDelete = 1L << 19,      // 유닛삭제
    BossKiller = 1L << 20,      // 보스 잡기
}

public static class SkillTags
{
    /// <summary>필터·표시 순서와 한글 이름.</summary>
    public static readonly (SkillTag tag, string label, string shortLabel)[] All =
    {
        (SkillTag.Slow, "이동속도 감소", "이"),
        (SkillTag.SlowOnHit, "발동이감", "발이"),
        (SkillTag.Stun, "스턴", "스"),
        (SkillTag.ArmorReduce, "방어력 감소", "깎"),
        (SkillTag.ArmorReduceOnHit, "발동방깎", "발깎"),
        (SkillTag.ArmorBreak, "아머브레이크", "암"),
        (SkillTag.StackingArmor, "중첩방깎", "중깎"),
        (SkillTag.AttackUp, "공격력 증가", "공"),
        (SkillTag.AttackUpOnHit, "발동공격력증가", "발공"),
        (SkillTag.AttackSpeedUp, "공격속도 증가", "공속"),
        (SkillTag.HealRegen, "체력 재생", "체재"),
        (SkillTag.ManaRegen, "마나 재생", "마재"),
        (SkillTag.Flying, "공중이동", "공이"),
        (SkillTag.Single, "단일", "단"),
        (SkillTag.Finisher, "끝딜", "끝"),
        (SkillTag.AreaMaxPct, "범위 전체 체력 퍼센트", "범전"),
        (SkillTag.AreaCurrentPct, "범위 현재 체력 퍼센트", "범현"),
        (SkillTag.AreaMissingPct, "범위 잃은 체력 퍼센트", "범잃"),
        (SkillTag.IgnoreArmor, "방무뎀", "방무"),
        (SkillTag.UnitDelete, "유닛삭제", "삭"),
        (SkillTag.BossKiller, "보스 잡기", "보"),
    };

    static readonly Dictionary<UnitData, SkillTag> cache = new Dictionary<UnitData, SkillTag>();

    /// <summary>유닛의 꼬리표 묶음(캐시 — 에셋은 실행 중 안 바뀐다). 에디터에서 에셋을 고친 뒤엔 Clear().</summary>
    public static SkillTag Of(UnitData unit)
    {
        if (unit == null) return SkillTag.None;
        if (!cache.TryGetValue(unit, out SkillTag tags)) cache[unit] = tags = Derive(unit);
        return tags;
    }

    public static void Clear() => cache.Clear();

    public static string Label(SkillTag tag)
    {
        foreach (var entry in All) if (entry.tag == tag) return entry.label;
        return tag.ToString();
    }

    public static IEnumerable<string> Labels(SkillTag tags)
    {
        foreach (var entry in All) if ((tags & entry.tag) != 0) yield return entry.label;
    }

    /// <summary>이름 옆 짧은 꼬리표(우선순위 순 최대 count개).</summary>
    public static string ShortList(SkillTag tags, int count = 2)
    {
        var parts = new List<string>();
        foreach (var entry in All)
        {
            if ((tags & entry.tag) == 0) continue;
            parts.Add(entry.shortLabel);
            if (parts.Count >= count) break;
        }
        return string.Join("·", parts);
    }

    static bool IsOnHit(SkillTriggerType t) => t == SkillTriggerType.OnHitChance || t == SkillTriggerType.OnHitCount || t == SkillTriggerType.CooldownAutoCast || t == SkillTriggerType.OnEnemyEnterRange;

    /// <summary>규칙표(설계표 §2) — 스킬 효과·트리거·유닛 필드 → 꼬리표.</summary>
    public static SkillTag Derive(UnitData unit)
    {
        SkillTag tags = SkillTag.None;
        if ((unit.movementAbility & MovementAbility.Flying) != 0) tags |= SkillTag.Flying;
        if (unit.manaAuraRegenPerSecond > 0f) tags |= SkillTag.ManaRegen;
        if (unit.lifeAuraRegenPerSecond > 0f) tags |= SkillTag.HealRegen;

        int count = unit.SkillCount;
        for (int i = 0; i < count; i++)
        {
            SkillData skill = unit.SkillAt(i);
            if (skill == null || skill.levels == null || skill.levels.Count == 0) continue;
            SkillLevel level = skill.levels[0];
            if (level == null || level.effects == null) continue;
            bool onHit = IsOnHit(skill.triggerType);
            bool aura = skill.triggerType == SkillTriggerType.Aura;
            foreach (SkillEffect e in level.effects)
            {
                if (e == null) continue;
                bool single = e.target == SkillTargetKind.SingleTarget;
                bool area = e.target == SkillTargetKind.Enemies || e.target == SkillTargetKind.ChainEnemies || e.target == SkillTargetKind.RandomEnemyInRange;
                bool allyish = e.target == SkillTargetKind.Allies || e.target == SkillTargetKind.Self;
                switch (e.kind)
                {
                    case SkillEffectKind.Stun:
                        tags |= SkillTag.Stun; break;
                    case SkillEffectKind.Slow:
                        tags |= aura ? SkillTag.Slow : SkillTag.SlowOnHit; break;
                    case SkillEffectKind.ArmorBreak:
                        tags |= SkillTag.ArmorReduce;
                        if (onHit) tags |= SkillTag.ArmorReduceOnHit;
                        if (e.hitCount > 1) tags |= SkillTag.StackingArmor;
                        else if (onHit && single) tags |= SkillTag.ArmorBreak;   // 사장님 「암브」: 평타 발동 단일 대상 방어 −N
                        break;
                    case SkillEffectKind.ArmorBonus:
                        if (e.multiplier < 0f) { tags |= SkillTag.ArmorReduce; if (onHit) tags |= SkillTag.ArmorReduceOnHit; }
                        break;
                    case SkillEffectKind.AegrStack:
                    case SkillEffectKind.AisrStack:
                    case SkillEffectKind.A11SStack:
                    case SkillEffectKind.A0VJStack:
                        tags |= SkillTag.ArmorReduce | SkillTag.StackingArmor;
                        if (onHit) tags |= SkillTag.ArmorReduceOnHit;
                        break;
                    case SkillEffectKind.AttackPowerBuffPercent:
                    case SkillEffectKind.AttackPowerBuffFlat:
                        if (allyish) tags |= aura ? SkillTag.AttackUp : SkillTag.AttackUpOnHit; break;
                    case SkillEffectKind.AttackSpeedBuffPercent:
                    case SkillEffectKind.AttackSpeedStack:
                        tags |= SkillTag.AttackSpeedUp; break;
                    case SkillEffectKind.HealOverTime:
                        if (allyish) tags |= SkillTag.HealRegen; break;
                    case SkillEffectKind.KillNormalEnemies:
                        tags |= SkillTag.UnitDelete; break;
                    case SkillEffectKind.BossDamageMultiplier:
                        tags |= SkillTag.BossKiller; break;
                    case SkillEffectKind.Damage:
                        if (e.armorIgnoreRatio > 0f) tags |= SkillTag.IgnoreArmor;
                        if (e.targetCondition == SkillEffectTargetCondition.TargetPointValueAtLeast) tags |= SkillTag.BossKiller;
                        if (e.killMostLostHp) tags |= SkillTag.Finisher;
                        switch (e.basis)
                        {
                            case SkillEffectBasis.TargetMaxHpPercent:
                                if (area) tags |= SkillTag.AreaMaxPct;
                                if (single && skill.triggerType == SkillTriggerType.OnHitChance) tags |= SkillTag.Finisher;   // 확률 발동 최대체력 = 끝딜
                                break;
                            case SkillEffectBasis.TargetCurrentHpPercent:
                                if (area) tags |= SkillTag.AreaCurrentPct;
                                else if (single) tags |= SkillTag.Single;
                                break;
                            case SkillEffectBasis.TargetMissingHpPercent:
                                if (area) tags |= SkillTag.AreaMissingPct;
                                if (single && level.gaugeKind == SkillGaugeKind.Mana) tags |= SkillTag.Finisher;   // 마나 스킬 잃은 체력 = 끝딜
                                break;
                        }
                        break;
                }
            }
        }
        return tags;
    }
}
