using System.Collections.Generic;

// 스킬 유형 분류(조합 검색 서랍 필터 + 정보창 피해 유형 표기가 같은 판정을 쓴다 — 설명과 필터가 어긋나지 않게 한 곳에서만 정한다).
// 사장님 정의(10-08): 고정 = 방어·마저항 무시 / 폭발형 = 마저항만·폭뎀증폭만 / 마법(AP) = 마저항·마깎·마뎀증폭 / 물리(AD) = 방어 적용.
public enum DamageKind { Physical, Magic, Explosive, Fixed }

[System.Flags]
public enum SkillTypeTag
{
    None = 0,
    Physical = 1 << 0,       // 물리 피해(AD, 고정·폭발형 아님)
    Magic = 1 << 1,          // 마법 피해(AP, 고정·폭발형 아님)
    Explosive = 1 << 2,      // 폭발형 피해
    Fixed = 1 << 3,          // 고정 피해
    ArmorIgnore = 1 << 4,    // 방무뎀: 물리 피해 효과의 방어 무시 비율 > 0, 또는 유닛 평타 방무뎀(UnitData.attackArmorIgnoreRatio)
    Finisher = 1 << 5,       // 끝딜: 최대체력 비례·잃은 체력 비례·일반 적 처형(KillNormalEnemies)
    ArmorBreak = 1 << 6,     // 방깎: ArmorBreak · 적에게 걸리는 음수 ArmorBonus
    MagicArmorBreak = 1 << 7,// 마깎: AegrStack
    MagicAmp = 1 << 8,       // 마뎀증폭: AisrStack
    ExplosiveAmp = 1 << 9,   // 폭증: A11SStack · LaneExplosiveAmp
    Slow = 1 << 10,          // 이속↓: Slow(남는 비율 < 1)
    Stun = 1 << 11,          // 스턴: Stun(자기 스턴 제외)
    // ── 칩으로는 안 쓰고 개수만 세는 참고 분류 ──
    SelfAttackDown = 1 << 12,   // 자기 공격력↓(01 디버프류)
    SelfSpeedDown = 1 << 13,    // 자기 공속↓(문 디버프류)
    EnemyArmorUp = 1 << 14,     // 적 방어↑(조씨 디버프류)
    EnemySpeedUp = 1 << 15,     // 적 이속↑(외동 디버프류)
    AllyMoveDebuff = 1 << 16,   // 아군 이속↓(노태현 하체부실류)
    AllyAttackDown = 1 << 17,   // 아군 공격력↓(임장혁 이간질류)
}

public static class SkillTypeTags
{
    /// <summary>한 피해 효과의 유형 — PlayerFacingText.DamageKindLabel과 필터가 똑같이 쓴다.</summary>
    public static DamageKind KindOf(SkillEffect e)
    {
        if (e.fixedDamage) return DamageKind.Fixed;
        if (e.explosive) return DamageKind.Explosive;
        if (e.damageType == DamageType.AP) return DamageKind.Magic;
        return DamageKind.Physical;
    }

    /// <summary>스킬 하나(모든 레벨·빌려주는 스킬 포함)의 태그.</summary>
    public static SkillTypeTag Of(SkillData skill)
    {
        SkillTypeTag tags = SkillTypeTag.None;
        Collect(skill, ref tags, 0);
        return tags;
    }

    static void Collect(SkillData skill, ref SkillTypeTag tags, int depth)
    {
        if (skill == null || skill.levels == null || depth > 3) return;
        foreach (SkillLevel level in skill.levels)
        {
            if (level == null || level.effects == null) continue;
            foreach (SkillEffect e in level.effects)
            {
                if (e == null) continue;
                tags |= OfEffect(e);
                if (e.kind == SkillEffectKind.GrantSkillToAllies && e.grantSkill != null) Collect(e.grantSkill, ref tags, depth + 1);
            }
        }
    }

