using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 김경현 「상호파주방장」(초월_김경현_AP, 마딜) 적용(사장님 10-06 19번째, 설계표 Docs/research/TRANSCEND_GYEONGHYEON_DESIGN_2026-10-06.md). 호출: call GyeonghyeonApply.Apply (다시 불러도 안전).
/// 이식 스킬을 4개로 교체: 경력직 = 스플래시(반경 300, UnitData.attackSplashRadius) · 숙련된칼솜씨 = 액티브 끝딜(대상 지정, 최대체력 22%, 쿨 60초, 사거리 750, 방어 무시) ·
/// 재료확보 = 180초마다 가장 가까운 일반 적 1기 삭제 + 노획물 1개(GrantLoot; 특성강화 2pt 레벨 2 = 50% 확률로 1개 더) · 맛있는요리 = 노획물 이름·설명(스킬 자체엔 효과 없음).
/// 노획물 = 원작 레일리 노획물품(h056) 한 종류의 이름만 5개(치킨·떡볶이·돈까스·토스트·맥주): 클릭 = 판매(ItemUseKind.LootSale) — 37% 랜덤위습 1기, 그중 40% +100엔·목재 1.
/// 칭호 「상호파주방장」 · 마나 0 · 재료 안흔함 엄태웅 교체 + 특별 유재헌 추가 · 입력말 「셰프빅헤드」.
/// </summary>
static class GyeonghyeonApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_김경현_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_김경현_AP.asset";
    const string TraitPath = "Assets/Data/Traits/Trait_초월_김경현_AP.asset";
    const string Title = "상호파주방장";
    const string Phrase = "셰프빅헤드";
    static readonly string[] LootNames = { "치킨", "떡볶이", "돈까스", "토스트", "맥주" };

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float cooldown, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_김경현_AP_{suffix}.asset";
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
            new SkillLevel { cooldown = cooldown, triggerChance = 1f, range = range, effects = new List<SkillEffect>(effects) }
        };
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static ItemData MakeLoot(string name, int index, WispData wisp)
    {
        string path = $"Assets/Data/Items/ItemData_L00{index + 1}_{name}.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.itemName = name;
        item.hasGrade = false;
        item.tooltipText = $"노획물 — {name}. 클릭하면 판매한다: 37% 확률로 랜덤위습 1기, 그중 40%는 +100엔과 목재 1.";
        item.designNote = "사장님 10-06 초월 김경현 노획물(원작 레일리 노획물품 h056·A080 unique_sell6): 효과 없는 판매 물건, 이름만 5개. 판매 = ItemUseKind.LootSale.";
        item.effects = new List<ItemEffect>();
        item.useKind = ItemUseKind.LootSale;
        item.useWispRolls = new List<ItemUseWispRoll> { new ItemUseWispRoll { wisp = wisp, chance = 0.37f, elseWisp = null, colorHex = "C8E6A0" } };
        item.lootBonusChance = 0.4f;
        item.lootBonusGold = 100;
        item.lootBonusWood = 1;
        EditorUtility.SetDirty(item);
        return item;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        var uncommonTaewoong = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/안흔함_엄태웅.asset");
        var specialJaehun = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/특별함_유재헌.asset");
        if (unit == null || recipe == null || wisp == null || uncommonTaewoong == null || specialJaehun == null) return "❌ 에셋 없음(유닛·조합식·랜덤유닛 위습·안흔함 엄태웅·특별함 유재헌)";

        var loot = new List<ItemData>();
        for (int i = 0; i < LootNames.Length; i++) loot.Add(MakeLoot(LootNames[i], i, wisp));

        SkillData career = MakeSkill("경력직", "경력직 — 스플래시(평타 범위 피해)",
            "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300(UnitData.attackSplashRadius)의 같은 레인 적에게도 같은 피해. 이 스킬 자체엔 효과가 없다(이름·설명만, 로스터 필드가 일한다). 원작 최상호 AD 300 선례.",
            SkillTriggerType.Aura, 0f, 0f);

        SkillData knife = MakeSkill("숙련된칼솜씨", "숙련된칼솜씨 — 액티브 끝딜(대상 최대체력 22%)",
            "사장님 10-06 「액티브스킬(끝딜, 전체체력 22%)」 확정: 명령 카드 칸을 누른 뒤 적 하나를 클릭하면 그 적 최대 체력의 22%를 방어 무시로 입힌다. 쿨 60초 · 사거리 750(쿨은 제안값).",
            SkillTriggerType.ActiveButton, 750f, 60f,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMaxHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.22f, armorIgnoreRatio = 1f });
        knife.levels[0].needsTargetClick = true;
        EditorUtility.SetDirty(knife);

        SkillEffect kill = new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self };
        SkillEffect grant1 = new SkillEffect { kind = SkillEffectKind.GrantLoot, target = SkillTargetKind.Self, multiplier = 0f, lootItems = new List<ItemData>(loot) };
        SkillData mats = MakeSkill("재료확보", "재료확보 — 몹삭제(180초) + 노획물 획득",
            "사장님 10-06 「몹삭제(노획물획득 180초)」·「노획물획득(치킨, 떡볶이, 돈까스, 토스트, 맥주)」. 180초마다 사거리 700 안 가장 가까운 일반 적(보스·PV≥200·B06B 제외) 1기를 삭제하고 노획물 5종 중 무작위 1개를 얻는다(아이템 칸 6칸, 가득 차면 못 받음). 노획물은 눌러서 판매 — 37% 랜덤위습 1기, 그중 40% +100엔·목재 1(원작 레일리 노획물품). 특성강화 2pt(맛있는요리) = 노획물을 얻을 때 50% 확률로 1개 더.",
            SkillTriggerType.CooldownAutoCast, 700f, 180f, kill, grant1);
        mats.levels.Add(new SkillLevel
        {
            cooldown = 180f, triggerChance = 1f, range = 700f,
            effects = new List<SkillEffect>
            {
                new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self },
                new SkillEffect { kind = SkillEffectKind.GrantLoot, target = SkillTargetKind.Self, multiplier = 0.5f, lootItems = new List<ItemData>(loot) },
            }
        });
        EditorUtility.SetDirty(mats);

        SkillData cook = MakeSkill("맛있는요리", "맛있는요리 — 특성강화 2pt(재료획득확률 증가)",
            "사장님 10-06 「특성강화2(재료획득확률증가)」. 명령 카드 특성강화 칸(포인트 2, 한 번)으로 재료확보가 레벨 2가 된다: 노획물을 얻을 때 50% 확률로 1개 더. 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 0f);

        var trait = AssetDatabase.LoadAssetAtPath<UnitTraitData>(TraitPath);
        if (trait == null)
        {
            trait = ScriptableObject.CreateInstance<UnitTraitData>();
            AssetDatabase.CreateAsset(trait, TraitPath);
        }
        trait.targetUnit = unit;
        trait.traitName = "맛있는요리 특성강화";
        trait.description = "사장님 10-06: 특성 포인트 2개로 강화하면 재료확보로 노획물을 얻을 때 50% 확률로 1개를 더 얻는다(한 번). 스킬승급형(skillLevelUnlockIndex 1) — 재료확보의 레벨 2다.";
        trait.costTraitPoints = 2;
        trait.skillLevelUnlockIndex = 1;
        trait.effects = new List<TraitEffect>();
        EditorUtility.SetDirty(trait);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { career, knife, mats, cook };
        unit.unitName = Title;
        unit.attackSplashRadius = 300f;
        unit.manaMax = 0f;
        unit.trait = trait;
        EditorUtility.SetDirty(unit);

        int removed = recipe.ingredients.RemoveAll(i => i != null && i.unit != null && i.unit.name == "전설적인_엄태웅");
        if (!recipe.ingredients.Exists(i => i != null && i.unit == uncommonTaewoong)) recipe.ingredients.Add(new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = uncommonTaewoong, count = 1 });
        if (!recipe.ingredients.Exists(i => i != null && i.unit == specialJaehun)) recipe.ingredients.Add(new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = specialJaehun, count = 1 });
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        return $"김경현 상호파주방장 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 스플래시 {unit.attackSplashRadius} · 특성 {trait.traitName}({trait.costTraitPoints}pt) · 노획물 {loot.Count}종 · 재료 {recipe.ingredients.Count}종(전설 엄태웅 {removed}칸 삭제) · 입력말 {recipe.chatPhrase}";
    }
}
