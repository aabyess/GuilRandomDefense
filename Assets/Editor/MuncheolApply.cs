using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 신문철 「말썽쟁이」(초월_신문철_AP, 마딜) 적용(사장님 10-06): 스킬 5 · 칭호 · 입력말(문철이는못말려). 재료는 이미 같다(확인만). 호출: call MuncheolApply.Apply (다시 불러도 안전).
/// 설계표: Docs/design/SHINMUNCHEOL_DESIGN_2026-10-06.md (사장님 확정: 폭증15% = 평타마다 +1% 누적 최대 +15%).
///  ① 분위기메이커 = 아군+자기 공속 +15% 오라(반경 850) · ② 사고뭉치 = 평타 15% 현재체력 2%(범퍼) · ③ 스노우볼 = 평타마다 피해 +1% 누적 최대 +15%(3초 무공격 초기화) ·
///  ④ 노출중독 = 평타 10% 깡딜 400,000 + 이감 −15%(2.5초) · ⑤ 엄마간식 = 액티브: 내 아군 하나 클릭 → 공속 +100% 10초(쿨 40초).
/// </summary>
static class MuncheolApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_신문철_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_신문철_AP.asset";
    const string Title = "말썽쟁이";
    const string Phrase = "문철이는못말려";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_신문철_AP_{suffix}.asset";
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
        if (unit == null || recipe == null) return "❌ 초월_신문철_AP 유닛·조합식 에셋 없음";

        SkillData mood = MakeSkill("분위기메이커", "분위기메이커 — 공격속도 오라",
            "사장님 10-06 「공속 15%」. 반경 850 안 아군과 자기 공격속도 +15%(원작 지구력 오라 AOae Allies +15%, 원작018_H09I/A0QZ). 반경 850은 임장혁 오라와 같은 값(제안값).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 850f, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.15f, buffId = "MUNCHEOL_AS_AURA" },
                    new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.15f, buffId = "MUNCHEOL_AS_AURA" },
                },
            });

        SkillData trouble = MakeSkill("사고뭉치", "사고뭉치 — 범퍼(현재체력 2%) 발동",
            "사장님 10-06 「범퍼(현재체력2%)」. 평타 15% 확률로 맞은 적 중심 반경 300(제안값) 안 모든 적 현재체력의 2% 마법 피해(범퍼 = 범위 전체 — 사장님 10-06 정정)(박민석 「흑인」 범퍼와 같은 확률 15%, 현재체력이라 끝딜 규칙 대상 아님). 보스·스토리 적도 비례 그대로.",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 0.15f, range = 300f, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetCurrentHpPercent, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.02f, armorIgnoreRatio = 1f },
                },
            });

        SkillData snowball = MakeSkill("스노우볼", "스노우볼 — 평타마다 피해 +1% 누적(최대 +15%)",
            "사장님 10-06 「폭증 15%」(확정 해석 A: 눈덩이). 평타 한 대마다 자기 평타·스킬 최종 피해가 +1%씩 쌓인다(최대 +15%). 마지막 평타 뒤 3초 안 치면 0으로 초기화. 양재모 「공속 누적」과 같은 틀의 피해판 — 원작 근거 없음, 제안값.",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AttackDamageStack, target = SkillTargetKind.Self, stackPerHit = 0.01f, stackCap = 0.15f, stackResetSeconds = 3f },
                },
            });

        SkillData exposure = MakeSkill("노출중독", "노출중독 — 이동속도 감소 + 깡딜(발동)",
            "사장님 10-06 「발동(이감15%, 깡딜)」. 평타 10% 확률로 대상 한 기에게 깡딜 400,000(마법, 원작 초월 확률 깡딜 중앙값 — 박민석 외동Lv.Devil과 같다) + 이동속도 −15%(남는 속도 0.85) 2.5초(박민석 흑인과 같은 지속).",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 0.10f, range = 0f, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 400000f },
                    new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.85f, duration = 2.5f },
                },
            });

        SkillData snack = MakeSkill("엄마간식", "엄마간식 — 아군 하나 공격속도 +100%(액티브)",
            "사장님 10-06 「쿨스킬(선택유닛 공속 100%)」. 칸을 누른 뒤 내 아군 유닛 하나(자기 가능)를 클릭하면 그 유닛 공속 +100% 10초. 쿨타임 40초, 마나 소모 없음(지속 10초·쿨 40초는 바지사장 「분노조절장애」와 같은 제안값). 우클릭 취소.",
            SkillTriggerType.ActiveButton,
            new SkillLevel
            {
                cooldown = 40f, needsAllyClick = true, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 1f, duration = 10f, buffId = "MUNCHEOL_SNACK" },
                },
            });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { mood, trouble, snowball, exposure, snack };
        unit.unitName = Title;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "(빈 재료)");
        return $"신문철 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.DisplayName}」 · 입력말 {recipe.chatPhrase} · 재료(변경 없음 확인) {string.Join(", ", names)}";
    }
}