    /// <summary>효과 하나의 태그.</summary>
    public static SkillTypeTag OfEffect(SkillEffect e)
    {
        SkillTypeTag t = SkillTypeTag.None;
        bool enemySide = e.target != SkillTargetKind.Self && e.target != SkillTargetKind.Allies && e.target != SkillTargetKind.DesignatedAlly;
        switch (e.kind)
        {
            case SkillEffectKind.Damage:
            case SkillEffectKind.DamageOverTime:
                switch (KindOf(e))
                {
                    case DamageKind.Fixed: t |= SkillTypeTag.Fixed; break;
                    case DamageKind.Explosive: t |= SkillTypeTag.Explosive; break;
                    case DamageKind.Magic: t |= SkillTypeTag.Magic; break;
                    default:
                        t |= SkillTypeTag.Physical;
                        if (e.armorIgnoreRatio > 0f) t |= SkillTypeTag.ArmorIgnore;
                        break;
                }
                if (e.kind == SkillEffectKind.Damage && (e.basis == SkillEffectBasis.TargetMaxHpPercent || e.basis == SkillEffectBasis.TargetMissingHpPercent)) t |= SkillTypeTag.Finisher;
                break;
            case SkillEffectKind.KillNormalEnemies: t |= SkillTypeTag.Finisher; break;
            case SkillEffectKind.ArmorBreak: t |= SkillTypeTag.ArmorBreak; break;
            case SkillEffectKind.ArmorBonus:
                if (enemySide && e.multiplier < 0f) t |= SkillTypeTag.ArmorBreak;
                else if (enemySide && e.multiplier > 0f) t |= SkillTypeTag.EnemyArmorUp;
                break;
            case SkillEffectKind.AegrStack: t |= SkillTypeTag.MagicArmorBreak; break;
            case SkillEffectKind.AisrStack: t |= SkillTypeTag.MagicAmp; break;
            case SkillEffectKind.A11SStack:
            case SkillEffectKind.LaneExplosiveAmp: t |= SkillTypeTag.ExplosiveAmp; break;
            case SkillEffectKind.Slow:
                if (e.multiplier < 1f) t |= SkillTypeTag.Slow; else if (e.multiplier > 1f) t |= SkillTypeTag.EnemySpeedUp;
                break;
            case SkillEffectKind.Stun: if (enemySide) t |= SkillTypeTag.Stun; break;
            case SkillEffectKind.AttackPowerBuffPercent:
            case SkillEffectKind.AttackPowerBuffFlat:
                if (e.multiplier < 0f) t |= e.target == SkillTargetKind.Allies ? SkillTypeTag.AllyAttackDown : SkillTypeTag.SelfAttackDown;
                break;
            case SkillEffectKind.AttackSpeedBuffPercent:
                if (e.multiplier < 0f) t |= SkillTypeTag.SelfSpeedDown;
                break;
            case SkillEffectKind.AllyMoveSpeedDebuff: t |= SkillTypeTag.AllyMoveDebuff; break;
        }
        return t;
    }

    /// <summary>특성강화(UnitData.trait)가 주는 방깎·마깎·이속↓·스턴 + 능력교체 스킬. 플레이어 눈엔 스킬이든 특성이든 「방깎 유닛」이다(사장님 10-15).
    /// 특성은 구매해야 켜지지만 표시·검색은 「가질 수 있는가」 기준이다.</summary>
    public static SkillTypeTag OfTrait(UnitTraitData trait)
    {
        SkillTypeTag t = SkillTypeTag.None;
        if (trait == null) return t;
        if (trait.effects != null)
            foreach (TraitEffect e in trait.effects)
            {
                if (e == null) continue;
                switch (e.kind)
                {
                    case TraitEffectKind.ArmorShred: t |= SkillTypeTag.ArmorBreak; break;
                    case TraitEffectKind.MagicArmorShred: t |= SkillTypeTag.MagicArmorBreak; break;
                    case TraitEffectKind.SlowOnHit: t |= SkillTypeTag.Slow; break;
                    case TraitEffectKind.StatusAilment: t |= SkillTypeTag.Stun; break;
                }
            }
        SkillTypeTag replaced = SkillTypeTag.None;
        if (trait.replacementSkill != null) Collect(trait.replacementSkill, ref replaced, 0);
        return t | replaced;
    }

    static readonly Dictionary<UnitData, SkillTypeTag> cache = new Dictionary<UnitData, SkillTypeTag>();

    /// <summary>유닛의 태그(스킬 목록 + 평타 방무뎀). 에셋은 실행 중 안 바뀌니 캐시한다.</summary>
    public static SkillTypeTag Of(UnitData unit)
    {
        if (unit == null) return SkillTypeTag.None;
        if (cache.TryGetValue(unit, out SkillTypeTag cached)) return cached;
        SkillTypeTag tags = SkillTypeTag.None;
        if (unit.skills != null && unit.skills.Count > 0)
            foreach (SkillData s in unit.skills) Collect(s, ref tags, 0);
        else Collect(unit.skill, ref tags, 0);
        if (unit.attackArmorIgnoreRatio > 0f) tags |= SkillTypeTag.ArmorIgnore;
        tags |= OfTrait(unit.trait);
        cache[unit] = tags;
        return tags;
    }
}
