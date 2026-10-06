using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 구주호 「주호리얼」(초월_구주호_AD, 물딜) 적용(사장님 10-06 17번째 — 「다 추천대로」 확정, 설계표 Docs/research/TRANSCEND_JUHO_DESIGN_2026-10-06.md). 호출: call JuhoApply.Apply (다시 불러도 안전).
/// 이식 스킬 8개를 4개로 교체(새 kind 없음):
///  · 샌드백으로단련된주먹 = 스플래시(반경 300 — UnitData.attackSplashRadius) + 방깍 오라(반경 800 적 방어 −30, 특성강화 시 −40 + 공격력 +20% = 스킬 레벨 2) — 특성강화 3pt(스킬승급형 skillLevelUnlockIndex 1, 바지사장 틀)
///  · 그래플러 = 스턴(평타 1/6 범위 405 1.0초) + 암브(아머브레이크: 평타 1/8 단일 대상 방어 −9)
///  · 오라오라! = 범퍼(평타 25% 대상 최대체력 ×1%) · 죽지않은노장 = 특성강화 3pt 이름·설명(스킬 자체엔 효과 없음).
/// 칭호 「주호리얼」 · 마나 게이지 0 · 재료에서 희귀 배성령 삭제 · 입력말 「힘법사」.
/// </summary>
static class JuhoApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_구주호_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_구주호_AD.asset";
    const string TraitPath = "Assets/Data/Traits/Trait_초월_구주호_AD.asset";
    const string Title = "주호리얼";
    const string Phrase = "힘법사";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_구주호_AD_{suffix}.asset";
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
        var trait = AssetDatabase.LoadAssetAtPath<UnitTraitData>(TraitPath);
        if (unit == null || recipe == null) return "❌ 초월_구주호_AD 유닛·조합식 에셋 없음";

        // skills[0] — 특성강화(skillLevelUnlockIndex 1)가 이 스킬의 레벨 2를 읽는다. 레벨 1 = 방깍 −30, 레벨 2 = −40 + 공격력 +20%.
        SkillData fist = MakeSkill("샌드백으로단련된주먹", "샌드백으로단련된주먹 — 스플래시 + 방깍 오라(−30, 특성강화 −40)",
            "사장님 10-06 「스플래시」·「방깍(30)」(확정: 오라, 모든 적) · 「특성강화3(방깍10, 데미지증가)」. 평타 스플래시 반경 300(UnitData.attackSplashRadius) + 반경 800 안 적 방어 −30. 특성 포인트 3으로 강화하면 방깍 −40 + 이 유닛 공격력 +20%(스킬승급 레벨 2).",
            SkillTriggerType.Aura, 800f, 1f,
            new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -30f, buffId = "JUHO_ARMOR" });
        fist.levels.Add(new SkillLevel
        {
            cooldown = 0f, triggerChance = 1f, range = 800f,
            effects = new List<SkillEffect>
            {
                new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -40f, buffId = "JUHO_ARMOR" },
                new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.20f, buffId = "JUHO_ATK" },
            }
        });
        EditorUtility.SetDirty(fist);

        SkillData grappler = MakeSkill("그래플러", "그래플러 — 스턴(평타 1/6, 1초)",
            "사장님 10-06 「스턴(1)」. 평타 1/6 확률로 범위 405 안 적 스턴 1.0초(박민석 외동의고함 405·3초 기준 짧게).",
            SkillTriggerType.OnHitChance, 405f, 1f / 6f,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 1f });

        SkillData amb = MakeSkill("그래플러_암브", "그래플러 — 암브(아머브레이크, 평타 1/8 단일 방어 −9)",
            "사장님 10-06 「암브(단일9)」(PM 확정: 암브 = 아머브레이크). 평타 1/8 확률로 맞은 적 한 기의 방어 −9(원작 평타 발동 방깎 1~9, 참고표 ArmorBreak 최대 9).",
            SkillTriggerType.OnHitChance, 0f, 0.125f,
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 9f });

        SkillData ora = MakeSkill("오라오라", "오라오라! — 범퍼(평타 25% 대상 최대체력 1%)",
            "사장님 10-06 「범퍼(최대체력1%)」(끝딜 규칙: 확률 발동 = 최대체력 비례). 평타 25% 확률로 대상 최대 체력 ×1% 피해(신 기준 R49 일반 77.5k · R50 보스 875k), 방어 무시(원작 %체력 스킬은 UNIVERSAL — PM 새 규칙).",
            SkillTriggerType.OnHitChance, 0f, 0.25f,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMaxHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.01f, armorIgnoreRatio = 1f });

        SkillData veteran = MakeSkill("죽지않은노장", "죽지않은노장 — 특성강화 3pt(방깍 −40 · 공격력 +20%)",
            "사장님 10-06 「특성강화3(방깍10, 데미지증가)」. 명령 카드 특성강화 칸(포인트 3, 한 번)으로 샌드백으로단련된주먹이 레벨 2가 된다: 방깍 오라 −30 → −40 + 이 유닛 공격력 +20%. 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { fist, grappler, amb, ora, veteran };
        unit.unitName = Title;
        unit.attackSplashRadius = 300f;
        unit.manaMax = 0f;
        if (trait != null)
        {
            trait.targetUnit = unit;
            trait.traitName = "죽지않은노장 특성강화";
            trait.description = "사장님 10-06: 특성 포인트 3개로 강화하면 샌드백으로단련된주먹의 방깍 오라가 −30에서 −40이 되고 이 유닛 공격력이 +20% 늘어난다(한 번). 스킬승급형(skillLevelUnlockIndex 1) — 방깍 스킬의 레벨 2다.";
            trait.costTraitPoints = 3;
            trait.skillLevelUnlockIndex = 1;
            trait.effects = new List<TraitEffect>();
            EditorUtility.SetDirty(trait);
            unit.trait = trait;
        }
        EditorUtility.SetDirty(unit);

        // 재료에서 희귀 배성령 삭제(사장님 표에 없음)
        int removed = recipe.ingredients.RemoveAll(i => i != null && i.unit != null && i.unit.name == "희귀함_배성령");
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"구주호 주호리얼 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 스플래시 {unit.attackSplashRadius} · 특성 {(trait != null ? trait.traitName : "❌없음")} · 재료 {recipe.ingredients.Count}종(배성령 {removed}칸 삭제) · 입력말 {recipe.chatPhrase}";
    }
}
