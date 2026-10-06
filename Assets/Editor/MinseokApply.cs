using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 박민석 「외동의악마」(초월_박민석_ADAP, 물딜+마딜) 적용(사장님 10-06): 스킬 교체 · 칭호 · 재료 · 공용 디버프 「외동」. 호출: call MinseokApply.Apply (다시 불러도 안전).
/// 수치 근거(Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md, 초월 평타 발동 Flat 76건·최대체력% 9건·마나 게이지 16건 등):
///  · 외동Lv.Devil = 깡딜(평타 1/10·400,000 AD) + 방깍(5) + 마방깍(Aegr +5) 한 굴림 · 흑인 = 범퍼(최대체력 ×0.04, 평타 15%) + 이감(0.7·2.5초) · 어지러움 = 스턴 1/6·1초
///  · 불가항력 = 보잡 ×1.3(원작 근거 없음) · 외동의고함 = 마나 125: AP 깡딜 2,500,000 + 스턴 3초(범위 405) · 공복상태 = Life 85: 가장 가까운 일반 적 1기 즉사(범위 405, 일반 적이 있을 때만)
///  · 「외동」 디버프: 공용 SkillData_공용_디버프_외동.asset(이름·설명만, 효과 0) — 김민준(구현담당2)도 같은 에셋을 skills에 넣는다.
/// </summary>
static class MinseokApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_박민석_ADAP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_박민석_ADAP.asset";
    const string Title = "외동의악마";
    public const string DebuffPath = "Assets/Data/UnitSkills/SkillData_공용_디버프_외동.asset";

    static SkillData MakeAt(string path, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, bool requireNormalEnemy, params SkillEffect[] effects)
    {
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, gaugeKind = gauge, hitCountThreshold = threshold, resetTo = 0, requireNormalEnemyInRange = requireNormalEnemy, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillData Make(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, SkillGaugeKind gauge, int threshold, bool requireNormalEnemy, params SkillEffect[] effects)
        => MakeAt($"{SkillFolder}/SkillData_사장님_초월_박민석_ADAP_{suffix}.asset", skillName, description, trigger, range, chance, gauge, threshold, requireNormalEnemy, effects);

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_박민석_ADAP 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData devil = Make("외동LvDevil", "외동Lv.Devil — 깡딜 + 방깍 + 마방깍(평타 1/10)",
            "사장님 10-06. 평타 10% 확률로 대상 한 기에게 깡딜 400,000(AD) + 방어 −5 + 마법 방어 −5레벨(Aegr 스택). 값 근거: 초월 평타 발동 Flat 깡딜 76건(확률 중앙 0.10·피해 중앙 400,000), 방깎 7건 중앙 5, 원작 Aegr 스택 +5(영원 김정래·조세민).",
            SkillTriggerType.OnHitChance, 0f, 0.10f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 400000f },
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 5f },
            new SkillEffect { kind = SkillEffectKind.AegrStack, target = SkillTargetKind.SingleTarget, multiplier = 5f });

        SkillData bumper = Make("흑인", "흑인 — 범퍼(최대체력) 발동(이감)",
            "사장님 10-06 「범퍼(최대체력) 발동(이감)」. 평타 15% 확률로 대상 최대 체력 ×4% 피해(AD) + 대상 이동속도 −30% 2.5초. 값 근거: 최대체력% 9건 0.01~0.12·확률 7.25~20%, 이감 발동형 0.25~0.75·2~5초(두유찬 사보 0.7·2.5초).",
            SkillTriggerType.OnHitChance, 0f, 0.15f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMaxHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.04f },
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.7f, duration = 2.5f });

        SkillData dizzy = Make("어지러움", "어지러움 — 스턴(1)",
            "사장님 10-06 「스턴(1)」. 평타 1/6 확률로 대상 한 기 스턴 1초(원작 초월 Zoro 1/6·1.0초, A0AG 20%·1.0초).",
            SkillTriggerType.OnHitChance, 0f, 1f / 6f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1f });

        SkillData force = Make("불가항력", "불가항력 — 보잡(보스 상대 피해 증가)",
            "사장님 10-06 「보잡」. 보스 상대 평타·스킬 최종 피해 ×1.3. 원작에 보스 전용 피해 증가 능력이 없어 원작 근거 없음 — 제안값(최상호 구일 왕의자질과 같다).",
            SkillTriggerType.Aura, 0f, 1f, SkillGaugeKind.Mana, 0, false,
            new SkillEffect { kind = SkillEffectKind.BossDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f });

        SkillData roar = Make("외동의고함", "외동의고함 — 마나스킬(깡딜 + 스턴 3초)",
            "사장님 10-06 「마나스킬(깡딜, 스턴3초)」. 마나 게이지 125에 닿으면 범위 405 안 적에게 깡딜 2,500,000(AP) + 스턴 3초. 값 근거: 마나 게이지 깡딜 16건 임계 50~160 중앙 125·피해 중앙 2,500,000, 스턴 3초(A15Z·A0PZ 등 원작 다수).",
            SkillTriggerType.OnHitCount, 405f, 1f, SkillGaugeKind.Mana, 125, false,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 2500000f },
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 3f });

        SkillData hungry = Make("공복상태", "공복상태 — 체력스킬(유닛삭제: 일반 적 즉사)",
            "사장님 10-06 「체력스킬(유닛삭제)」. LIFE 게이지 85에 닿으면 범위 405 안 가장 가까운 일반 적 한 기를 즉사. 보스·스토리 적·신세계 광폭화(B06B)는 대상 제외(원작 LaillySkill3·Kick_1의 PV<200 + B06B 없음과 같은 판정). 범위 안에 일반 적이 없으면 발동하지 않고 게이지를 그대로 둔다.",
            SkillTriggerType.OnHitCount, 405f, 1f, SkillGaugeKind.Life, 85, true,
            new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self });

        SkillData debuff = MakeAt(DebuffPath, "외동",
            "디버프 「외동」 — 효과는 사장님이 알려 주시는 대로 나중에 채운다(지금은 이름·설명 칸만, 효과 0). 박민석 외동의악마·김민준이 이 에셋 하나를 같이 쓴다 — 여기만 고치면 둘 다 바뀐다.",
            SkillTriggerType.Aura, 0f, 1f, SkillGaugeKind.Mana, 0, false);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { devil, bumper, dizzy, force, roar, hungry, debuff };
        unit.unitName = Title;
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("히든_감탄떡볶이"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_백기현"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_이승우"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("안흔함_상붕카"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("히든_뻬꼼"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("안흔함_김민준"), count = 1 },
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },
        };
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int nulls = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"외동의악마 적용: 스킬 {old.Count}개 제거 → 7개(외동 디버프 포함) · 칭호 「{unit.unitName}」 · 재료 {recipe.ingredients.Count}종" + (nulls > 0 ? $" · ⚠️ 빈 재료 {nulls}" : "") + $" · 공용 디버프 {DebuffPath}";
    }
}
