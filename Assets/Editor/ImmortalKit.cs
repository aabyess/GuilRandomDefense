using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>불멸 8종 Apply 공용 도구(2026-10-06, 설계표 Docs/research/IMMORTAL_DESIGN_2026-10-06.md). 스킬 에셋 만들기·효과 조각·유닛/조합식 설정.</summary>
static class ImmortalKit
{
    const string SkillFolder = "Assets/Data/UnitSkills";

    public static SkillData Skill(string unitKey, string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_불멸_{unitKey}_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }
        skill.skillName = skillName;
        skill.description = description;
        skill.triggerType = trigger;
        skill.levels = new List<SkillLevel>
        {
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    /// <summary>이름·설명만 있는 칸(효과는 로스터 필드나 다른 스킬이 한다).</summary>
    public static SkillData Label(string unitKey, string suffix, string skillName, string description)
        => Skill(unitKey, suffix, skillName, description, SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

    public static SkillData OnHit(string unitKey, string suffix, string skillName, string description, float chance, float range, params SkillEffect[] effects)
        => Skill(unitKey, suffix, skillName, description, SkillTriggerType.OnHitChance, range, chance, 0, SkillGaugeKind.Mana, effects);

    public static SkillData AuraSkill(string unitKey, string suffix, string skillName, string description, float range, params SkillEffect[] effects)
        => Skill(unitKey, suffix, skillName, description, SkillTriggerType.Aura, range, 1f, 0, SkillGaugeKind.Mana, effects);

    public static SkillData ManaSkill(string unitKey, string suffix, string skillName, string description, int threshold, float range, params SkillEffect[] effects)
        => Skill(unitKey, suffix, skillName, description, SkillTriggerType.OnHitCount, range, 1f, threshold, SkillGaugeKind.Mana, effects);

    // ── 효과 조각 ──
    public static SkillEffect Flat(float amount, DamageType type, SkillTargetKind target = SkillTargetKind.SingleTarget) => new SkillEffect
    { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = target, damageType = type, attackType = type == DamageType.AP ? AttackType.Spells : AttackType.Unassigned, multiplier = amount };

    /// <summary>%체력 피해 — PM 새 규칙: 전부 방어 무시.</summary>
    public static SkillEffect Pct(SkillEffectBasis basis, float ratio, DamageType type, SkillTargetKind target, SkillEffectTargetCondition cond = SkillEffectTargetCondition.None, float condValue = 0f) => new SkillEffect
    { kind = SkillEffectKind.Damage, basis = basis, target = target, damageType = type, attackType = type == DamageType.AP ? AttackType.Spells : AttackType.Unassigned, multiplier = ratio, armorIgnoreRatio = 1f, targetCondition = cond, targetConditionValue = condValue };

    public static SkillEffect Stun(float seconds, SkillTargetKind target) => new SkillEffect { kind = SkillEffectKind.Stun, target = target, duration = seconds };
    public static SkillEffect ArmorBreak(float amount, SkillTargetKind target = SkillTargetKind.SingleTarget) => new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = target, multiplier = amount };
    public static SkillEffect Aegr(float amount) => new SkillEffect { kind = SkillEffectKind.AegrStack, target = SkillTargetKind.SingleTarget, multiplier = amount };
    public static SkillEffect ArmorAura(float amount, string buffId, SkillEffectTargetCondition cond = SkillEffectTargetCondition.None, float condValue = 0f) => new SkillEffect
    { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -amount, buffId = buffId, targetCondition = cond, targetConditionValue = condValue };
    public static SkillEffect SlowAura(float remain, string buffId) => new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = remain, buffId = buffId };
    public static SkillEffect AsAura(float percent, string buffId, SkillTargetKind target) => new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = target, multiplier = percent, buffId = buffId };
    public static SkillEffect AdAura(float percent, string buffId, SkillTargetKind target) => new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = target, multiplier = percent, buffId = buffId };

    public static UnitData Unit(string name) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
    public static CombineRecipe Recipe(string name) => AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{name}.asset");

    /// <summary>유닛에 스킬·칭호·게이지를 쓰고 trait를 정리한다.</summary>
    public static void SetUnit(UnitData unit, string title, List<SkillData> skills, float manaMax, float lifeMax = -1f)
    {
        unit.skill = null;
        unit.skills = skills;
        unit.unitName = title;
        unit.manaMax = manaMax;
        unit.manaGaugePerMana = manaMax > 0f ? 1f : 0f;
        unit.manaGaugeStart = 0f;
        if (lifeMax >= 0f) { unit.lifeGaugeMax = lifeMax; unit.lifeGaugeStart = 0f; unit.lifeGaugeRegenPerSecond = 0f; unit.lifeGaugeCustomHitGain = false; }
        EditorUtility.SetDirty(unit);
    }

    public static void ReplaceIngredient(CombineRecipe recipe, string oldUnitName, UnitData replacement)
    {
        for (int i = 0; i < recipe.ingredients.Count; i++)
            if (recipe.ingredients[i] != null && recipe.ingredients[i].unit != null && recipe.ingredients[i].unit.name == oldUnitName) recipe.ingredients[i].unit = replacement;
    }

    public static string Names(CombineRecipe recipe)
    {
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "(빈 재료)");
        return string.Join(", ", names);
    }
}
