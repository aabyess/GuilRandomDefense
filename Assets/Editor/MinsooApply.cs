using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 박민수 「해방된자」(초월_박민수_AD, 물딜) 적용(사장님 10-06): 조로 이식 스킬 7개를 빼고 사장님 스킬로 교체 · 칭호 · 입력말(카무쿠라이즈루). 재능투자는 UnitAttacker·GameHud(8~11번 칸).
/// 호출: call MinsooApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_MINSOO_DESIGN_2026-10-06.md.
///  · 감금 = 단일 스턴 0.8초(사장님) · 평타 20%(김경현 AP A0X4 20%) · 재능투자 스턴 단계가 지속·확률을 키운다.
///  · 인싸Lv.4 = 현재체력(단일): 평타 1/10 · 대상 현재체력 25%, 보스(PV≥200)는 고정 300000(도플라밍고 일반 25% · 보스 고정 분기).
///  · 체육특기생(민수) = 억제기(범위 스턴): 평타 1/12 · 범위 500 · 2.5초(구주호 키자루 1/12·500·2.75, 조로 Zoro_tiger 500·2.5). 스턴 단계가 같이 키운다.
///  · 무시무시한성장속도 = 시간 비례 피해: 30초마다 +2%, 상한 +100%(사장님 확정, 원작 근거 없음).
/// </summary>
static class MinsooApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_박민수_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_박민수_AD.asset";
    const string Title = "해방된자";
    const string Phrase = "카무쿠라이즈루";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_박민수_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillEffect Hit(SkillEffectBasis basis, float multiplier, SkillEffectTargetCondition condition, float conditionValue) => new SkillEffect
    {
        kind = SkillEffectKind.Damage, basis = basis, target = SkillTargetKind.SingleTarget,
        damageType = DamageType.AD, attackType = AttackType.Normal, multiplier = multiplier,
        targetCondition = condition, targetConditionValue = conditionValue,
    };

    static SkillEffect IgnoreArmor(SkillEffect e) { e.armorIgnoreRatio = 1f; return e; }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_박민수_AD 유닛·조합식 에셋 없음";

        SkillData lock1 = MakeSkill("감금", "감금 — 단일 스턴(발동)",
            "사장님 10-06 「스턴(단일, 0.8)」. 평타 20% 확률로 맞은 적 스턴 0.8초(원작 초월 김경현 AP A0X4 20%·0.5초 확률 기준). 재능투자 「스턴」 단계마다 지속 +0.35초·확률 +2.5%p.",
            SkillTriggerType.OnHitChance, 0f, 0.20f,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 0.8f, talentStunScaled = true });

        SkillData hp = MakeSkill("인싸Lv4", "인싸Lv.4 — 현재체력(단일)",
            "사장님 10-06 「현재체력(단일)」. 평타 10% 확률로 맞은 적 현재 체력의 25%(원작 도플라밍고 일반 DP_Skill_2, PV<200) — 보스(PV≥200)는 비례 대신 고정 300000(원작 보스 고정 분기).",
            SkillTriggerType.OnHitChance, 0f, 0.10f,
            IgnoreArmor(Hit(SkillEffectBasis.TargetCurrentHpPercent, 0.25f, SkillEffectTargetCondition.TargetPointValueLessThan, 200f)),   // %체력은 방어 무시(원작 UNIVERSAL) — 10-06 신 기준 감사로 일괄
            Hit(SkillEffectBasis.Flat, 300000f, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f));

        SkillData suppress = MakeSkill("체육특기생_억제기", "체육특기생(민수) — 억제기(범위 스턴)",
            "사장님 10-06 「억제기」 = 범위 스턴(원작엔 억제기 없음 — 사장님이 범위 스턴으로 정함). 평타 1/12 확률로 범위 500 안 적 스턴 2.5초(원작 구주호 키자루 1/12·500범위·2.75초, 조로 Zoro_tiger 500범위·2.5초 기준). 재능투자 「스턴」 단계가 같이 키운다.",
            SkillTriggerType.OnHitChance, 500f, 1f / 12f,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2.5f, talentStunScaled = true });

        SkillData growth = MakeSkill("무시무시한성장속도", "무시무시한성장속도 — 시간 비례 피해 증가(패시브)",
            "사장님 10-06 「시간비례데미지증가」. 이 유닛이 생긴 뒤 30초마다 최종 피해 +2%, 상한 +100%(사장님 확정, 원작 근거 없음).",
            SkillTriggerType.Aura, 0f, 1f,
            new SkillEffect { kind = SkillEffectKind.DamageGrowthOverTime, target = SkillTargetKind.Self, multiplier = 0.02f, bonus = 1.0f, duration = 30f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { lock1, hp, suppress, growth };
        unit.unitName = Title;
        unit.manaMax = 0f;   // 조로 마나 게이지 145는 버린다
        unit.trait = null;   // 새 스킬이 능력교체형 특성(원작018_H09I가 슬롯 0을 덮음)에 덮이지 않게 — 특성 에셋은 그대로(PM·사장님 결정)
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"박민수 해방된자 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종";
    }
}
