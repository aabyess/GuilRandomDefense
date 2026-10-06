using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 김정래 「전전교회장」(영원_김정래, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-6).
/// 호출: call EternalJeonglaeApply.Apply (다시 불러도 안전). 스킬(원작 우타·버기 이식 스킬은 전부 뺀다):
///  · 리더십 = 반경 850 아군(자기 포함) 공속 +15%·공격력 +10% 오라 · 말의무게 = 반경 850 적 방어 −30 오라(원작 버기 A0M7 −30)
///  · 언변 = 발동 셋(스킬 3개, 이름은 「언변 — …」): 평타 1/6 반경 405 스턴 1.0초 · 평타 1/8 이감(남는 속도 0.5·3초) · 평타 1/10 반경 850 아군 공격력 +20% 5초
///  · 최고의연설 = 마나 게이지 100 → 반경 850 아군(자기 포함) 공격력 +20% 10초 + 적 방깍 9(암브 한 번)
///  · 카리스마 = 유닛회유(평타 0.8%·사거리 700, 불멸 김용태·정준영과 같은 회유 유닛 Summon_회유_적) + 패시브 회유 1기당 이 유닛 피해 +0.1%(상한 +30%, SkillEffectKind.DamagePerRecruit)
///  · 깊은포용력 = 반경 850 아군(자기 포함)이 받는 아군발 디버프 무효(DispelAllyDebuffs — 임장혁 고충해소와 같은 효과)
/// 칭호 「전전교회장」 · 입력말 「언변술사」 · trait 비움. 재료(전설 김정래 · 히든 황정기 · 히든 정기훈)는 이미 원문과 맞다.
/// </summary>
static class EternalJeonglaeApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_김정래.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_김정래.asset";
    const string RecruitPath = "Assets/Data/Units/Summons/Summon_회유_적.asset";
    const string Title = "전전교회장";
    const string Phrase = "언변술사";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_김정래_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillEffect AsAura(float pct, string id, SkillTargetKind t) => new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = t, multiplier = pct, buffId = id };
    static SkillEffect AdAura(float pct, string id, SkillTargetKind t, float duration = 0f) => new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = t, multiplier = pct, buffId = id, duration = duration };

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var recruit = AssetDatabase.LoadAssetAtPath<UnitData>(RecruitPath);
        if (unit == null || recipe == null) return "❌ 영원_김정래 유닛·조합식 에셋 없음";
        if (recruit == null) return "❌ 회유 유닛 에셋(Summon_회유_적) 없음 — ImmortalApplyC.Apply를 먼저";

        SkillData leadership = MakeSkill("리더십", "리더십 — 공속·공증 오라",
            "사장님 10-06 「공속」·「공증」(확정 해석: 오라). 반경 850 안 아군(자기 포함) 공격속도 +15%·공격력 +10%(원작 우타 A16U +15%·버기 +60% 사이, 제안값).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            AsAura(0.15f, "JEONG_LEAD_AS", SkillTargetKind.Allies), AsAura(0.15f, "JEONG_LEAD_AS", SkillTargetKind.Self),
            AdAura(0.10f, "JEONG_LEAD_AD", SkillTargetKind.Allies), AdAura(0.10f, "JEONG_LEAD_AD", SkillTargetKind.Self));

        SkillData weight = MakeSkill("말의무게", "말의무게 — 방깍 오라(−30)",
            "사장님 10-06 「방깍」(확정 해석: 오라, 모든 적). 반경 850 안 적 방어 −30(원작 버기 A0M7 −30·핸콕 A0IM −45).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -30f, buffId = "JEONG_ARMOR" });

        SkillData eloquenceStun = MakeSkill("언변_스턴", "언변 — 발동 스턴",
            "사장님 10-06 「발동(스턴, 이감, 공증)」 중 스턴. 평타 1/6 확률로 반경 405 안 적 스턴 1.0초(팔레트).",
            SkillTriggerType.OnHitChance, 405f, 1f / 6f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 1f });
        SkillData eloquenceSlow = MakeSkill("언변_이감", "언변 — 발동 이감",
            "사장님 10-06 「발동(스턴, 이감, 공증)」 중 이감. 평타 1/8 확률로 맞은 적 이동속도 −50%(남는 속도 0.5) 3초(팔레트).",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.5f, duration = 3f });
        SkillData eloquenceBuff = MakeSkill("언변_공증", "언변 — 발동 공증",
            "사장님 10-06 「발동(스턴, 이감, 공증)」 중 공증. 평타 1/10 확률로 반경 850 안 아군(자기 포함) 공격력 +20% 5초(원작 버기 e0AJ 900 안 아군 +33% 4.5초 근처, 제안값).",
            SkillTriggerType.OnHitChance, 850f, 0.1f, 0, SkillGaugeKind.Mana,
            AdAura(0.20f, "JEONG_ELOQ_AD", SkillTargetKind.Allies, 5f), AdAura(0.20f, "JEONG_ELOQ_AD", SkillTargetKind.Self, 5f));

        SkillData speech = MakeSkill("최고의연설", "최고의연설 — 마나 스킬(공증 20% + 방깍 9)",
            "사장님 10-06 「마나스킬(공증 20%, 방깍 9)」. 마나 게이지(평타 +1) 100(이 유닛 원작 영원 마나)에 차면 반경 850 안 아군(자기 포함) 공격력 +20% 10초 + 반경 안 적 방어 −9(Aid1 아머브레이크 한 번 — 영구 누적, 합계 상한 −75) → 게이지 0. 값은 제안값.",
            SkillTriggerType.OnHitCount, 850f, 1f, 100, SkillGaugeKind.Mana,
            AdAura(0.20f, "JEONG_SPEECH_AD", SkillTargetKind.Allies, 10f), AdAura(0.20f, "JEONG_SPEECH_AD", SkillTargetKind.Self, 10f),
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.Enemies, multiplier = 9f });

        SkillData charisma = MakeSkill("카리스마", "카리스마 — 유닛회유(평타 0.8%) + 회유 1기당 공격력 +0.1%",
            "사장님 10-06 「유닛회유(공증 0.1%, 팔기가능)」(확정). 평타 1회당 0.8% 확률로 사거리 700 안 가장 가까운 일반 적 1기를 죽이지 않고 내 유닛으로 바꾼다(동시 5기, 공격력 = 이 유닛 평타 10%, 판매 버튼으로 팔면 37% 랜덤위습·그중 40% +100엔·목재 1 — 불멸 김용태·정준영과 같은 회유 시스템). 패시브: 살아 있는 회유 유닛 1기당 이 유닛 피해 +0.1%(상한 +30% — 「공증 0.1%」를 회유 수 비례로 읽음).",
            SkillTriggerType.OnHitChance, 700f, 0.008f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.RecruitEnemy, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { recruit }, multiplier = 0.10f, bonus = 5f },
            new SkillEffect { kind = SkillEffectKind.DamagePerRecruit, target = SkillTargetKind.Self, multiplier = 0.001f, bonus = 0.3f });

        SkillData embrace = MakeSkill("깊은포용력", "깊은포용력 — 디버프 감소(오라)",
            "사장님 10-06 「디버프감소」. 반경 850 안 아군(자기 포함)이 받는 아군발 디버프(이속 감소·공격력 감소)를 무시한다 — 임장혁 고충해소와 같은 효과(DispelAllyDebuffs 공유, 이 유닛이 건 디버프는 제외 규칙 그대로).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Allies, multiplier = 1f, buffId = "JEONG_DISPEL" },
            new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Self, multiplier = 1f, buffId = "JEONG_DISPEL" });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { leadership, eloquenceStun, eloquenceSlow, eloquenceBuff, speech, weight, charisma, embrace };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 100f;
        unit.manaGaugePerMana = 1f;
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"김정래 전전교회장 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종 그대로{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
