using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 두유찬 「보스의앞잡이」(초월_두유찬_AD, 물딜) 적용(사장님 10-06 20번째): 원작 참모총장 이식 스킬 7개를 빼고 사장님 스킬 3개로 교체 · 칭호 · 재료(임채민 전설 → 희귀) · 입력말(꺽다리) · 체력 게이지 50 · trait 비움.
/// 호출: call DuyuchanApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_YUCHAN_DESIGN_2026-10-06.md (사장님 확정 반영).
///  · 혼신의일격 = 체력 스킬: 체력 게이지(평타 +1) 50 가득 → 범위 405 안 적 최대 체력의 7%(AD, 보스 포함). 「공격횟수당 체력 1 감소」는 이 체력 게이지 설명(사장님 확정 — 별도 스킬 없음).
///  · 짓밟기 = 단일 스턴: 평타 15% · 1.0초(제안값).
///  · 폭언 = 이감 20%: 평타 15% 발동 · 단일 대상 남는 속도 0.8 · 3초(사장님 수치 20%).
///  · 꺽다리 = 타이핑 말 / 이름만 — 스킬 에셋 없음(사장님 확정 짝: 꺽다리=이름만).
/// </summary>
static class DuyuchanApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_두유찬_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_두유찬_AD.asset";
    const string Title = "보스의앞잡이";
    const string Phrase = "꺽다리";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_두유찬_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = threshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_두유찬_AD 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData strike = MakeSkill("혼신의일격", "혼신의일격 — 체력스킬(체력 게이지 50): 대상 최대 체력 7%",
            "사장님 10-06 「체력스킬(전체체력7%)」 · 「공격횟수당 체력1감소」(= 평타마다 체력 게이지 1, 사장님 확정). 체력 게이지가 50에 차면 범위 405 안 적 최대 체력의 7% 물리 피해(보스 포함 — 최대체력 비례 규칙, 원작 %체력 스킬처럼 방어 무시) → 게이지 0. 임계 50은 원작 LIFE 게이지 40·50·85 중앙 — 제안값.",
            SkillTriggerType.OnHitCount, 405f, 1f, SkillGaugeKind.Life, 50,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMaxHpPercent, target = SkillTargetKind.Enemies, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.07f, armorIgnoreRatio = 1f });   // 원작 %체력 스킬은 UNIVERSAL(방어 무시) — 신 보스 방어 317에선 안 무시하면 7%가 약 1%가 된다(실측 3.06%)

        SkillData stomp = MakeSkill("짓밟기", "짓밟기 — 단일 스턴(발동)",
            "사장님 10-06 「스턴(단일)」. 평타 15% 확률로 맞은 적 스턴 1.0초(원작 초월 단일 스턴 27건 확률 15~40%·0.5~5초 하한 근처) — 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.15f, SkillGaugeKind.Life, 0,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1.0f });

        SkillData abuse = MakeSkill("폭언", "폭언 — 이감 20%(발동)",
            "사장님 10-06 「발동(이감 20%)」. 평타 15% 확률로 맞은 적 이동속도 −20%(남는 속도 0.8) 3초 — 확률·시간은 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.15f, SkillGaugeKind.Life, 0,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.8f, duration = 3f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { strike, stomp, abuse };
        unit.unitName = Title;
        unit.trait = null;
        unit.lifeGaugeMax = 50f;
        unit.lifeGaugeStart = 0f;
        unit.lifeGaugeRegenPerSecond = 0f;
        unit.lifeGaugeCustomHitGain = false;   // 평타 +1
        EditorUtility.SetDirty(unit);

        // 재료: 「임채민 채민파리더」 = 희귀함_임채민(전설 임채민은 교회파리더) — 사장님 표에 맞춘다.
        UnitData chaemin = Roster("희귀함_임채민"), legendChaemin = Roster("전설적인_임채민");
        int swapped = 0;
        foreach (RecipeIngredient ing in recipe.ingredients)
            if (ing.unit == legendChaemin && chaemin != null) { ing.unit = chaemin; swapped++; }
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"두유찬 보스의앞잡이 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · trait 비움 · 체력 게이지 {unit.lifeGaugeMax} · 임채민 재료 교체 {swapped}건 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
