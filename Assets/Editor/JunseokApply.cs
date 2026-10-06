using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 황준석 「공짜집착증」(초월_황준석_ADAP, 물딜+마딜) 적용(사장님 10-06 「다 추천대로」): 스킬 4 · 칭호 · 입력말(구일대표홍보대사) · 비행 · trait 비움. 재료는 이미 같다(확인만).
/// 호출: call JunseokApply.Apply (다시 불러도 안전). 설계표: Docs/design/JUNSEOK_DESIGN_2026-10-06.md §7(확정).
///  ① 준석의담판(외교) = 이 유닛이 있는 동안 처치 골드 배율 +0.2(GoldPlusBonus) · ② 결의 = 맵 전체 공속 +20% 오라 · ③ 믿음직한도움(스토리) = PV≥200 스토리 적 상대 피해 ×1.3 ·
///  ④ 어디든지달려갑니다 = 지형무시 이동(movementAbility Flying → FlyingMover, 효과 0인 표시용 스킬).
/// </summary>
static class JunseokApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_황준석_ADAP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_황준석_ADAP.asset";
    const string Title = "공짜집착증";
    const string Phrase = "구일대표홍보대사";
    const float MapWide = 50000f;   // 「맵 전체」 — 같은 주인 아군 전부. 오라 반경은 range / WorldScale(4.167)로 비교돼 5000은 세계 1200밖에 안 닿는다(실측: 1500 떨어진 아군 미적용)

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_황준석_ADAP_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }
        skill.skillName = skillName;
        skill.description = description;
        skill.triggerType = trigger;
        skill.levels = new List<SkillLevel>(levels);
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_황준석_ADAP 유닛·조합식 에셋 없음";

        SkillData diplomacy = MakeSkill("준석의담판", "준석의담판 — 처치 골드 +0.2(외교)",
            "사장님 10-06 「외교」(해석 A 확정). 이 유닛이 살아 있는 동안 내 처치 골드 배율에 +0.2(원작 Gold_Plus 아이템 I00Z +0.20과 같은 크기). 유닛이 사라지면 되돌린다.",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.GoldPlusBonus, target = SkillTargetKind.Self, multiplier = 0.2f } } });

        SkillData resolve = MakeSkill("결의", "결의 — 공격속도 오라(맵 전체)",
            "사장님 10-06 「맵전체(공속, 오라)」. 같은 레인 아군 전체와 자기 공격속도 +20%(원작 황준석 의협 A0WL 아군 공속 +20% 오라, 반경만 맵 전체).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = MapWide, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.20f, buffId = "JUNSEOK_AS_AURA" },
                    new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.20f, buffId = "JUNSEOK_AS_AURA" },
                },
            });

        SkillData help = MakeSkill("믿음직한도움", "믿음직한도움 — 스토리 적 상대 피해 +30%",
            "사장님 10-06 「스토리」(해석 A 확정). 스토리 건물·미니보스(포인트값 200 이상)를 때릴 때 이 유닛의 평타·스킬 최종 피해 ×1.3. 일반 적·보스엔 안 건다. 원작 근거 없음 — 제안값(보잡 ×1.3과 같은 값).",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.StoryDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f } } });

        SkillData run = MakeSkill("어디든지달려갑니다", "어디든지달려갑니다 — 지형무시 이동",
            "사장님 10-06 「지형무시이동」. 이 유닛이 길·바다에 막히지 않고 직선으로 다닌다(UnitData.movementAbility = Flying → FlyingMover). 이 스킬 에셋은 이름·설명만 있는 표시용(효과 0).",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect>() });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { diplomacy, resolve, help, run };
        unit.unitName = Title;
        unit.movementAbility = MovementAbility.Flying;
        unit.trait = null;   // 원작 키드 스킬승급(레벨 2 없음)이라 눌러도 돈만 먹는다 — 단추를 없앤다(특성 에셋은 그대로)
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "(빈 재료)");
        return $"황준석 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.DisplayName}」 · 비행 {unit.movementAbility} · trait {(unit.trait == null ? "없음" : unit.trait.name)} · 입력말 {recipe.chatPhrase} · 재료(변경 없음 확인) {string.Join(", ", names)}";
    }
}
