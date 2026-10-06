using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 엄태웅 「중사(진)」(초월_엄태웅_AD, 물딜) 적용(사장님 10-06 11번째): 스킬 4개 · 칭호 · trait 비움 · 입력말(태웅사마).
/// 호출: call TaewoongApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_TAEWOONG_DESIGN_2026-10-06.md.
///  · 타고난신체 = 깡딜: 평타 1/8 · 범위 450 · 500000(AD).   · 포박 = 단일 스턴: 평타 20% · 1.5초(제안값).
///  · 주특기 = 방깎: 평타 1/12 · 범위 550 · 방어 −5(원작 김민준 AP 9%·550·−5 · 최상호 1/16·−3).
///  · 강도높은트레이너 = 공격력 오라: 반경 850 · 아군+자기 공격력 +25%(원작 강재규 A0V8).
///  · 웅교교주 = 도박확률증가(+4%p/회, 엔 10000, 최대 5회 · 플레이어 개인 누적)는 명령 카드 칸이라 SkillData가 없다(GameHud·GamblingShop).
///  · 폭탄제조(목재강화)·유물 지배자의싸인은 사장님 확인 중 — 아직 안 넣는다.
/// </summary>
static class TaewoongApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_엄태웅_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_엄태웅_AD.asset";
    const string Title = "중사(진)";   // DisplayPerson이 앞에 「엄태웅」을 붙인다
    const string Phrase = "태웅사마";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_엄태웅_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_엄태웅_AD 유닛·조합식 에셋 없음";

        SkillData burst = MakeSkill("타고난신체", "타고난신체 — 깡딜(발동)",
            "사장님 10-06 「깡딜」. 평타 1/8 확률로 범위 450 안 적에게 고정 500000(물리). 원작 초월 깡딜 400,000~1,850,000 중간 — 제안값.",
            SkillTriggerType.OnHitChance, 450f, 0.125f,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AD, attackType = AttackType.Normal, multiplier = 500000f });

        SkillData bind = MakeSkill("포박", "포박 — 단일 스턴(발동)",
            "사장님 10-06 「단일스턴」. 평타 20% 확률로 맞은 적 하나를 1.5초 스턴(원작 단일 스턴 0.45~3초 · 김경현 AP A0X4 20%) — 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.20f,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1.5f });

        SkillData shred = MakeSkill("주특기", "주특기 — 방깎(발동)",
            "사장님 10-06 「방깎」. 평타 1/12 확률로 범위 550 안 적 방어 −5(원작 김민준 AP 9%·범위 550·−5 · 최상호 AD 1/16·−3) — 제안값.",
            SkillTriggerType.OnHitChance, 550f, 1f / 12f,
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.Enemies, multiplier = 5f });

        SkillData aura = MakeSkill("강도높은트레이너", "강도높은트레이너 — 아군 공격력 +25% 오라",
            "사장님 10-06 「공격력증가오라」. 반경 850 안 아군과 자기 공격력 +25%(원작 공증 오라 A0V8 ACac 0.25·반경 850, 참고표 AttackPowerBuffPercent).",
            SkillTriggerType.Aura, 850f, 1f,
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.25f, buffId = "TAEWOONG_ATK_AURA" },
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.25f, buffId = "TAEWOONG_ATK_AURA" });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { burst, bind, shred, aura };
        unit.unitName = Title;
        unit.trait = null;   // 원작 호킨스 A0WK 능력교체형 특성이 새 스킬을 덮지 않게 — 특성 에셋은 그대로
        EditorUtility.SetDirty(unit);

        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"엄태웅 중사(진) 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · trait 비움 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종";
    }
}
