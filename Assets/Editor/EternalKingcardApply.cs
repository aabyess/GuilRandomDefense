using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 최상호 「킹카」(영원_최상호, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 §2-8). 바지사장(초월_최상호_AP) 틀 복사:
///  · 황제의품격 = 방깍 오라 반경 850 적 방어 −40(원작 영원 핸콕 −45·카벤딧슈 −35 사이)
///  · 절대공격(킹카) = 마나 게이지 100 → 반경 600 깡딜 2,000,000(방어 무시 — 바지사장과 같은 일괄 방무), 공속 비례 ×(1+1.0×(공속−1)) 공속 4배까지
///  · 절대방어(킹카) = 체력 게이지 175 → 반경 500 스턴 2.0초, 공속 비례 ×(1+0.5×(공속−1)) 공속 3배까지(최대 4초)
///  · 구일의황제 = 이름만(사장님 권장 확정)
/// 호출: call EternalKingcardApply.Apply (다시 불러도 안전). 칭호 「킹카」 · 입력말 「킹카」(채팅 전용). 「최상호 or 바지사장」 OR은 식 둘(영원_최상호·영원_최상호_바지사장)로 이미 있어 둘 다 같은 입력말을 쓴다.
/// </summary>
static class EternalKingcardApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_최상호.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_최상호.asset";
    const string RecipeAltPath = "Assets/Data/Recipes/영원_최상호_바지사장.asset";   // 「최상호 or 바지사장」 — 첫 재료만 초월_최상호_AP인 쌍둥이 식(OR은 식 둘로 이미 구현돼 있다)
    const string Title = "킹카";
    const string Phrase = "킹카";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_최상호_{suffix}.asset";
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
        if (unit == null || recipe == null) return "❌ 영원_최상호 유닛·조합식 에셋 없음";

        SkillData dignity = MakeSkill("황제의품격", "황제의품격 — 방깍 오라(−40)",
            "사장님 10-06 「방깍」(확정 해석: 오라, 모든 적). 반경 850 안 적 방어 −40(원작 영원 핸콕 A0IM −45·카벤딧슈 A0EC −35 사이, 제안값).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 850f,
                effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -40f, buffId = "KINGCARD_ARMOR" } },
            });

        SkillData attack = MakeSkill("절대공격", "절대공격(Style : 킹카) — 마나스킬(공속 비례 깡딜)",
            "사장님 10-06 「절대공격(style : 킹카)」 = 바지사장 절대공격과 같은 틀. 마나 게이지(평타 +1) 100(이 유닛 원작 영원 마나 100)에 차면 600 범위 2,000,000(방어 무시 AP), 공속이 높을수록 세진다: 피해 × (1 + 1.0 × (공속 배율 − 1)), 공속 4배까지(최대 8,000,000). 공속 비례는 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                range = 600f, hitCountThreshold = 100, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, triggerChance = 1f,
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

        SkillData defense = MakeSkill("절대방어", "절대방어(Style : 킹카) — 체력스킬(공속 비례 스턴)",
            "사장님 10-06 「절대방어(style : 킹카)」 = 바지사장 절대방어와 같은 틀. 체력 게이지(평타 +1) 175(이 유닛 원작 영원 LIFE 175)에 차면 500 범위 스턴 2.0초, 공속이 높을수록 길어진다: 지속 × (1 + 0.5 × (공속 배율 − 1)), 공속 3배까지(최대 ×2 = 4초). 공속 비례는 원작 근거 없음 — 제안값.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                range = 500f, hitCountThreshold = 175, resetTo = 0, gaugeKind = SkillGaugeKind.Life, triggerChance = 1f,
                effects = new List<SkillEffect>
                {
                    new SkillEffect
                    {
                        kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2f, heroDuration = 1f,
                        attackSpeedScale = 0.5f, attackSpeedScaleCap = 3f,
                    },
                },
            });

        SkillData emperor = MakeSkill("구일의황제", "구일의황제",
            "사장님 10-06 스킬 이름만 있고 효과 사양이 없다 — 사장님 10-06 확정(권장): 이름만 단다. 효과가 생기면 채운다.",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, effects = new List<SkillEffect>() });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { dignity, attack, defense, emperor };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 100f;
        unit.manaGaugePerMana = 1f;
        unit.lifeGaugeMax = 175f;
        unit.lifeGaugeCustomHitGain = false;   // 평타 +1
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        var alt = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipeAltPath);
        if (alt != null) { alt.chatPhrase = Phrase; EditorUtility.SetDirty(alt); }
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"최상호 킹카 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 체력 게이지 {unit.lifeGaugeMax} · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
