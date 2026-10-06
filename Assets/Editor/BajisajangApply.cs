using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 최상호 「바지사장」(초월_최상호_AP, 마딜) 적용(사장님 10-06): 도플라밍고 이식 스킬 10개를 빼고 사장님 스킬 4개로 교체 · 재료 · 입력말(씹덕대마왕) · 특성강화 설명.
/// 호출: call BajisajangApply.Apply (다시 불러도 안전 — 같은 이름 스킬 에셋은 덮어쓴다). 설계표: Docs/design/BAJISAJANG_DESIGN_2026-10-06.md.
///  ① 적응불가의덕력 = 스턴: 평타 15% · 스턴 3.0초(영웅 1.5) — 원작 구주호 A0CS 15%·3.0초.
///  ② 절대공격 = 마나스킬(공속비례 깡딜): 마나 게이지 125 · 600 범위 2,000,000 × (1 + 1.0×(공속−1)), 공속 4배까지 — 원작 두유찬 125·구주호 초록소 2,000,000. 공속 비례는 원작 근거 없음 — 제안값.
///  ③ 절대방어 = 체력스킬(공속비례 스턴): 체력 게이지 50 · 500 범위 스턴 2.0초 × (1 + 0.5×(공속−1)), 공속 3배까지(최대 ×2 = 4초) — 원작 키드 LIFE50 2.0초. 공속 비례는 원작 근거 없음 — 제안값.
///  ④ 분노조절장애(Style : 최상호) = 액티브(버튼): 자기 공속 +100% 10초 · 쿨 40초, 특성 3pt면 쿨 20초(−50%, 사장님 확정) — 원작 근거 없음 — 제안값.
/// </summary>
static class BajisajangApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_최상호_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_최상호_AP.asset";
    const string TraitPath = "Assets/Data/Traits/Trait_초월_최상호_AP.asset";
    const string Phrase = "씹덕대마왕";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_최상호_AP_{suffix}.asset";
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
        var trait = AssetDatabase.LoadAssetAtPath<UnitTraitData>(TraitPath);
        if (unit == null || recipe == null) return "❌ 초월_최상호_AP 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData stun = MakeSkill("적응불가의덕력", "적응불가의덕력 — 스턴(발동)",
            "사장님 10-06 「스턴」. 평타 15% 확률로 맞은 적을 3.0초 스턴(영웅 지속 1.5초) — 원작 초월 구주호 A0CS 15%·3.0초(ahdu 1.5) 기준.",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                triggerChance = 0.15f,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 3f, heroDuration = 1.5f },
                },
            });

        SkillData attack = MakeSkill("절대공격", "절대공격 — 마나스킬(공속 비례 깡딜)",
            "사장님 10-06 「마나스킬(공속비례 깡딜)」. 평타 125타째(마나 게이지 125 — 원작 두유찬 125) 600 범위 2,000,000(원작 구주호 초록소 2,000,000)을 주고, 공속이 높을수록 세진다: 피해 × (1 + 1.0 × (공속 배율 − 1)), 공속 4배까지(최대 8,000,000). 공속 비례는 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                range = 600f, hitCountThreshold = 125, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, triggerChance = 1f,
                effects = new List<SkillEffect>
                {
                    new SkillEffect
                    {
                        kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies,
                        damageType = DamageType.AD, attackType = AttackType.Magic, multiplier = 2000000f, armorIgnoreRatio = 1f,
                        attackSpeedScale = 1f, attackSpeedScaleCap = 4f,
                    },
                },
            });

        SkillData defense = MakeSkill("절대방어", "절대방어 — 체력스킬(공속 비례 스턴)",
            "사장님 10-06 「체력스킬(공속비례 스턴)」 — 두 번째 게이지(체력 게이지)가 차면 스턴이 터진다. 평타 50타째(체력 게이지 50 — 원작 키드·아카이누 LIFE50) 500 범위 스턴 2.0초(원작 키드 2.0초), 공속이 높을수록 길어진다: 지속 × (1 + 0.5 × (공속 배율 − 1)), 공속 3배까지(최대 ×2 = 4초). 공속 비례는 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                range = 500f, hitCountThreshold = 50, resetTo = 0, gaugeKind = SkillGaugeKind.Life, triggerChance = 1f,
                effects = new List<SkillEffect>
                {
                    new SkillEffect
                    {
                        kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2f, heroDuration = 1f,
                        attackSpeedScale = 0.5f, attackSpeedScaleCap = 3f,
                    },
                },
            });

        SkillEffect hasteEffect() => new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 1f, duration = 10f };
        SkillData rage = MakeSkill("분노조절장애", "분노조절장애(Style : 최상호)",
            "사장님 10-06 「액티브스킬(자신 공속 증가)」. 누르면 10초 동안 자기 공속 +100%. 쿨타임 40초(특성 포인트 3개로 강화하면 20초 — 50% 감소), 마나 소모 없음. 공속이 오르면 절대공격·절대방어도 같이 세진다. 원작 근거 없음 — 제안값(원작 공속 선례: 구주호 야마토 +400%·1.25초 / 초록소 +5%·6.85초).",
            SkillTriggerType.ActiveButton,
            new SkillLevel { cooldown = 40f, effects = new List<SkillEffect> { hasteEffect() } },
            new SkillLevel { cooldown = 20f, effects = new List<SkillEffect> { hasteEffect() } });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { stun, attack, defense, rage };
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_노태현"), count = 1 },    // 노태현 사회복무요원
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("변화됨_최상호"), count = 1 },      // 최상호 한남(PM이 unitName을 「한남」으로 바꿈)
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_이은엽"), count = 1 },      // 이은엽 드럼신동
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_박수찬"), count = 1 },      // 박수찬 심각한로리콘
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_배성령"), count = 1 },      // 배성령 해커
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },    // 초월위습(모든 초월 식에 무조건)
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);

        if (trait != null)
        {
            trait.traitName = "바지사장 특성강화";
            trait.description = "사장님 10-06: 특성 포인트 3개로 강화하면 액티브 스킬 「분노조절장애(Style : 최상호)」의 쿨타임이 50% 줄어든다(40초 → 20초, 이 유닛 전용·한 번). 스킬승급형(skillLevelUnlockIndex 1) — 액티브 스킬의 레벨2가 쿨 20초다.";
            trait.costTraitPoints = 3;
            trait.skillLevelUnlockIndex = 1;
            EditorUtility.SetDirty(trait);
        }

        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"바지사장 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")} · 입력말 {recipe.chatPhrase} · 특성 {(trait != null ? trait.traitName : "없음")}";
    }
}
