using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 이재윤 「초특급인싸」(초월_이재윤_AD, 물딜) 적용(사장님 10-06): 스킬 6 · 칭호 · 재료 · 입력말(원숭이왕) · 비행 · trait 비움 · 스토리 7 유물 종이비행기. 호출: call JaeyunApply.Apply (다시 불러도 안전).
/// 설계표: Docs/design/JAEYUN_DESIGN_2026-10-06.md (수치 근거는 거기 §3·§7).
///  ① 인싸Lv.Max 스턴 1초(평타 1/6) · ② 체육특기생(재윤) 체력스킬(평타 50타째 현재체력 25%, 보스 고정 300,000 — 노태현 틀) · ③ 91사단의지휘자 맵 전체 공격력 +25% 오라 ·
///  ④ 긍정의힘 맵 전체 아군 디버프 해제(Allies+Self) · ⑤ 원숭이의민첩함 공중이동(movementAbility Flying) · ⑥ 내면의악 레인 유닛 카운트 [한계−10, 한계]일 때 내 레인 적 방어 −10·이속 −30% 오라.
/// 종이비행기: 스토리 7 itemDrops의 버스터콜 지령서 자리를 대신한다(에셋은 지우지 않는다). 확률 1/20은 그대로.
/// </summary>
static class JaeyunApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_이재윤_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_이재윤_AD.asset";
    const string StoryPath = "Assets/Data/Stories/Story07_메가스터디.asset";
    const string BusterItemPath = "Assets/Data/Items/ItemData_I00L_버스터콜지령서.asset";
    const string PlaneItemPath = "Assets/Data/Items/ItemData_R002_종이비행기.asset";
    const string Title = "초특급인싸";
    const string Phrase = "원숭이왕";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_이재윤_AD_{suffix}.asset";
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

    static SkillEffect Hit(SkillEffectBasis basis, float multiplier, SkillEffectTargetCondition condition = SkillEffectTargetCondition.None, float conditionValue = 0f) => new SkillEffect
    {
        kind = SkillEffectKind.Damage, basis = basis, target = SkillTargetKind.SingleTarget,
        damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = multiplier,
        targetCondition = condition, targetConditionValue = conditionValue, armorIgnoreRatio = 1f,   // 고정·%체력 깡딜은 방어 무시(사장님 10-06)
    };

    const float MapWide = 5000f;   // 「맵 전체」 — 같은 주인 아군 전부(UnitIdentity.AlliesOf는 주인이 같은 유닛만 본다)

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var story = AssetDatabase.LoadAssetAtPath<StoryData>(StoryPath);
        var buster = AssetDatabase.LoadAssetAtPath<ItemData>(BusterItemPath);
        if (unit == null || recipe == null) return "❌ 초월_이재윤_AD 유닛·조합식 에셋 없음";
        if (story == null || buster == null) return "❌ 스토리 7·버스터콜 지령서 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData stun = MakeSkill("인싸LvMax", "인싸Lv.Max — 스턴(발동)",
            "사장님 10-06 「스턴(1초)」. 평타 1/6 확률로 맞은 적 스턴 1초(원작 초월 Zoro 1/6·1.0초, 박민석 「어지러움」과 같다).",
            SkillTriggerType.OnHitChance,
            new SkillLevel { cooldown = 0f, triggerChance = 1f / 6f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 1f } } });

        SkillData health = MakeSkill("체육특기생재윤", "체육특기생(재윤) — 체력스킬(현재체력)",
            "사장님 10-06 「현재체력 단일」. 평타 50타째(원작 초월 체력 스킬 김만경·키드 LIFE 50) 대상 현재체력 25%(원작 도플라밍고 일반 25%, PV<200) — 보스·스토리 적(PV≥200)은 비례 대신 고정 300,000(원작 보스 분기, 사장님 확정: 체력스킬 보스 고정은 원작 분기 그대로). 노태현 「가리지않는수단과방법」과 같은 틀.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana, hitCountThreshold = 50, resetTo = 0,
                effects = new List<SkillEffect>
                {
                    Hit(SkillEffectBasis.TargetCurrentHpPercent, 0.25f, SkillEffectTargetCondition.TargetPointValueLessThan, 200f),
                    Hit(SkillEffectBasis.Flat, 300000f, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f),
                },
            });

        SkillData command = MakeSkill("91사단의지휘자", "91사단의지휘자 — 공격력 오라(맵 전체)",
            "사장님 10-06 「공격력 오라 맵 전체」. 같은 레인 아군 전체와 자기 공격력 +25%(원작 공증 오라 A0V8 ACac 0.25 — 임장혁 「영혼없는칭찬」과 같은 값, 반경만 맵 전체). 수치는 제안값.",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = MapWide, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.25f, buffId = "JAEYUN_ATK_AURA" },
                    new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.25f, buffId = "JAEYUN_ATK_AURA" },
                },
            });

        SkillData positive = MakeSkill("긍정의힘", "긍정의힘 — 디버프 해제(맵 전체 오라)",
            "사장님 10-06 「아군 디버프 해제」. 같은 레인 아군 전체와 자기가 받는 아군발 디버프(이속 감소·공격력 감소 등)를 무시한다. 임장혁 「고충해소」와 같은 효과(DispelAllyDebuffs 공유, Allies+Self 한 쌍).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = MapWide, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Allies, multiplier = 1f, buffId = "JAEYUN_DISPEL" },
                    new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.Self, multiplier = 1f, buffId = "JAEYUN_DISPEL" },
                },
            });

        SkillData monkey = MakeSkill("원숭이의민첩함", "원숭이의민첩함 — 공중 이동",
            "사장님 10-06 「공중이동」. 이 유닛이 바다를 지나 다닌다(UnitData.movementAbility = Flying). 이 스킬 에셋은 이름·설명만 있는 표시용(효과 0).",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect>() });

        SkillData evil = MakeSkill("내면의악", "내면의악 — 치밀한수작(방어 감소) · 낯선공포(이동속도 감소)",
            "사장님 10-06 「유닛 수가 최대−10~최대일 때 켜지는 오라」. 내 레인의 적 수(HUD 「유닛 카운트」, 한계 70·난이도별)가 [한계−10, 한계]일 때만 내 레인의 적 전체에 방어 −10(치밀한수작)·이동속도 −30%(낯선공포, 남는 속도 0.7)가 걸린다. 수치는 제안값(박민석 방깍 5·흑인 이감 0.7과 같은 척도).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = MapWide, gaugeKind = SkillGaugeKind.Mana, laneCountWindow = 10,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.ArmorBonus, target = SkillTargetKind.Enemies, multiplier = -10f },
                    new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.7f },
                },
            });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { stun, health, command, positive, monkey, evil };
        unit.unitName = Title;
        unit.movementAbility = MovementAbility.Flying;
        unit.manaMax = 50f;        // 체력스킬 카운터(평타 +1, 50타째 발동) — 노태현과 같은 방식
        unit.trait = null;         // 원작 교체 스킬(원작007_H08V)이 슬롯 0을 덮는 능력 교체형 — 사장님·PM 결정: 특성 연결을 끊는다(특성 에셋은 그대로)
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("히든_석성례"), count = 1 },        // 석성례 팔방미인
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_임채현"), count = 1 },    // 임채현 호모사피엔스
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_서아인"), count = 1 },      // 서아인 사이코패스
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_박예원"), count = 1 },      // 박예원 성대결절
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("안흔함_김경현"), count = 1 },      // 김경현(칭호 없는 재료 = 기본 유닛, PM 확정)
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },    // 초월위습(모든 초월 식에 무조건)
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);

        // ── 종이비행기(유물) — 스토리 7 버스터콜 지령서 자리 대신 ──
        ItemData plane = AssetDatabase.LoadAssetAtPath<ItemData>(PlaneItemPath);
        if (plane == null)
        {
            plane = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(plane, PlaneItemPath);
        }
        plane.itemName = "종이비행기";
        plane.hasGrade = buster.hasGrade;
        plane.grade = buster.grade;
        plane.isRelic = true;
        plane.icon = null;   // 비슷한 아이콘이 없다 — 글자만(손거울과 같다)
        plane.linkedAbilityId = "";
        plane.sellWoodMin = 0;
        plane.sellWoodMax = 0;
        plane.effects = new List<ItemEffect>();
        plane.useKind = ItemUseKind.PaperPlane;
        plane.tooltipText = "유물. 누르면 정지시킬 적 하나를 고른다 — 그 적은 보스라도 영구 정지, 내 레인의 모든 적은 현재체력 −25%. 한 판에 한 번만, 아이템은 남는다.";
        plane.designNote = "사장님 10-06 유물 「종이비행기」: 스토리 7(1/20) 버스터콜 지령서 자리를 대신한다(원작 버스터콜 1/20 → 종이비행기 1/20). 인벤토리에서 눌러 쓴다 · 타겟 지정으로 직접 고르기(보스 가능) · 체력 −25%는 사용자 레인 적 전체(보스·스토리 적도 그 레인에 있으면 포함) · 정지된 보스는 끝까지 멈춤 · 한 번 쓰면 재사용 불가. 구현: PaperPlane.TryUse.";
        EditorUtility.SetDirty(plane);

        int replaced = 0;
        if (story.itemDrops != null)
            foreach (EnemyItemDrop drop in story.itemDrops)
                if (drop.item == buster || drop.item == plane) { drop.item = plane; drop.message = "★아이템:\n종이비행기 - 유물 획득!"; replaced++; }
        EditorUtility.SetDirty(story);

        AssetDatabase.SaveAssets();
        NetSetup.BuildCatalog();
        int nulls = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"이재윤 적용: 스킬 {old.Count}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.DisplayName}」 · 비행 {unit.movementAbility} · trait {(unit.trait == null ? "없음" : unit.trait.name)} · 재료 {recipe.ingredients.Count}종{(nulls > 0 ? $" · ⚠️ 빈 재료 {nulls}" : "")} · 입력말 {recipe.chatPhrase} · 종이비행기 {PlaneItemPath} · 스토리 7 보상 {replaced}칸 교체(확률 {story.itemDropChance:F4}) · 버스터콜 지령서 에셋 유지";
    }
}
