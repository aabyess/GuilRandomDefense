using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 서민성 「그랜드마스터」(영원_서민성, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-5).
/// 호출: call EternalMinseongApply.Apply (다시 불러도 안전). 스킬(원작 오뎅 이식은 전부 뺀다):
///  · 그의스킬샷 = 액티브(깡딜, 지점 지정): 명령 카드 칸 → 땅 클릭 → 그 지점 반경 500 깡딜 1,500,000(방어 무시 AP), 쿨 15초, 강화 레벨당 +5%(SkillEffect.enhanceScale)
///  · 고혈-가장중요한순간 = 강화 시스템(이름): UnitData.enhanceMaxLevel 18 · 엔 5,000 + 위습 1개 / 레벨(명령 카드 「강화」 칸, UnitAttacker.TryEnhance)
///  · 도망치지마 = 6강 해금 단일 스턴(평타 1/6 · 1.0초) · 가게두어라 = 11강 해금 발동(평타 1/8 이감 0.5·3초 + 마방깍 Aegr +5) · 고혈 마나 스킬 = 16강 해금(마나 145 → 반경 500 깡딜 3,000,000 방무 — 사장님이 이름을 안 줘 「고혈 — 마지막 한 방」)
///  · 막타충 = 스킬 피해로 일반 적을 처치하면 5초 동안 스킬 피해 +20%(SkillEffectKind.SkillDamageAfterKill)
/// 칭호 「그랜드마스터」 · 입력말 「은둔고수」 · trait 비움. 재료(원문 3칸): 히든 이삭토스트 · 전설 박병규(세상물정모르는도련님) · 「이승우 악의근원」 = **불멸 이승우**(사장님 권장 확정 Q4 — 지금 식의 전설 이승우를 바꾼다).
/// </summary>
static class EternalMinseongApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_서민성.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_서민성.asset";
    const string Title = "그랜드마스터";
    const string Phrase = "은둔고수";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, int enhanceRequired, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_서민성_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, requiredEnhanceLevel = enhanceRequired, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 영원_서민성 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData shot = MakeSkill("그의스킬샷", "그의스킬샷 — 액티브 깡딜(땅 지점 조준)",
            "사장님 10-06 「액티브스킬(깡딜)」 + 「그의스킬샷」(스킬샷 = 조준 발사, 권장 확정). 명령 카드 액티브 칸을 누르고 땅을 좌클릭하면 그 지점 반경 500 안 적에게 깡딜 1,500,000(AP 방어 무시) — 쿨 15초, 강화 레벨 1당 피해 +5%(18강 +90%). 우클릭 취소. 값은 제안값. 호스트/싱글만(멀티 클라는 지점 지정 액티브와 같은 제한).",
            SkillTriggerType.ActiveButton, 500f, 1f, 0, 0,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 1500000f, armorIgnoreRatio = 1f, enhanceScale = 0.05f });
        shot.levels[0].cooldown = 15f;
        shot.levels[0].needsPointClick = true;
        EditorUtility.SetDirty(shot);

        SkillData blood = MakeSkill("고혈", "고혈-가장중요한순간 — 강화 시스템(이름)",
            "사장님 10-06 「강화(골드, 위습) 최대 18」. 이 유닛을 고르면 명령 카드에 「강화」 칸이 뜬다 — 누를 때마다 엔 5,000 + 위습 1개(종류 무관)를 내고 이 유닛의 강화 레벨이 1 오른다(최대 18). 6강 단일 스턴 · 11강 이감+마방깍 · 16강 마나 스킬 해금, 그의스킬샷 피해 레벨당 +5%. 값은 UnitData.enhanceMaxLevel·enhanceGoldCost·enhanceWispCount에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만). 비용은 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0, 0);

        SkillData stun = MakeSkill("도망치지마", "도망치지마 — 단일 스턴(6강 해금)",
            "사장님 10-06 「6강-단일스턴」. 강화 6 이상일 때 평타 1/6 확률로 맞은 적 한 기를 1.0초 스턴.",
            SkillTriggerType.OnHitChance, 0f, 1f / 6f, 0, 6,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1f });

        SkillData letGoSlow = MakeSkill("가게두어라_이감", "가게두어라 — 발동 이감(11강 해금)",
            "사장님 10-06 「11강-발동(이감, 마방깍)」 중 이감. 강화 11 이상일 때 평타 1/8 확률로 맞은 적 이동속도 −50%(남는 속도 0.5) 3초.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0, 11,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.5f, duration = 3f });
        SkillData letGoAegr = MakeSkill("가게두어라_마방깍", "가게두어라 — 발동 마방깍(11강 해금)",
            "사장님 10-06 「11강-발동(이감, 마방깍)」 중 마방깍. 강화 11 이상일 때 평타 1/8 확률로 맞은 적 한 기의 마법 방어 Aegr +5.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0, 11,
            new SkillEffect { kind = SkillEffectKind.AegrStack, target = SkillTargetKind.SingleTarget, multiplier = 5f });

        SkillData finisher = MakeSkill("고혈_마나스킬", "고혈 — 마지막 한 방(마나 스킬, 16강 해금)",
            "사장님 10-06 「16강 마나스킬(깡딜)」(이름은 사장님이 안 줘서 내가 지음). 강화 16 이상일 때 마나 게이지(평타 +1) 145(이 유닛 원작 영원 마나)에 차면 반경 500 안 적에게 깡딜 3,000,000(AP 방어 무시) → 게이지 0. 값은 제안값.",
            SkillTriggerType.OnHitCount, 500f, 1f, 145, 16,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 3000000f, armorIgnoreRatio = 1f });

        SkillData lastHit = MakeSkill("막타충", "막타충 — 막타 시 스킬 피해 증가(패시브)",
            "사장님 10-06 「몹 막타 시 스킬 데미지 증가」. 이 유닛의 스킬 피해로 일반 적을 처치하면 5초 동안 이 유닛 스킬 피해가 +20%(중첩 없음, 다시 처치하면 시간만 갱신). 평타 피해는 안 오른다. 값은 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0, 0,
            new SkillEffect { kind = SkillEffectKind.SkillDamageAfterKill, target = SkillTargetKind.Self, multiplier = 0.2f, duration = 5f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { shot, blood, stun, letGoSlow, letGoAegr, finisher, lastHit };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 145f;
        unit.manaGaugePerMana = 1f;
        unit.enhanceMaxLevel = 18;
        unit.enhanceGoldCost = 5000;
        unit.enhanceWispCount = 1;
        EditorUtility.SetDirty(unit);

        UnitData immortal = Roster("불멸_이승우");
        int swapped = 0;
        foreach (RecipeIngredient ing in recipe.ingredients)
            if (ing != null && ing.unit != null && ing.unit.name == "전설적인_이승우" && immortal != null) { ing.unit = immortal; swapped++; }
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "빈칸");
        return $"서민성 그랜드마스터 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 강화 최대 {unit.enhanceMaxLevel}({unit.enhanceGoldCost}엔+위습 {unit.enhanceWispCount}) · 재료 {recipe.ingredients.Count}종(이승우 교체 {swapped}): {string.Join(", ", names)} · 입력말 {recipe.chatPhrase}";
    }
}
