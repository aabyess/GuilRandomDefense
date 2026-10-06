using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 임장혁 「짱스」(초월_임장혁_AD, 마딜 · 지원형) 적용(사장님 10-06): 루치 이식 스킬 5개를 빼고 사장님 스킬로 교체 · 칭호 · 재료 · 입력말(훌륭한장애인) · 마나 게이지/오라 · 특성 2pt 이간질 제거.
/// 호출: call JanghyukApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_JANGHYUK_DESIGN_2026-10-06.md. 사장님 답(10-06): 보조딜은 역할 설명이라 스킬 없음 · 「스킬피해증가(아군공격력비례)」 → 주변 아군 공격력 증가 오라 · 「범위증폭」 → 주변 아군(자기 포함) 스킬 피해 증가.
///  · 영혼없는칭찬 = 아군 공속 증가(발동: 평타 10% · 반경 850 · +30% 5초) + 아군 공격력 증가 오라(반경 850 · +25% — 원작 공증 오라 A0V8 ACac 0.25·850).
///  · 어지러운언행 = 스턴 0.5초(평타 20%, 사장님 값 · 확률은 김경현 AP A0X4 20%).
///  · 가스라이팅 = 스킬 피해 증가 오라(아군+자기, 반경 850 · +25% — 원작 근거 없음, 제안값).
///  · 이간질 = 디버프: 반경 600 아군 공격력 −10%(노태현 아군 이속 감소 틀 · 제안값). 특성 포인트 2로 영구 제거(스킬 레벨 2에서 효과 0).
///  · 신나는연주 = 마나 재생 오라: UnitData.manaAura 초당 +2 · 반경 850(자기 포함).
///  · 악보완성! = 마나스킬: 마나 게이지 120(평타 +1) 가득 → 반경 850 자신 제외 아군 공속 +40% 8초(원작 마나 게이지 초월 115~160).
///  · 고충해소 = 디버프 해제 오라: 반경 850 아군이 받는 아군발 디버프 무시(자기 이간질은 제외 — PM 기본안).
/// </summary>
static class JanghyukApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_임장혁_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_임장혁_AD.asset";
    const string Title = "짱스";
    const string Phrase = "훌륭한장애인";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[][] levelEffects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_임장혁_AD_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillData>();
            AssetDatabase.CreateAsset(skill, path);
        }
        skill.skillName = skillName;
        skill.description = description;
        skill.triggerType = trigger;
        skill.levels = new List<SkillLevel>();
        foreach (SkillEffect[] effects in levelEffects)
            skill.levels.Add(new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) });
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillEffect[] One(params SkillEffect[] e) => e;

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_임장혁_AD 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData praiseSpeed = MakeSkill("영혼없는칭찬_공속", "영혼없는칭찬 — 아군 공격속도 증가(발동)",
            "사장님 10-06 「아군유닛공격속도증가(발동)」. 평타 10% 확률로 반경 850 안 아군 공속 +30% 5초(원작 아군 공속 오라 +15~20% — 배성령 A0V4·최상호 A0WL, 발동형 수치는 원작 근거 없음 — 제안값).",
            SkillTriggerType.OnHitChance, 850f, 0.10f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.30f, duration = 5f, buffId = "JANG_PRAISE_AS" }));

        SkillData praiseAttack = MakeSkill("영혼없는칭찬_공격력오라", "영혼없는칭찬 — 아군 공격력 증가(오라)",
            "사장님 10-06 「스킬피해증가(아군유닛공격력비례)」 → 주변 아군의 공격력 증가. 반경 850 안 아군과 자기 공격력 +25%(원작 공증 오라 A0V8 ACac 0.25·반경 850, 참고표 AttackPowerBuffPercent).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.25f, buffId = "JANG_ATK_AURA" },
                new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.25f, buffId = "JANG_ATK_AURA" }));

        SkillData dizzy = MakeSkill("어지러운언행", "어지러운언행 — 스턴 0.5초(발동)",
            "사장님 10-06 「스턴(0.5)」. 평타 20% 확률로 맞은 적 스턴 0.5초(원작 초월 김경현 AP A0X4 20%·0.5초). 보조딜은 역할 설명이라 스킬 없음(사장님).",
            SkillTriggerType.OnHitChance, 0f, 0.20f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 0.5f }));

        SkillData gaslight = MakeSkill("가스라이팅", "가스라이팅 — 스킬 피해 증가(아군·자기 오라)",
            "사장님 10-06 「범위증폭 → 스킬 데미지 증가」. 반경 850 안 아군과 자기가 내는 스킬 피해(평타 제외) +25%. 원작 근거 없음 — 제안값.",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AllySkillDamageBonus, target = SkillTargetKind.Allies, multiplier = 0.25f, buffId = "JANG_SKILLDMG" },
                new SkillEffect { kind = SkillEffectKind.AllySkillDamageBonus, target = SkillTargetKind.Self, multiplier = 0.25f, buffId = "JANG_SKILLDMG" }));

        // 이간질 — 레벨 1: 디버프 · 레벨 2(특성 2pt): 효과 0(영구 제거)
        SkillData divide = MakeSkill("이간질", "이간질 — 주변 아군 공격력 감소(디버프, 특성으로 제거)",
            "사장님 10-06 「디버프: 이간질 = 아군유닛공격력감소」. 반경 600 안 아군 공격력 −10%(노태현 아군 이속 감소 틀 · 제안값). 고충해소(디버프 해제)는 자기 이간질을 지우지 않는다 — 특성 포인트 2개로 강화하면(스킬승급형 레벨 2) 영구히 없어진다.",
            SkillTriggerType.Aura, 600f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = -0.10f, buffId = "JANG_DIVIDE" }),
            One(new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0f, buffId = "JANG_DIVIDE" }));

        SkillData music = MakeSkill("신나는연주", "신나는연주 — 마나 재생 오라",
            "사장님 10-06 「마나재생오라」. 반경 850 안 같은 주인 유닛(자기 포함) 마나 게이지 재생 초당 +2(UnitData.manaAuraRegenPerSecond · 원작 마나 오라 값은 로스터에 이미 쓰인다). 이 스킬 자체는 이름표(ApplyBuff).",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.ApplyBuff, target = SkillTargetKind.Self, buffId = "JANG_MANA_AURA" }));

        SkillData score = MakeSkill("악보완성", "악보완성! — 마나스킬(자신 제외 아군 공속 증가)",
            "사장님 10-06 「마나스킬(자신제외아군유닛공격속도증가)」. 마나 게이지(평타 +1)가 120에 차면 반경 850 안 자신 제외 아군 공속 +40% 8초 → 게이지 0(원작 마나 게이지 초월 115~160, 수치는 제안값).",
            SkillTriggerType.OnHitCount, 850f, 1f, 120, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.40f, duration = 8f, buffId = "JANG_SCORE_AS" }));

        SkillData dispel = MakeSkill("고충해소", "고충해소 — 디버프 해제(오라)",
            "사장님 10-06 「디버프해제」. 반경 850 안 아군이 받는 아군발 디버프(이속 감소·공격력 감소)를 무시한다. 이 유닛 자신이 건 이간질은 지우지 않는다(PM 기본안 — 특성 2pt 제거가 의미 있게). 이재윤 「긍정의힘」과 같은 효과(DispelAllyDebuffs 공유).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Allies, multiplier = 1f, buffId = "JANG_DISPEL" },
                new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Self, multiplier = 1f, buffId = "JANG_DISPEL" }));   // 자기도(10-06 실측: 임장혁 본인 이속이 노태현 디버프로 100.2에 남았다)

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { praiseSpeed, praiseAttack, dizzy, gaslight, divide, music, score, dispel };
        unit.unitName = Title;
        unit.manaMax = 120f;                 // 악보완성! 마나 게이지(평타 +1)
        unit.manaGaugePerMana = 1f;          // 0이면 게이지 상한이 0이 돼 악보완성!이 영영 못 찬다(10-06 실측에서 발견)
        unit.manaAuraRegenPerSecond = 2f;    // 신나는연주 — 마나 재생 오라
        unit.manaAuraRange = 850f;
        unit.manaAuraBuffId = "JANG_MANA_AURA";
        unit.manaAuraIncludesSelf = true;
        EditorUtility.SetDirty(unit);

        // 특성: 능력교체형이 아니라 이간질 영구 제거(스킬승급형 레벨 2) 2pt, 한 번.
        string traitNote = "특성 없음";
        UnitTraitData trait = unit.trait;
        if (trait != null)
        {
            trait.traitName = "짱스 특성강화";
            trait.description = "사장님 10-06: 특성 포인트 2개로 강화하면 「이간질」(주변 아군 공격력 감소 디버프)이 영구히 없어진다(이 유닛 전용·한 번). 스킬승급형(skillLevelUnlockIndex 1) — 이간질 스킬 레벨 2의 효과가 0이다.";
            trait.costTraitPoints = 2;
            trait.skillLevelUnlockIndex = 1;
            trait.replacementSkill = null;
            EditorUtility.SetDirty(trait);
            traitNote = $"특성 「{trait.traitName}」 {trait.costTraitPoints}pt";
        }

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_임장혁"), count = 1 },   // 임장혁 짱스파단장
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_이용민"), count = 1 },     // 이용민 만삭
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_정내연"), count = 1 },     // 정내연 그림쟁이
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_배성령"), count = 1 },     // 배성령 해커
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_이현빈"), count = 1 },     // 이현빈 파파라치
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },   // 초월위습
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"임장혁 짱스 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax}·마나 오라 +{unit.manaAuraRegenPerSecond}/초 · {traitNote} · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")} · 입력말 {recipe.chatPhrase}";
    }
}
