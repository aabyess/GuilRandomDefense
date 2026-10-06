using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 김만경 「윤식파해결사」(초월_김만경_AD, 물딜) 적용(사장님 10-06 14번째 — 「다 추천대로」 확정, 설계표 Docs/research/TRANSCEND_MANGYEONG_DESIGN_2026-10-06.md).
/// 호출: call MangyeongApply.Apply (다시 불러도 안전). 도플라밍고 이식 스킬 6개를 4개로 교체:
///  · 해결사의능력 = 스플래시(UnitData.attackSplashRadius 300 — 스킬 효과 없음, 이름·설명만) · 강강약강 = 보잡(보스 ×1.3)
///  · 두려움의대상 = 방깍 오라(반경 800 보스 PV≥200 방어 −45, 원작 A0EI) + 이감 오라(반경 850 적 이속 −25%) · 빽 = 전설 이상 1기당 스플래시 피해 +2%(상한 +100%).
/// 칭호 「윤식파해결사」 · trait 비움 · 마나·체력 게이지 0 · 입력말 「구일의집행인」. 재료는 이미 사장님 표와 같다.
/// </summary>
static class MangyeongApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_김만경_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_김만경_AD.asset";
    const string Title = "윤식파해결사";
    const string Phrase = "구일의집행인";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_김만경_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = range, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_김만경_AD 유닛·조합식 에셋 없음";

        SkillData solver = MakeSkill("해결사의능력", "해결사의능력 — 스플래시(평타 범위 피해)",
            "사장님 10-06 「스플래시」(+「보조딜」은 역할 설명이라 스킬 없음). 평타가 맞은 적 주변 반경 300(원작 단위, UnitData.attackSplashRadius)의 같은 레인 적에게도 같은 피해(주 대상 평타 불변). 원작 최상호 AD 300 선례. 이 스킬 자체엔 효과가 없다(이름·설명만, 로스터 필드가 일한다).",
            SkillTriggerType.Aura, 0f);

        SkillData boss = MakeSkill("강강약강", "강강약강 — 보잡(보스 상대 피해 ×1.3)",
            "사장님 10-06 「보잡」. 보스(PV≥200) 상대 최종 피해 ×1.3(원작 근거 없음 — 제안값, PM 방침).",
            SkillTriggerType.Aura, 0f,
            new SkillEffect { kind = SkillEffectKind.BossDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f });

        SkillData fear = MakeSkill("두려움의대상", "두려움의대상 — 방깍 오라(보스 −45) + 이감 오라(−25%)",
            "사장님 10-06 「방깍(45)」·「이감」(확정: 방깍(45) = 오라, 보스만). 반경 800 안 보스(PV≥200) 방어 −45(원작 A0EI 노장의 패기 — 보스방깎과 같은 틀) + 반경 850 안 적 이속 −25%(원작 이감 오라 −20~35%).",
            SkillTriggerType.Aura, 800f,
            new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -45f, buffId = "MANG_ARMOR", targetCondition = SkillEffectTargetCondition.TargetPointValueAtLeast, targetConditionValue = 200f },
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.75f, buffId = "MANG_SLOW" });

        SkillData back = MakeSkill("빽", "빽 — 전설 이상 유닛 1기당 스플래시 피해 +2%(상한 +100%)",
            "사장님 10-06 「상위유닛개수비례스킬데미지강화」(확정: 상위 유닛 = 전설 이상, 스킬 데미지 = 스플래시 피해, 1기당 +2%·상한 +100%). 주인의 전설 이상 유닛(소환수·초월위습 제외, 구일 공격 오라와 같은 기준) 수 비례. 원작 근거 없음 — 제안값.",
            SkillTriggerType.Aura, 0f,
            new SkillEffect { kind = SkillEffectKind.SkillDamagePerHighGradeUnit, target = SkillTargetKind.Self, multiplier = 0.02f, bonus = 1.0f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { solver, boss, fear, back };
        unit.unitName = Title;
        unit.attackSplashRadius = 300f;
        unit.trait = null;   // 원작 능력교체형 특성이 새 스킬을 덮지 않게 — 특성 에셋은 그대로
        unit.manaMax = 0f;
        unit.lifeGaugeMax = 0f;
        unit.lifeGaugeStart = 0f;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"김만경 윤식파해결사 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 스플래시 {unit.attackSplashRadius} · trait 비움 · 게이지 0 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종";
    }
}
