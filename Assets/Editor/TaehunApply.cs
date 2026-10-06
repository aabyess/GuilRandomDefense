using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 이태훈 「윤식파군기반장」(초월_이태훈_AP, 마딜) 적용(사장님 10-06 23번째): 원작 후지토라 이식 스킬 6개를 빼고 사장님 스킬 4개로 교체 · 칭호 · 재료(김용태 전설 → 특별함) · 입력말(골목대장) · trait 비움.
/// 호출: call TaehunApply.Apply (다시 불러도 안전). 설계표: Docs/research/TRANSCEND_TAEHUN_DESIGN_2026-10-06.md (사장님 확정 반영).
///  · 폭력의즐거움 = 방깍(35) 범위 오라: 반경 600 적 방어 −35(김만경 45·구주호 30과 같은 해석).
///  · 두려움의대상 = 이감 오라: 반경 600 적 이속 −20%(사장님 수치 없음 — 제안값).
///  · 약자멸시 = 마나 스킬(몹삭제): 마나 게이지 140(원작 후지토라 140, 평타 +1) 가득 → 반경 700 안 일반 적 중 「잃은 체력이 가장 큰」 1기 삭제(보스 제외 — 사장님 확정, SkillEffect.killMostLostHp).
///  · 명치적중 = 범퍼: 평타 15% 한 굴림에 현재체력 0.8%(방어 무시 — 원작 %체력은 UNIVERSAL) + 암브 −2(단일 방어 감소) + 스턴 1초(사장님 확정: 셋 한꺼번에).
/// </summary>
static class TaehunApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_이태훈_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_이태훈_AP.asset";
    const string Title = "윤식파군기반장";
    const string Phrase = "골목대장";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, bool requireNormalEnemy, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_이태훈_AP_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = threshold, resetTo = 0, gaugeKind = gauge, requireNormalEnemyInRange = requireNormalEnemy, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_이태훈_AP 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData armor = MakeSkill("폭력의즐거움", "폭력의즐거움 — 방깍(35) 범위 오라",
            "사장님 10-06 「방깍(35)」 = 주변 적 방어 감소 오라(김만경 45·구주호 30과 같은 해석, PM 확정). 반경 600 안 적 방어 −35(범위를 나가면 복구). 원작 방깎(AId1) 합계 상한 −75 안(암브 −2와 합쳐 −37).",
            SkillTriggerType.Aura, 600f, 1f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -35f });

        SkillData slow = MakeSkill("두려움의대상", "두려움의대상 — 이감 오라",
            "사장님 10-06 「이감」(수치 없음). 반경 600 안 적 이동속도 −20%(남는 속도 0.8, 박민수 −30%·두유찬 −20% 선례 중간 — 제안값).",
            SkillTriggerType.Aura, 600f, 1f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.8f });

        SkillData weak = MakeSkill("약자멸시", "약자멸시 — 마나스킬(몹삭제: 잃은 체력이 가장 큰 일반 적)",
            "사장님 10-06 「마나스킬(몹삭제)」 · 「잃은체력이가장높은몹우선삭제」. 마나 게이지(평타 +1)가 140에 차면 반경 700 안 일반 적(PV<200·보스·B06B 제외) 중 잃은 체력(최대−현재)이 가장 큰 1기를 삭제 → 게이지 0. 140·700은 원작 후지토라(Huji_03) 값. 범위 안에 일반 적이 없으면 발동하지 않는다.",
            SkillTriggerType.OnHitCount, 700f, 1f, SkillGaugeKind.Mana, 140, true,
            new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self, killMostLostHp = true });

        SkillData solar = MakeSkill("명치적중", "명치적중 — 범퍼(현재체력 0.8%) + 암브 2 + 스턴(발동)",
            "사장님 10-06 「범퍼(암브2, 현재체력0.8%, 스턴)」 — 셋을 한꺼번에(질문지 확정). 평타 15%(박민석 흑인 범퍼 선례) 확률로 맞은 적 중심 반경 300(제안값) 안 모든 적에게 현재 체력의 0.8% 피해(범퍼 = 범위 전체 — 사장님 10-06 정정, 암브·스턴은 표적 단일 그대로)(방어 무시 — 원작 %체력 스킬은 UNIVERSAL) + 방어 −2(암브 = 아머브레이크 단일, PM 확정) + 스턴 1초(제안값).",
            SkillTriggerType.OnHitChance, 300f, 0.15f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetCurrentHpPercent, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.008f, armorIgnoreRatio = 1f },
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 2f },
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { armor, slow, weak, solar };
        unit.unitName = Title;
        unit.trait = null;
        EditorUtility.SetDirty(unit);

        // 재료: 「김용태 흑화」 = 특별함_김용태(commandId 흑화) — 지금 식의 전설 김용태를 바꾼다.
        UnitData special = Roster("특별함_김용태"), legend = Roster("전설적인_김용태");
        int swapped = 0;
        foreach (RecipeIngredient ing in recipe.ingredients)
            if (ing.unit == legend && special != null) { ing.unit = special; swapped++; }
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"이태훈 윤식파군기반장 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · trait 비움 · 마나 게이지 {unit.manaMax} · 김용태 재료 교체 {swapped}건 · 입력말 {recipe.chatPhrase} · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")}";
    }
}
