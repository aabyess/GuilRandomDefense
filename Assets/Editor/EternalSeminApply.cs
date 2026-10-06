using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 조세민 「감성충만국밥악질가수」(영원_조세민, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-2).
/// 호출: call EternalSeminApply.Apply (다시 불러도 안전). 스킬 6(원작 핸콕 이식 스킬은 전부 뺀다):
///  · 라이브콘서트 = 마나 스킬(공속): 마나 게이지 100 → 반경 850 아군(자기 포함) 공속 +25% 12초 → 게이지 0 — 꺼지지않는열기(「스킬발동시레벨업」): 발동할 때마다 +5%p, 최대 10회(+75% 합계 한도 없이 25+50=75%)
///  · 폭언 = 마방깍(평타 1/8 · Aegr +5) · 말의상처 = 단일스턴(평타 1/6 · 1.0초) · 프레임씌우기 = 단일증폭(평타 1/8 · 대상 받는 피해 증가 스택 Aisr +5) · 축복받은땅 = 마젠(반경 850 주변 아군 마나 게이지 +3/초, UnitData.manaAura)
/// 칭호 「감성충만국밥악질가수」 · 입력말 「타락한신의추종자」 · **조합 결과는 180초 뒤에 나온다**(CombineRecipe.resultDelaySeconds — 재료는 즉시 소모). 재료 5칸은 이미 원문과 맞다.
/// </summary>
static class EternalSeminApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_조세민.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_조세민.asset";
    const string Title = "감성충만국밥악질가수";
    const string Phrase = "타락한신의추종자";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_조세민_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 영원_조세민 유닛·조합식 에셋 없음";

        SkillEffect Speed(SkillTargetKind t) => new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = t, multiplier = 0.25f, duration = 12f, buffId = "SEMIN_LIVE", castCountBonus = 0.05f, castCountCap = 10 };
        SkillData concert = MakeSkill("라이브콘서트", "라이브콘서트 — 마나 스킬(아군 공속) · 발동할 때마다 레벨업",
            "사장님 10-06 「마나스킬(공속)」 + 「스킬발동시레벨업(마나스킬)×Lv」(권장 확정). 마나 게이지(평타 +1) 100(원작 영원 마나 100~175 중 하단)에 차면 반경 850 안 아군(자기 포함) 공속 +25% 12초 → 게이지 0. 발동할 때마다 레벨이 올라 공속이 +5%p씩 커진다(최대 10회 = +75%) — 「×Lv」를 곱이 아니라 레벨당 가산으로 읽었다(곱이면 Lv 10에 ×10 폭주). 값은 제안값.",
            SkillTriggerType.OnHitCount, 850f, 1f, 100, Speed(SkillTargetKind.Allies), Speed(SkillTargetKind.Self));
        SkillData heat = MakeSkill("꺼지지않는열기", "꺼지지않는열기 — 마나 스킬 발동시 레벨업(이름만)",
            "사장님 10-06 이름 6개 중 하나 — 「스킬발동시레벨업」 패시브(권장 확정). 라이브콘서트가 터질 때마다 공속 효과가 +5%p씩 커진다(최대 10회). 효과는 라이브콘서트 안(SkillEffect.castCountBonus)에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f, 0);
        SkillData abuse = MakeSkill("폭언", "폭언 — 마방깍(발동)",
            "사장님 10-06 「마방깍」. 평타 1/8 확률로 맞은 적 한 기의 마법 방어를 Aegr +5 낮춘다(박민석 외동 선례).",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0,
            new SkillEffect { kind = SkillEffectKind.AegrStack, target = SkillTargetKind.SingleTarget, multiplier = 5f });
        SkillData wound = MakeSkill("말의상처", "말의상처 — 단일 스턴(발동)",
            "사장님 10-06 「단일스턴」. 평타 1/6 확률로 맞은 적 한 기를 1.0초 스턴(팔레트).",
            SkillTriggerType.OnHitChance, 0f, 1f / 6f, 0,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1f });
        SkillData frame = MakeSkill("프레임씌우기", "프레임씌우기 — 단일 증폭(발동)",
            "사장님 10-06 「단일증폭」(적이 받는 피해 증가 스택). 평타 1/8 확률로 맞은 적 한 기에 증폭 스택(Aisr) +5.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0,
            new SkillEffect { kind = SkillEffectKind.AisrStack, target = SkillTargetKind.SingleTarget, multiplier = 5f });
        SkillData blessed = MakeSkill("축복받은땅", "축복받은땅 — 마젠(주변 아군 마나 게이지 +3/초)",
            "사장님 10-06 「마젠」(불멸 고도현 확정과 같은 읽기: 반경 850 주변 아군(자기 포함) 마나 게이지 재생 오라 +3/초). 값은 UnitData.manaAuraRegenPerSecond·manaAuraRange에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f, 0);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { concert, heat, abuse, wound, frame, blessed };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 100f;
        unit.manaGaugePerMana = 1f;
        unit.manaAuraRegenPerSecond = 3f;
        unit.manaAuraRange = 850f;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        recipe.resultDelaySeconds = 180f;   // 「180초뒤에 생성」
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"조세민 감성충만국밥악질가수 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 마젠 {unit.manaAuraRegenPerSecond}/초 · 조합 결과 {recipe.resultDelaySeconds}초 뒤 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종 그대로{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
