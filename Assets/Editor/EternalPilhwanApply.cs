using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 문필환 「네버마인드」(영원_문필환, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-4).
/// 호출: call EternalPilhwanApply.Apply (다시 불러도 안전). 이름 5 ↔ 효과 8 짝짓기(권장 확정):
///  · 구일왕자 = 예쁨(사양 없음 — 이름만) + 마법→물리 변환(이 유닛 스킬 피해는 전부 물리 AD — 대상 방어 적용, 방무 아님) — 이름만, 효과는 아래 스킬들이 AD로 나가는 것
///  · 문의일족 = 디버프 「문」(공용 에셋 SkillData_공용_디버프_문: 자기 공속 −15%, 사장님 확정 수치) + 이름만 칸
///  · 횡포 = 깡딜(평타 1/10 · 1,000,000) + 스턴(평타 1/6 · 반경 405 · 1.0초) — 스킬 둘(확률이 달라 갈라 둠)
///  · 깨물기 = 범퍼(평타 15% · 맞은 적 중심 반경 300 안 적 전체 · 현재체력 2%, 방어 무시)
///  · 꺽을수없는고집 = 맥주만땅강화(평타 1/8 단일 암브 −5 + 평타마다 자기 피해 +1% 누적 최대 +50%·3초 초기화) + 광폭화(광폭화 몹 B06B 상대 피해 ×1.5, 새 SkillEffectKind.DamageVsTargetBuff)
/// 칭호 「네버마인드」 · 입력말 「구일왕자」 · 마나 0(마나 스킬 없음) · trait 비움. 재료(원문 4칸): 변화됨 박은석(NPC) · 전설 최상호(상호파의수장) · 특별 김태영 · 전설 박은석(은석가족두목, 두 번째 「박은석」) — 지금 식의 전설 노태현·특별 양재모·초월위습은 뺀다.
/// </summary>
static class EternalPilhwanApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_문필환.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_문필환.asset";
    const string MoonPath = "Assets/Data/UnitSkills/SkillData_공용_디버프_문.asset";
    const string Title = "네버마인드";
    const string Phrase = "구일왕자";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_문필환_{suffix}.asset";
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
            new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = 0, resetTo = 0, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var moon = AssetDatabase.LoadAssetAtPath<SkillData>(MoonPath);
        if (unit == null || recipe == null || moon == null) return "❌ 영원_문필환 유닛·조합식·공용 디버프 문 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData prince = MakeSkill("구일왕자", "구일왕자 — 예쁨 + 마법피해→물리 변환(이름만)",
            "사장님 10-06. 「예쁨」은 효과 사양이 없어 이름만(사장님 권장 확정). 「모든 마법피해량을 물리피해량으로 변환」은 이 유닛 스킬의 피해를 전부 물리(AD, 대상 방어 적용)로 내는 것으로 읽었다 — 아래 스킬이 모두 AD라 이 스킬 자체엔 효과가 없다.",
            SkillTriggerType.Aura, 0f, 1f);
        SkillData clan = MakeSkill("문의일족", "문의일족 — 디버프 「문」(이름칸)",
            "사장님 10-06 「디버프 : 문, 공속10%감소」. 공용 디버프 「문」(SkillData_공용_디버프_문 — 자기 공속 −15%, 사장님 확정 수치)을 이 유닛이 같이 가진다. 이 칸은 이름만이고 효과는 공용 「문」 스킬이 한다.",
            SkillTriggerType.Aura, 0f, 1f);
        SkillData tyranny = MakeSkill("횡포_깡딜", "횡포 — 깡딜",
            "사장님 10-06 「깡딜」(팔레트). 평타 1/10 확률로 맞은 적에게 고정 1,000,000(AD — 마법→물리 변환, 대상 방어 적용).",
            SkillTriggerType.OnHitChance, 0f, 0.1f,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.SingleTarget, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 1000000f });
        SkillData tyrannyStun = MakeSkill("횡포_스턴", "횡포 — 스턴",
            "사장님 10-06 「스턴」(팔레트). 평타 1/6 확률로 반경 405 안 적 스턴 1.0초.",
            SkillTriggerType.OnHitChance, 405f, 1f / 6f,
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 1f });
        SkillData bite = MakeSkill("깨물기", "깨물기 — 범퍼(현재체력 2%, 반경 300)",
            "사장님 10-06 「범퍼(현재체력)」(범퍼 = 범위 전체 확정). 평타 15% 확률로 맞은 적 중심 반경 300 안 모든 적에게 현재 체력의 2% 피해(AD, 방어 무시 — %체력 일괄 규칙, 보스·스토리도 비례).",
            SkillTriggerType.OnHitChance, 300f, 0.15f,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetCurrentHpPercent, target = SkillTargetKind.Enemies, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.02f, armorIgnoreRatio = 1f });
        SkillData beerAmb = MakeSkill("꺽을수없는고집_암브", "꺽을수없는고집 — 맥주만땅강화(단일 암브)",
            "사장님 10-06 「맥주만땅강화(단일암브, 피해량증가)」 중 암브. 평타 1/8 확률로 맞은 적 한 기의 방어 −5(아머브레이크, 평타 발동 방깎 1~9 중앙).",
            SkillTriggerType.OnHitChance, 0f, 0.125f,
            new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 5f });
        SkillData beerStack = MakeSkill("꺽을수없는고집_피해증가", "꺽을수없는고집 — 맥주만땅강화(피해량 증가)",
            "사장님 10-06 「맥주만땅강화(단일암브, 피해량증가)」 중 피해량증가. 평타 한 대마다 자기 평타·스킬 최종 피해 +1%씩 누적, 최대 +50%, 마지막 평타 뒤 3초 안 치면 초기화(신문철 스노우볼과 같은 틀, 폭증 읽기). 제안값.",
            SkillTriggerType.OnHitChance, 0f, 1f,
            new SkillEffect { kind = SkillEffectKind.AttackDamageStack, target = SkillTargetKind.Self, stackPerHit = 0.01f, stackCap = 0.5f, stackResetSeconds = 3f });
        SkillData berserk = MakeSkill("꺽을수없는고집_광폭화", "꺽을수없는고집 — 광폭화 몹 상대 강화(패시브)",
            "사장님 10-06 「광폭화」(원랜디 광폭화 전담 읽기: 신세계 뒤 나오는 광폭화 몹을 잡는 유닛). 광폭화 몹(B06B)을 칠 때 이 유닛 평타·스킬 최종 피해 ×1.5(제안값).",
            SkillTriggerType.Aura, 0f, 1f,
            new SkillEffect { kind = SkillEffectKind.DamageVsTargetBuff, target = SkillTargetKind.Self, multiplier = 0.5f, buffId = "B06B" });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { prince, clan, moon, tyranny, tyrannyStun, bite, beerAmb, beerStack, berserk };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 0f;
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("변화됨_박은석"), count = 1 },      // 박은석 NPC
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_최상호"), count = 1 },    // 최상호 상호파의수장
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_김태영"), count = 1 },      // 김태영 일베조무사
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_박은석"), count = 1 },    // 박은석(은석가족두목)
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"문필환 네버마인드 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")} · 입력말 {recipe.chatPhrase}";
    }
}
