using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 김건 「잃어버린웃음보따리」(초월_김건_AP, 물딜+마딜) 적용(사장님 10-06 12번째, 설계표 Docs/research/TRANSCEND_GUN_DESIGN_2026-10-06.md — 질문 7개 전부 제안대로 확정).
/// 호출: call GunApply.Apply (다시 불러도 안전). 스킬 6: 제2의삶(공속 오라 +20%) · 포식(평타 1/16 유닛삭제 + 카운트) · 구건(체력 게이지 50 → 형태변환, 8초+카운트×0.5초 상한 15초, 자기 공속 −30%)
/// · 깔아뭉개기(형태 중 평타 1/4 범위 400 AP 2,000,000) · 배치기(형태 중 평타 1/6 범위 405 스턴 2초) · 힘의작용과반작용(형태 중 평타 1/5 경로 뒤로 160 넉백, 보스 면역).
/// 변화됨_김건 칭호 「New건」 · 로스터 damageType 3(물딜+마딜) · 입력말 「돌아와줘,건아!」.
/// </summary>
static class GunApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_김건_AP.asset";
    const string MaterialPath = "Assets/Data/Units/Roster/변화됨_김건.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_김건_AP.asset";
    const string Title = "잃어버린웃음보따리";
    const string MaterialTitle = "New건";
    const string Phrase = "돌아와줘,건아!";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, string requiredBuff, SkillGaugeKind gauge, int threshold, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_김건_AP_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, gaugeKind = gauge, hitCountThreshold = threshold, resetTo = 0, requiredBuffId = requiredBuff, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var material = AssetDatabase.LoadAssetAtPath<UnitData>(MaterialPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || material == null || recipe == null) return "❌ 초월_김건_AP·변화됨_김건·조합식 에셋 없음";

        SkillData life2 = MakeSkill("제2의삶", "제2의삶 — 공격속도 오라 +20%",
            "사장님 10-06 「공격속도증가오라」. 반경 850 안 아군과 자기 공격속도 +20%(원작 최상호 A0WL +20%·배성령 A0V4 +15%, 참고표 AttackSpeedBuffPercent).",
            SkillTriggerType.Aura, 850f, 1f, "", SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.20f, buffId = "GUN_AS_AURA" },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.20f, buffId = "GUN_AS_AURA" });

        SkillData eat = MakeSkill("포식", "포식 — 유닛삭제(평타 1/16)",
            "사장님 10-06 「유닛삭제(발동)」. 평타 1/16 확률로 사거리 안 가장 가까운 일반 적(보스·PV≥200·B06B 제외) 1기를 즉사시키고 유닛삭제 카운트 +1(형태변환 때 소모). 확률은 제안값(사장님 확정).",
            SkillTriggerType.OnHitChance, 700f, 1f / 16f, "", SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self });

        SkillData form = MakeSkill("구건", "구건 — 형태변환(체력 게이지 50): 8초 + 유닛삭제 카운트×0.5초(상한 +15초)",
            "사장님 10-06 「체력스킬(형태변환-구건)」·「형태변환 시 유닛삭제 카운트를 소모해 개수 비례 지속시간 증가」. 체력 게이지(평타 +1)가 50에 차면 구건 형태 8초 + 카운트×0.5초(상한 +15초), 카운트 전부 소모, 형태 동안 자기 공격속도 −30%(사장님 확정). 형태 중 깔아뭉개기·배치기·힘의작용과반작용이 게이트를 연다.",
            SkillTriggerType.OnHitCount, 0f, 1f, "", SkillGaugeKind.Life, 50,
            new SkillEffect { kind = SkillEffectKind.FormChange, target = SkillTargetKind.Self, duration = 8f, multiplier = 0.5f, bonus = 15f, formSelfAttackSpeed = -0.30f });

        SkillData crush = MakeSkill("깔아뭉개기", "깔아뭉개기 — 구건 형태 중 방무딜(평타 1/4)",
            "사장님 10-06 「방무딜」(구건 형태 효과). 형태 중 평타 1/4 확률로 범위 400 안 적에게 AP(방어 무시) 2,000,000(신 기준 R49 일반 26%·R50 보스 2.3%).",
            SkillTriggerType.OnHitChance, 400f, 0.25f, "GUN_FORM", SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 2000000f });

        SkillData belly = MakeSkill("배치기", "배치기 — 구건 형태 중 스턴(평타 1/6)",
            "사장님 10-06 「스턴」(구건 형태 효과). 형태 중 평타 1/6 확률로 범위 405 안 적 스턴 2.0초(박민석 외동의고함 405·3초 기준 짧게).",
            SkillTriggerType.OnHitChance, 405f, 1f / 6f, "GUN_FORM", SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2f });

        SkillData reaction = MakeSkill("힘의작용과반작용", "힘의작용과반작용 — 구건 형태 중 넉백(평타 1/5)",
            "사장님 10-06 「넉백」(구건 형태 효과) + 「공격속도감소」(자기 −30%는 구건 형태변환에 있음). 형태 중 평타 1/5 확률로 맞은 적 하나를 경로 뒤로 160(원작 단위) 밀어 보낸다. 보스는 면역(사장님 확정).",
            SkillTriggerType.OnHitChance, 0f, 0.2f, "GUN_FORM", SkillGaugeKind.Mana, 0,
            new SkillEffect { kind = SkillEffectKind.Knockback, target = SkillTargetKind.SingleTarget, multiplier = 160f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { life2, eat, form, crush, belly, reaction };
        unit.unitName = Title;
        unit.damageType = (DamageType)3;   // 물딜+마딜(박민석 ADAP처럼)
        unit.lifeGaugeMax = 50f;
        unit.lifeGaugeStart = 0f;
        unit.lifeGaugeRegenPerSecond = 0f;
        unit.lifeGaugeCustomHitGain = false;   // 평타 +1
        EditorUtility.SetDirty(unit);

        material.unitName = MaterialTitle;
        EditorUtility.SetDirty(material);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"김건 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 변화됨_김건 「{material.unitName}」 · 입력말 {recipe.chatPhrase} · 체력 게이지 {unit.lifeGaugeMax}";
    }
}
