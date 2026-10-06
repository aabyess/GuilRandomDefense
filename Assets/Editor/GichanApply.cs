using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 박기찬 「최고의선생님」(초월_박기찬_AD, 물딜 · 보조) 적용(사장님 10-06 13번째): 원작 이식 스킬 3개를 빼고 사장님 스킬로 교체 · 칭호 · 입력말(섹스킹) · 체력 게이지 50 · 멀티샷 4(총 4마리). trait 비움(능력교체형·스킬 승급형 둘 다 새 스킬엔 효과 없음).
/// 호출: call GichanApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_GICHAN_DESIGN_2026-10-06.md (사장님 확정 반영).
///  · 벗어야흥이나지 = 방깍(40) 범위 오라(반경 600 적 방어 −40) + 암브(단일 7: 평타 12.5% 대상 방어 −7, ArmorBreak).
///  · 파트너 = 주변 아군 공속 +50% 오라(반경 850, 아군+자기, 사장님 확정 — 액티브 아님).
///  · 강간 = 단일 스턴(평타 20% · 1.5초, 제안값).
///  · 무언가의분출 = 체력 스킬 강력한 둔화(체력 게이지 50 · 범위 405 · 남는 속도 0.25 4초, 제안값).
///  · 만능플레이 = 멀티샷 4(UnitData.attackExtraTargets 3 + 반경 = 사거리) — 스킬 에셋은 이름표(ApplyBuff)만.
/// </summary>
static class GichanApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_박기찬_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_박기찬_AD.asset";
    const string Title = "최고의선생님";
    const string Phrase = "섹스킹";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_박기찬_AD_{suffix}.asset";
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
        if (unit == null || recipe == null) return "❌ 초월_박기찬_AD 유닛·조합식 에셋 없음";

        SkillData armorAura = MakeSkill("벗어야흥이나지_방깍", "벗어야흥이나지 — 방깍(40) 범위 오라",
            "사장님 10-06 「방깍(40)」 = 주변 적 방어 감소 오라(김만경 45·구주호 30과 같은 해석, PM 확정). 반경 600 안 적 방어 −40(범위를 나가면 복구). 원작 방깎(AId1) 합계 상한 −75 안(암브 −7과 합쳐 −47).",
            SkillTriggerType.Aura, 600f, 1f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -40f });

        SkillData armorBreak = MakeSkill("벗어야흥이나지_암브", "벗어야흥이나지 — 암브(단일 대상 방어 −7, 발동)",
            "사장님 10-06 「암브(단일7)」 = 아머브레이크(평타 발동 단일 대상 방어 감소, PM·사장님 확정 해석). 평타 12.5%(1/8)로 맞은 대상 방어 −7 — 확률은 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 7f });

        SkillData partner = MakeSkill("파트너", "파트너 — 주변 아군 공격속도 +50% 오라",
            "사장님 10-06 「공속(지정 공속 50%)」 → 질문지 답: 주변 아군 공속 +50% 오라(액티브 아님). 반경 850 안 아군과 자기 공속 +50%(버프 ID 하나라 같은 오라끼리 안 겹침). 반경은 임장혁 칭찬 오라와 같은 값 — 제안값.",
            SkillTriggerType.Aura, 850f, 1f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.5f, buffId = "GICHAN_PARTNER" },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.5f, buffId = "GICHAN_PARTNER" });

        SkillData stun = MakeSkill("강간", "강간 — 단일 스턴(발동)",
            "사장님 10-06 「스턴(단일)」. 평타 20% 확률로 맞은 적 스턴 1.5초(원작 초월 단일 스턴 27건 확률 15~40%·0.5~5초, 엄태웅 포박·임장혁 어지러운언행 20% 선례) — 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.20f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1.5f });

        SkillData slow = MakeSkill("무언가의분출", "무언가의분출 — 체력스킬(체력 게이지 50): 강력한 둔화",
            "사장님 10-06 「체력스킬(강력한둔화)」. 체력 게이지(평타 +1)가 50에 차면 범위 405 안 적 이동속도 −75%(남는 속도 0.25) 4초 → 게이지 0. 원작 LIFE 게이지 임계 40·50·85 · 이감 발동형 0.25~0.75·2~5초, 양재모 다한증(0.5·3초)보다 강하게 — 제안값.",
            SkillTriggerType.OnHitCount, 405f, 1f, SkillGaugeKind.Life, 50,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.25f, duration = 4f });

        SkillData multi = MakeSkill("만능플레이", "만능플레이 — 멀티샷 4",
            "사장님 10-06 「멀티샷4」 = 평타가 주 대상 + 곁의 적 3기(총 4마리)에게 같은 피해(UnitData.attackExtraTargets 3, 반경 = 사거리). 이 스킬은 이름표(ApplyBuff)만이고 실제 효과는 유닛 필드.",
            SkillTriggerType.Aura, 0f, 1f, SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.ApplyBuff, target = SkillTargetKind.Self, buffId = "GICHAN_MULTISHOT" });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { armorAura, armorBreak, partner, stun, slow, multi };
        unit.unitName = Title;
        unit.trait = null;
        unit.lifeGaugeMax = 50f;
        unit.lifeGaugeStart = 0f;
        unit.lifeGaugeRegenPerSecond = 0f;
        unit.lifeGaugeCustomHitGain = false;   // 평타 +1
        unit.attackExtraTargets = 3;
        unit.attackExtraTargetRadius = unit.attackRange;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"박기찬 최고의선생님 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · trait 비움 · 체력 게이지 {unit.lifeGaugeMax} · 멀티샷 +{unit.attackExtraTargets}(반경 {unit.attackExtraTargetRadius:F0}) · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
