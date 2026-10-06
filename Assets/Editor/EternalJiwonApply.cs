using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 영원함 이지원 「이지웬디」(영원_이지원, 물딜) 적용(사장님 10-06 + 「전부 권장」 확정, 설계표 Docs/research/ETERNAL_DESIGN_2026-10-06.md §2-3).
/// 호출: call EternalJiwonApply.Apply (다시 불러도 안전). 스킬 6(원작 이식 스킬은 전부 뺀다):
///  · 축제개최 = 마나 스킬: 마나 게이지 150 → 반경 600 안 적에게 깡딜 2,300,000(2,000,000 × 범위증폭 1.15, AP 방어 무시) + 스턴 2.0초 + 반경 안 아군(자기 포함) 공속 +15% 10초 → 게이지 0
///  · 그동안쌓은덕력 = 패시브(이름만): 이 유닛이 친 평타 수만큼 축제개최의 깡딜·스턴이 세진다(평타 1당 +0.1%, 상한 +100% — SkillEffect.hitCountScale, 효과는 축제개최 안에 걸려 있다)
///  · 전시회 = 이감 오라(반경 850 적 −30%) · 치유 = 마젠(UnitData.manaAura: 반경 850 주변 아군 마나 게이지 +3/초) · 작품매매 = 노획물(평타 4%로 노획물 아이템 1개, 판매하면 37% 위습·그중 40% +100엔·목재 1)
///  · 스트레스발산 = 축제개최의 스턴 부분(이름만 — 같은 게이지를 두 스킬이 나눠 쓰면 게이지가 엇갈려 한 스킬로 묶었다)
/// 칭호 「이지웬디」 · 입력말 「악의구렁텅이의여제」 · 스플래시 350 유지 · 체력 게이지 0(안 씀) · trait 비움.
/// 재료(원문 9칸): 전설 노태현 → **특별 노태현**(노티) · 희귀 강재규 → **안흔 강재규**(재규어) · **특별 조세민(이청용) 추가** — 나머지는 그대로(commandId 대조).
/// </summary>
static class EternalJiwonApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/영원_이지원.asset";
    const string RecipePath = "Assets/Data/Recipes/영원_이지원.asset";
    const string Title = "이지웬디";
    const string Phrase = "악의구렁텅이의여제";
    static readonly string[] LootAssets = { "ItemData_L001_치킨", "ItemData_L002_떡볶이", "ItemData_L003_돈까스", "ItemData_L004_토스트", "ItemData_L005_맥주" };

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_영원_이지원_{suffix}.asset";
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

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 영원_이지원 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
        var loot = new List<ItemData>();
        foreach (string a in LootAssets)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Data/Items/{a}.asset");
            if (item != null) loot.Add(item);
        }
        if (loot.Count == 0) return "❌ 노획물 아이템(ItemData_L00N_*) 없음 — GyeonghyeonApply.Apply를 먼저";

        SkillData festival = MakeSkill("축제개최", "축제개최 — 마나 스킬(범위 깡딜 + 스턴 + 아군 공속)",
            "사장님 10-06 「마나스킬(범위증폭, 공속, 깡딜, 스턴)」 확정(권장). 마나 게이지(평타 +1) 150(이 유닛 원작 영원 마나)에 차면 반경 600 안 적에게 깡딜 2,300,000(기본 2,000,000 × 범위증폭 15% — 불멸 고도현과 같은 읽기, AP 방어 무시) + 스턴 2.0초, 반경 안 아군(자기 포함) 공속 +15% 10초 → 게이지 0. 그동안쌓은덕력: 평타 1당 깡딜·스턴 +0.1%(상한 +100%). 값은 제안값.",
            SkillTriggerType.OnHitCount, 600f, 1f, 150, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.Enemies, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 2300000f, armorIgnoreRatio = 1f, hitCountScale = 0.001f, hitCountScaleCap = 1f },
            new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2f, hitCountScale = 0.001f, hitCountScaleCap = 1f },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.15f, duration = 10f, buffId = "JIWON_FESTIVAL" });

        SkillData virtue = MakeSkill("그동안쌓은덕력", "그동안쌓은덕력 — 평타 누적 비례 마나 스킬 강화(패시브)",
            "사장님 10-06 「공격한횟수만큼 마나스킬(깡딜, 스턴) 강화」. 이 유닛이 친 평타 수 1당 축제개최의 깡딜·스턴이 +0.1% 세진다(상한 +100% = 1000타). 효과는 축제개최 안(SkillEffect.hitCountScale)에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만). 계수는 제안값.",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

        SkillData exhibition = MakeSkill("전시회", "전시회 — 이감 오라(−30%)",
            "사장님 10-06 「이감」. 반경 850 안 적 이동속도 −30%(남는 속도 0.7 — 원작 영원 이감 오라 −45~−25 사이, 제안값).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.7f, buffId = "JIWON_SLOW" });

        SkillData heal = MakeSkill("치유", "치유 — 마젠(주변 아군 마나 게이지 +3/초)",
            "사장님 10-06 「마젠」(불멸 고도현 확정과 같은 읽기: 마젠 = 반경 850 주변 아군(자기 포함) 마나 게이지 재생 오라 +3/초). 값은 UnitData.manaAuraRegenPerSecond·manaAuraRange에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

        SkillData trade = MakeSkill("작품매매", "작품매매 — 노획물(평타 4%)",
            "사장님 10-06 「노획물」. 평타 4% 확률로 노획물(치킨·떡볶이·돈까스·토스트·맥주 중 무작위 1개)을 인벤토리에 받는다. 노획물은 사용 = 판매 — 37% 확률로 랜덤유닛 위습 1, 그중 40% +100엔·목재 1(원작 A080 노획물품). 4%는 제안값(초월 김경현 노획물은 몹삭제 1회마다).",
            SkillTriggerType.OnHitChance, 0f, 0.04f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.GrantLoot, target = SkillTargetKind.Self, multiplier = 0f, lootAlways = true, lootItems = new List<ItemData>(loot) });

        SkillData stress = MakeSkill("스트레스발산", "스트레스발산 — 축제개최의 스턴(이름만)",
            "사장님 10-06 이름 6개 중 하나 — 효과 짝짓기(권장 확정): 마나 스킬의 스턴 부분. 같은 마나 게이지를 두 스킬이 나눠 쓰면 게이지가 엇갈려 스턴은 축제개최 안에 넣었다. 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { festival, virtue, exhibition, heal, trade, stress };
        unit.unitName = Title;
        unit.trait = null;
        unit.manaMax = 150f;
        unit.manaGaugePerMana = 1f;
        unit.lifeGaugeMax = 0f;               // 체력 게이지는 안 쓴다(원작 이식분 115 제거)
        unit.manaAuraRegenPerSecond = 3f;      // 치유 = 마젠
        unit.manaAuraRange = 850f;
        EditorUtility.SetDirty(unit);

        // 재료(원문 9칸): 전설 노태현 → 특별 노태현, 희귀 강재규 → 안흔 강재규, 특별 조세민 추가.
        int swapped = 0;
        UnitData taehyunSpecial = Roster("특별함_노태현"), jaegyuUncommon = Roster("안흔함_강재규"), semin = Roster("특별함_조세민");
        foreach (RecipeIngredient ing in recipe.ingredients)
        {
            if (ing == null || ing.unit == null) continue;
            if (ing.unit.name == "전설적인_노태현" && taehyunSpecial != null) { ing.unit = taehyunSpecial; swapped++; }
            else if (ing.unit.name == "희귀함_강재규" && jaegyuUncommon != null) { ing.unit = jaegyuUncommon; swapped++; }
        }
        bool hasSemin = recipe.ingredients.Exists(i => i != null && i.unit == semin);
        if (!hasSemin && semin != null) { recipe.ingredients.Add(new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = semin, count = 1 }); swapped++; }
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "빈칸");
        return $"이지원 이지웬디 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 마젠 {unit.manaAuraRegenPerSecond}/초({unit.manaAuraRange}) · 재료 {recipe.ingredients.Count}종(교체·추가 {swapped}): {string.Join(", ", names)} · 입력말 {recipe.chatPhrase}";
    }
}
