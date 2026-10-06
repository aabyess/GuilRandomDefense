using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 최상호 「구일에서가장자유로운남자」(초월_최상호_AD, 물딜) 적용(사장님 10-06): 징베 이식 스킬 7개를 빼고 사장님 스킬 5개로 교체 · 칭호 · 재료.
/// 호출: call SanghoGuilApply.Apply (다시 불러도 안전 — 같은 이름 스킬 에셋은 덮어쓴다). 수치 근거: Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md.
///  · 상호파의수장 = 이감(적 이속 −30%) + 공격력 오라(+20% + 전설 이상 1기당 +4%, 상한 +40%) — Aura 850. 원작 이감 오라(A0GQ 귀기-조초 Oae1 −0.30) · 공증 오라(A0V8 ACac aare 850).
///  · 그분의후계자 = 아머브레이크(발동): 평타 10% · 방깎 5 · 범위 550(원작 타시기 Tasigi_02 9%·AId1 +5·550) + 전설 이상 아군에게 같은 스킬 부여(Aura 850 GrantSkillToAllies).
///  · 상호파집결 = 소환(발동): 평타 10%(두유찬 사보 1/10) · 쿨 10초(사장님) · 전설 노태현·양재모·박민석 20초(사장님).
///  · 왕의자질 = 보잡: 보스 상대 ×1.3 — 원작 근거 없음, 제안값(사장님 「못 찾으면 ×1.3」).
/// </summary>
static class SanghoGuilApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_최상호_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_최상호_AD.asset";
    const string Title = "구일에서가장자유로운남자";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, float cooldown, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_최상호_AD_{suffix}.asset";
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
            new SkillLevel { cooldown = cooldown, triggerChance = chance, range = range, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_최상호_AD 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
        UnitData legendNoTaeHyun = Roster("전설적인_노태현"), legendYangJaeMo = Roster("전설적인_양재모"), legendParkMinSeok = Roster("전설적인_박민석");
        if (legendNoTaeHyun == null || legendYangJaeMo == null || legendParkMinSeok == null) return "❌ 소환수 전설 유닛 에셋 없음";

        // 상호파의수장 — 이감 + 공격력 오라(Aura 850)
        const string atkBuff = "SANGHO_ATK_AURA";
        SkillData master = MakeSkill("상호파의수장", "상호파의수장 — 이감 + 공격력증가오라(전설 이상 수 비례)",
            "사장님 10-06 초월 최상호 구일. 이감: 범위 안 적 이속 −30%(원작 이감 오라 A0GQ 귀기-조초 AOae Oae1 −0.30). 공격력 오라: 본인·아군 공격력 +20%에 주인의 전설 이상 유닛(소환수 제외) 1기당 +4%(상한 +40%) — 범위 850(원작 공증 오라 A0V8 ACac aare 850·+25%) · 개수 비례는 원작 근거 없음(제안값).",
            SkillTriggerType.Aura, 850f, 1f, 0f,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.7f },
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.2f, buffId = atkBuff, perHighGradeUnitBonus = 0.04f, perHighGradeUnitBonusCap = 0.4f },
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.2f, buffId = atkBuff, perHighGradeUnitBonus = 0.04f, perHighGradeUnitBonusCap = 0.4f });

        // 그분의후계자 — 아머브레이크(발동) + 전설 이상 아군에게 부여
        SkillData armor = MakeSkill("그분의후계자", "그분의후계자 — 아머브레이크(발동)",
            "사장님 10-06. 평타 10% 확률로 대상 방어 −5(범위 550 안 적 한 기 — 원작 타시기 Tasigi_02 평타 9%·AId1 +5·범위 550 기준).",
            SkillTriggerType.OnHitChance, 550f, 0.10f, 0f,
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 5f });
        SkillData grant = MakeSkill("그분의후계자_부여", "그분의후계자 — 전설 이상 아군에게 아머브레이크 부여",
            "사장님 10-06 「전설이상상위유닛에게 아머브레이크(발동)능력부여」. 오라 범위 850 안 아군 중 등급 서열 ≥5(전설 이상)에게 위 발동 스킬을 빌려준다(범위를 벗어나면 회수). 소환수 제외.",
            SkillTriggerType.Aura, 850f, 1f, 0f,
            new SkillEffect { kind = SkillEffectKind.GrantSkillToAllies, target = SkillTargetKind.Allies, grantSkill = armor, minAllyTier = 5 });

        // 상호파집결 — 소환(발동)
        SkillData summon = MakeSkill("상호파집결", "상호파집결 — 소환수(노태현·양재모·박민석 전설) 소환(발동)",
            "사장님 10-06. 평타 10% 확률(원작 초월 사보 1/10 기준) · 쿨다운 10초 · 전설 노태현·양재모·박민석을 종류마다 동시 1기, 20초 동안 최상호 앞 부채꼴(−35°·0°·+35°, 반지름 70)에. 이미 있으면 남은 시간을 20초로 되돌린다. 소환수는 유닛 수·판매·조합 재료·전설 개수에서 제외.",
            SkillTriggerType.OnHitChance, 0f, 0.10f, 10f,
            new SkillEffect { kind = SkillEffectKind.SummonUnit, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { legendNoTaeHyun, legendYangJaeMo, legendParkMinSeok }, summonLifetime = 20f, summonFanDegrees = 35f, summonRadius = 70f });

        // 왕의자질 — 보잡(패시브)
        SkillData king = MakeSkill("왕의자질", "왕의자질 — 보잡(보스 상대 피해 증가)",
            "사장님 10-06 「보잡」 = 보스 잡기. 보스 상대 평타·스킬 최종 피해 ×1.3. 원작에 보스 전용 피해 증가 능력이 없어(보스 분기는 체력 비례 스킬의 고정값 분기뿐) 원작 근거 없음 — 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0f,
            new SkillEffect { kind = SkillEffectKind.BossDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { master, armor, grant, summon, king };
        unit.unitName = Title;
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_최상호"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_정윤식"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_고어진"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_최상호"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월위습_박은석.asset"), count = 1 },
        };
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        string missing = string.Join(", ", recipe.ingredients.FindAll(i => i.unit == null).ConvertAll(i => "null"));
        return $"최상호 구일 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → 5개 · 칭호 「{unit.unitName}」 · 재료 {recipe.ingredients.Count}종" + (missing.Length > 0 ? $" · ⚠️ 빈 재료 {missing}" : "");
    }
}
