using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 강재규 「상호파정보보완관」(초월_강재규_AP, 마딜) 적용(사장님 10-06): 오하라 이식 스킬 6개를 빼고 사장님 스킬로 교체 · 칭호 · 입력말(극악무도악질소악마) · 체력 게이지 40.
/// 호출: call JaegyuApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_JAEGYU_DESIGN_2026-10-06.md. 단일도킹(파괴사상)은 액티브 버튼 틀(구현담당2 ActiveButton)이 들어온 뒤 붙인다.
///  · 3대악질 = 깡딜: 평타 10% · 200000(원작 도플라밍고 일반 1/10·200000), AP(능력 피해는 방어 무시 — 우리 엔진 기본).
///  · 간잽이 = 스턴(체력스킬): 체력 게이지 40 가득 → 범위 525 스턴 2.85초(원작 Robine_skill_2 stomp A09I 525·2.85초, 이 유닛 원작 LIFE 40) → 게이지 0.
///  · 만성피로: 같은 순간(게이지 40) 자기 스턴 3초 → 끝나면 게이지 즉시 가득.
///  · 강약약강 = 끝딜(평타 10% · 대상 잃은 체력 5%, 보스(PV≥200)는 고정 300000) + 패시브 아군발 디버프 개수 비례 피해(개당 +15%, 상한 +60%). 원작 근거 없음 — 제안값.
/// </summary>
static class JaegyuApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_강재규_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_강재규_AP.asset";
    const string Title = "상호파정보보완관";
    const string Phrase = "극악무도악질소악마";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_강재규_AP_{suffix}.asset";
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

    static SkillEffect Hit(SkillEffectBasis basis, float multiplier, SkillEffectTargetCondition condition = SkillEffectTargetCondition.None, float conditionValue = 0f) => new SkillEffect
    {
        kind = SkillEffectKind.Damage, basis = basis, target = SkillTargetKind.SingleTarget,
        damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = multiplier,
        targetCondition = condition, targetConditionValue = conditionValue,
    };

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_강재규_AP 유닛·조합식 에셋 없음";

        SkillData burst = MakeSkill("3대악질", "3대악질 — 깡딜(발동)",
            "사장님 10-06. 평타 10% 확률로 맞은 적에게 고정 200000(원작 도플라밍고 일반 DP_Skill_2 1/10·200000). 마딜 — AP 능력 피해는 방어 무시(우리 엔진 기본).",
            SkillTriggerType.OnHitChance, 0f, 0.10f, 0, SkillGaugeKind.Life,
            Hit(SkillEffectBasis.Flat, 200000f));

        SkillData stun = MakeSkill("간잽이", "간잽이 — 스턴(체력스킬: 체력 게이지 40)",
            "사장님 10-06 「체력스킬 = 스턴」. 체력 게이지(평타 +1)가 40에 차면 범위 525 안 적 스턴 2.85초(원작 Robine_skill_2 stomp A09I 525·2.85초, 이 유닛 원작 LIFE게이지 40) → 게이지 0.",
            SkillTriggerType.OnHitCount, 525f, 1f, 40, SkillGaugeKind.Life,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2.85f });

        SkillData tired = MakeSkill("만성피로", "만성피로 — 체력 게이지가 터지면 자기 스턴 3초 후 게이지 가득",
            "사장님 10-06. 간잽이가 터지는 순간(체력 게이지 40) 자기 자신이 3초 스턴(공격·스킬 정지) → 끝나면 체력 게이지가 즉시 가득(다음 체력스킬 바로 가능).",
            SkillTriggerType.OnHitCount, 0f, 1f, 40, SkillGaugeKind.Life,
            new SkillEffect { kind = SkillEffectKind.SelfStunRefillLifeGauge, target = SkillTargetKind.Self, duration = 3f });

        SkillData finisher = MakeSkill("강약약강_끝딜", "강약약강 — 끝딜(잃은 체력 5%)",
            "사장님 10-06 「끝딜(잃은체력5%)」. 평타 10% 확률로 대상이 잃은 체력(최대−현재)의 5% 피해. 보스(PV≥200)는 비례 대신 고정 300000(원작 보스 분기 — 보스 상한). 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.10f, 0, SkillGaugeKind.Life,
            Hit(SkillEffectBasis.TargetMissingHpPercent, 0.05f, SkillEffectTargetCondition.TargetPointValueLessThan, 200f),
            Hit(SkillEffectBasis.Flat, 300000f, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f));

        SkillData perDebuff = MakeSkill("강약약강_디버프비례", "강약약강 — 아군발 디버프 개수 비례 피해 증가(패시브)",
            "사장님 10-06 「아군에 의해서 받는 디버프 개수 비례 데미지 증가」. 노태현 「아군 이속 감소」 같은 아군발 디버프를 받는 가짓수 × 개당 +15%(상한 +60%) 최종 피해 증가. 원작 근거 없음 — 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Life,
            new SkillEffect { kind = SkillEffectKind.DamagePerAllyDebuff, target = SkillTargetKind.Self, multiplier = 0.15f, bonus = 0.6f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { burst, stun, tired, finisher, perDebuff };
        unit.unitName = Title;
        unit.lifeGaugeMax = 40f;
        unit.lifeGaugeStart = 0f;
        unit.lifeGaugeRegenPerSecond = 0f;
        unit.lifeGaugeCustomHitGain = false;   // 평타 +1
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"강재규 상호파정보보완관 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 체력 게이지 {unit.lifeGaugeMax} · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종 그대로";
    }
}
