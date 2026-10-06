using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 노태현 「여동생살해자」(초월_노태현_AP, 마딜) 적용(사장님 10-06): 브룩 이식 스킬 7개를 빼고 사장님 스킬로 교체 · 칭호 · 재료 · 입력말(초구일급절망).
/// 호출: call NotaehyunApply.Apply (다시 불러도 안전 — 같은 이름 스킬 에셋은 덮어쓴다). 설계표: Docs/research/TRANSCEND_NOTAEHYUN_DESIGN_2026-10-06.md.
///  · 반사회적인격 = 디버프(단점): 반경 600 안 아군 이속 −20%(AllyMoveSpeedDebuff). 최윤서 강화가 켜지면 100% 제거. 원작 근거 없음 — 제안값.
///  · 하체부실 = 이동속도감소: 평타 12.5% · 남는 속도 0.6 · 3초(원작 초월 양재모 로키포트 1/8·0.6·3.0).
///  · 돌발행동 = 깡딜: 평타 10% · 400000(신 기준 감사로 200000→400000, 원작 초월 확률 깡딜 중앙값).
///  · 시너지폭발 = 스플래시(UnitData.attackSplashRadius 300) + 폭발증폭(스플래시 피해 ×1.5, 원작 근거 없음 — 제안값).
///  · 가리지않는수단과방법 = 체력스킬(마나 게이지 50타째, 대상 현재체력 25%·보스는 고정 300000 — 원작 김만경·키드 LIFE50/도플 25%/보스 고정 분기) + 보잡(보스 ×1.3 제안값).
///  · 방어 무시: 위 피해 효과는 평소 방어를 따르고, 「최윤서 강화」(영구 버프 YOONSEO_ENHANCED)가 켜지면 방어 무시(armorIgnoreRatio 1).
/// </summary>
static class NotaehyunApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_노태현_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_노태현_AP.asset";
    const string Title = "여동생살해자";
    const string Phrase = "초구일급절망";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, float cooldown, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_노태현_AP_{suffix}.asset";
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
            new SkillLevel { cooldown = cooldown, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillEffect Hit(SkillEffectBasis basis, float multiplier, SkillEffectTargetCondition condition = SkillEffectTargetCondition.None, float conditionValue = 0f) => new SkillEffect
    {
        kind = SkillEffectKind.Damage, basis = basis, target = SkillTargetKind.SingleTarget,
        damageType = DamageType.AD, attackType = AttackType.Magic, multiplier = multiplier,
        targetCondition = condition, targetConditionValue = conditionValue,
        armorIgnoreRatio = 1f, armorIgnoreRequiresBuff = UnitAttacker.YoonseoBuffId,
    };

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_노태현_AP 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData antisocial = MakeSkill("반사회적인격", "반사회적인격 — 주변 아군 이동속도 감소(단점)",
            "사장님 10-06 「디버프: 아군 이속 감소」. 반경 600 안 아군(나 제외)의 이동속도 −20% — 같은 디버프는 가장 센 하나만. 「최윤서 강화」를 켜면 100% 없어진다. 원작 근거 없음 — 제안값(범위는 원작 오라 500~925 중 하단).",
            SkillTriggerType.Aura, 600f, 1f, 0f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.AllyMoveSpeedDebuff, target = SkillTargetKind.Allies, multiplier = 0.2f, buffId = "NOTAE_ALLY_SLOW" });

        SkillData slow = MakeSkill("하체부실", "하체부실 — 이동속도감소(발동)",
            "사장님 10-06. 평타 12.5% 확률로 맞은 적 이동속도 −40%(남는 속도 0.6) 3초 — 원작 초월 양재모 AD 「로키포트」 1/8·0.6·3.0초 기준.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.6f, duration = 3f });

        SkillData burst = MakeSkill("돌발행동", "돌발행동 — 깡딜(발동)",
            "사장님 10-06. 평타 10% 확률로 맞은 적에게 고정 400000(사장님 10-06 신 기준 감사: 원작 초월 확률 깡딜 중앙값 40만, 처음엔 도플라밍고 일반 200000). 평소엔 방어를 따르고 「최윤서 강화」가 켜지면 방어 무시.",
            SkillTriggerType.OnHitChance, 0f, 0.10f, 0f, 0, SkillGaugeKind.Mana,
            Hit(SkillEffectBasis.Flat, 400000f));

        SkillData synergy = MakeSkill("시너지폭발", "시너지폭발 — 스플래시 + 폭발증폭",
            "사장님 10-06. 평타 범위 피해(UnitData.attackSplashRadius 300 — 최상호 구일과 같은 반경) + 폭발증폭: 범위 피해량 ×1.5(주 대상 평타는 그대로). 폭발증폭은 원작 근거 없음 — 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.SplashDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.5f });

        SkillData healthSkill = MakeSkill("가리지않는수단과방법_체력스킬", "가리지않는수단과방법 — 체력스킬(현재체력 비례)",
            "사장님 10-06 「체력스킬(현재체력)」 = 적 현재 체력의 N%. 평타 50타째(원작 초월 체력 스킬 김만경·키드 LIFE게이지 50) 대상 현재체력 25%(원작 도플라밍고 일반 25%, PV<200) — 보스(PV≥200)는 비례 대신 고정 300000(원작 보스 분기 Hidden1 300,000).",
            SkillTriggerType.OnHitCount, 0f, 1f, 0f, 50, SkillGaugeKind.Mana,
            Hit(SkillEffectBasis.TargetCurrentHpPercent, 0.25f, SkillEffectTargetCondition.TargetPointValueLessThan, 200f),
            Hit(SkillEffectBasis.Flat, 300000f, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f));

        SkillData boss = MakeSkill("가리지않는수단과방법_보잡", "가리지않는수단과방법 — 보잡(보스 상대 피해 증가)",
            "사장님 10-06 「보잡」 = 보스 잡기. 보스 상대 평타·스킬 최종 피해 ×1.3 — 원작에 보스 전용 피해 증가 능력이 없어 원작 근거 없음, 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.BossDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { antisocial, slow, burst, synergy, healthSkill, boss };
        unit.unitName = Title;
        unit.attackSplashRadius = 300f;
        unit.manaMax = 50f;   // 체력스킬 카운터(평타 +1, 50타째 발동) — 브룩 마나 115는 버린다
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_노태현"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("히든_황정기"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_강주혁"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_양재모"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_최준우"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        string missing = string.Join(", ", recipe.ingredients.FindAll(i => i.unit == null).ConvertAll(i => "null"));
        return $"노태현 여동생살해자 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 스플래시 {unit.attackSplashRadius} · 재료 {recipe.ingredients.Count}종 · 입력말 {recipe.chatPhrase}" + (missing.Length > 0 ? $" · ⚠️ 빈 재료 {missing}" : "");
    }
}
