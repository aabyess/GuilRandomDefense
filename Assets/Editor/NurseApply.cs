using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 양재모 「물리간호사」(초월_양재모_AD, 물딜) 적용(사장님 10-06): 로우 이식 스킬 3개 → 사장님 4개 · 칭호 · 재료. 호출: call NurseApply.Apply (다시 불러도 안전).
/// 수치 근거(Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md):
///  · 상호파의최강자(현재체력): 평타 1/8 · 대상 현재 체력 ×0.20(AD) — 원작 Law_Skill_2(로키포트 주모자) 1/8·20%, 같은 종류 초월 14건 범위 0.1~0.4.
///  · 저지불가(단일스턴): 평타 20% · 1.5초 — 원작 초월 단일 스턴 27건 확률 15~40%·0.5~5초(A0X0 40%·1.5초, A0AG 20%·1.0초).
///  · 간호학과대표(공속 누적): 평타마다 +1% · 상한 +100% · 3초 안 치면 초기화(사장님 확정, 원작 근거 없음).
///  · 다한증(체력스킬): LIFE 게이지 50 · 범위 405 · 적 남는 속도 0.5 3초 + 자기 공속 −30% 5초 — 원작 LIFE 게이지 임계 40·50·85, 이감 발동형 0.25~0.75·2~5초, 자기 공속 감소는 원작 근거 없음(제안값).
///  「끝딜피해량감소」는 현재체력 스킬의 단점 서술(사장님 확정, 구현 없음).
/// </summary>
static class NurseApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_양재모_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_양재모_AD.asset";
    const string Title = "물리간호사";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_양재모_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, gaugeKind = gauge, hitCountThreshold = threshold, resetTo = 0, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_양재모_AD 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData current = MakeSkill("상호파의최강자", "상호파의최강자 — 현재체력(평타 1/8, 대상 현재 체력 20%)",
            "사장님 10-06 「현재체력」. 평타 1/8 확률로 대상 한 기의 현재 체력 ×20% 피해(AD). 원작 Law_Skill_2(로키포트 사건의 주모자) 1/8·20% 그대로. 적 체력이 줄수록 피해도 줄어드는 단점이 있다(사장님 「끝딜피해량감소」 = 이 설명, 구현 없음).",
            SkillTriggerType.OnHitChance, 0f, 0.125f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetCurrentHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.2f, armorIgnoreRatio = 1f });   // %체력은 방어 무시(원작 UNIVERSAL) — 10-06 신 기준 감사로 일괄

        SkillData stun = MakeSkill("저지불가", "저지불가 — 단일 스턴(평타 20%, 1.5초)",
            "사장님 10-06 「단일스턴」. 평타 20% 확률로 대상 한 기 스턴 1.5초. 원작 초월 단일 스턴 27건(확률 15~40%·0.5~5초) 중앙값 근처(A0X0 40%·1.5초, A0AG 20%·1.0초).",
            SkillTriggerType.OnHitChance, 0f, 0.2f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1.5f });

        SkillData nurse = MakeSkill("간호학과대표", "간호학과대표 — 평타마다 공속 +1% 누적(상한 +100%, 3초 무공격 시 초기화)",
            "사장님 10-06 「체력회복오라」 교체. 평타 한 대마다 자기 공격속도 +1%가 쌓인다(상한 +100%). 마지막 평타 뒤 3초 안 치면 0으로 초기화. 원작 근거 없음(원작 공속은 고정 오라 +12~50%뿐) — 제안값.",
            SkillTriggerType.OnHitChance, 0f, 1f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.AttackSpeedStack, target = SkillTargetKind.Self, stackPerHit = 0.01f, stackCap = 1f, stackResetSeconds = 3f });

        SkillData sweat = MakeSkill("다한증", "다한증 — 체력스킬(LIFE 게이지 50): 적 이속 감소 + 자기 공속 감소",
            "사장님 10-06 「체력스킬(이속감소, 자신공속감소)」. LIFE 게이지가 50에 닿으면 범위 405 안 적 이동속도 −50% 3초 + 자기 공속 −30% 5초(단점). LIFE 게이지 임계는 원작 40·50·85 중간, 이감 발동형 0.25~0.75·2~5초 범위, 자기 공속 감소는 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitCount, 405f, 1f, SkillGaugeKind.Life, 50,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.5f, duration = 3f },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = -0.3f, duration = 5f, buffId = "NURSE_SWEAT" });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { current, stun, nurse, sweat };
        unit.unitName = Title;
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_양재모"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_홍인창"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_최동준"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_박민수"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("흔함_최상호"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },
        };
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int nulls = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"물리간호사 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → 4개 · 칭호 「{unit.unitName}」 · 재료 {recipe.ingredients.Count}종" + (nulls > 0 ? $" · ⚠️ 빈 재료 {nulls}" : "");
    }
}
