using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 유재헌 「앰생파조장」(초월_유재헌_ADAP, 물딜+마딜) 적용(사장님 10-06 + 질문지 답, 설계표 Docs/design/JAEHEON_DESIGN_2026-10-06.md §7). 호출: call JaeheonApply.Apply (다시 불러도 안전).
///  ① 토사장의어둠의수하들 = 소환수 2기 상시(Summon_유재헌_수하A/B — 공격력 본체 30%·평타 10% 스턴 0.5초): CooldownAutoCast 5초 — 죽으면 다시 나온다(summonLifetime 99999)
///  ② 돈벌이 = 처치 골드 +0.2(GoldPlusBonus) · ③ 현상수배 = 내 레인 적 이속 −18% 오라(Slow 0.82, ownLaneOnly) + 이감 1%당 처치 골드·목재 +1%(SlowRewardBonus 0.01)
///  ④ 도망자의삶 = 순간이동(액티브, 땅 지점 클릭 — 배성령과 같은 TeleportToPoint 틀, 쿨 12초) · ⑤ 토토 = 엔 1,000 도박 버튼(FlexKind.Toto — 이 유닛을 고르면 명령 카드에 뜨는 칸; 스킬은 이름·설명만)
/// 칭호 「앰생파조장」 · 입력말 「영겁의도망꾼」 · trait 비움 · 재료 5종(전설 김민규 · 전설 홍인창 · 희귀 유재헌 · 희귀 이은엽 + 초월위습).
/// </summary>
static class JaeheonApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_유재헌_ADAP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_유재헌_ADAP.asset";
    const string SummonFolder = "Assets/Data/Units/Summons";
    const string SummonTemplate = "Assets/Data/Units/Summons/Summon_시노부_분신.asset";
    const string Title = "앰생파조장";
    const string Phrase = "영겁의도망꾼";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_유재헌_ADAP_{suffix}.asset";
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

    static UnitData MakeSummon(string name, UnitData body, SkillData stun)
    {
        string path = $"{SummonFolder}/{name}.asset";
        UnitData summon = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (summon == null)
        {
            if (!AssetDatabase.CopyAsset(SummonTemplate, path)) return null;   // 시노부 분신 형식(프리팹·시스템 유닛 표지)을 그대로 복사
            summon = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        }
        // 프리팹 참조는 템플릿(시노부 분신)의 현재 참조를 따라간다 — 모델 배선 때 fileID가 바뀌어도 매번 다시 이어진다(푸바오와 같은 이유).
        var template = AssetDatabase.LoadAssetAtPath<UnitData>(SummonTemplate);
        summon.prefab = template.prefab;
        summon.unitName = "토사장의 수하";
        summon.attackPower = Mathf.Round(body.attackPower * 0.3f);
        summon.attackRange = body.attackRange;
        summon.attackSpeed = body.attackSpeed;
        summon.moveSpeed = 0f;
        summon.hp = 100f;
        summon.trait = null;
        summon.skill = null;
        summon.skills = new List<SkillData> { stun };
        EditorUtility.SetDirty(summon);
        return summon;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        if (unit == null || recipe == null) return "❌ 초월_유재헌_ADAP 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData minionStun = MakeSkill("수하_스턴", "수하 — 평타 스턴(소환수 전용)",
            "사장님 10-06 「소환수(스턴, 보조딜)」. 소환수 「토사장의 수하」가 평타 10% 확률로 맞은 적 한 기를 0.5초 스턴. 소환수 유닛의 skills에만 걸린다.",
            SkillTriggerType.OnHitChance,
            new SkillLevel { cooldown = 0f, triggerChance = 0.10f, range = 0f, gaugeKind = SkillGaugeKind.Mana, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.SingleTarget, duration = 0.5f } } });
        UnitData minionA = MakeSummon("Summon_유재헌_수하A", unit, minionStun), minionB = MakeSummon("Summon_유재헌_수하B", unit, minionStun);
        if (minionA == null || minionB == null) return $"❌ 소환 유닛 복사 실패({SummonTemplate})";

        SkillData minions = MakeSkill("토사장의어둠의수하들", "토사장의어둠의수하들 — 소환수 2기 상시",
            "사장님 10-06 확정: 소환수 2기 상시(평타 10% 스턴 0.5초 · 공격력 본체 30%). 5초마다 사라진 종류만 다시 만든다(CooldownAutoCast — 이미 있으면 안 만들고 수명만 되돌림, 수명 99999초). 소환수는 유닛 수·판매·조합 재료에서 제외. 모델 임시(시노부 분신 형식).",
            SkillTriggerType.CooldownAutoCast,
            new SkillLevel
            {
                cooldown = 5f, triggerChance = 1f, range = 0f, gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.SummonUnit, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { minionA, minionB }, summonLifetime = 99999f, summonFanDegrees = 35f, summonRadius = 70f } },
            });

        SkillData earn = MakeSkill("돈벌이", "돈벌이 — 처치 골드 +0.2",
            "사장님 10-06 확정: 골드 획득 +20%. 이 유닛이 살아 있는 동안 주인의 처치 골드 배율 +0.2(황준석 「준석의담판」과 같은 GoldPlusBonus).",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, range = 0f, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.GoldPlusBonus, target = SkillTargetKind.Self, multiplier = 0.2f } } });

        SkillData wanted = MakeSkill("현상수배", "현상수배 — 이감 −18% + 이감 비례 보상",
            "사장님 10-06 「몹이감+18」 + 「이감이높을수록 금화·목재 획득량 증가」 확정: 내 레인 적 이동속도 −18%(남는 속도 0.82, 오라 — 남의 레인 적은 안 건드린다: ownLaneOnly) + 내 레인 적이 죽을 때 그 적이 받던 이감 1%당 처치 골드·목재 +1%(이속 82%면 +18%, SlowRewardBonus).",
            SkillTriggerType.Aura,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 50000f, ownLaneOnly = true,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.82f, buffId = "JAEHEON_WANTED" },
                    new SkillEffect { kind = SkillEffectKind.SlowRewardBonus, target = SkillTargetKind.Self, multiplier = 0.01f },
                },
            });

        SkillData fugitive = MakeSkill("도망자의삶", "도망자의삶 — 순간이동(액티브: 땅 지점 클릭)",
            "사장님 10-06 확정: 배성령 「암살스킬」과 같은 틀 — 명령 카드 액티브 칸을 누르고 땅을 좌클릭하면 그 지점으로 순간이동(NavMesh 밖이면 가장 가까운 점, 우클릭 취소). 쿨 12초(배성령 값에 맞춤). 호스트/싱글만.",
            SkillTriggerType.ActiveButton,
            new SkillLevel { cooldown = 12f, needsPointClick = true, effects = new List<SkillEffect> { new SkillEffect { kind = SkillEffectKind.TeleportToPoint, target = SkillTargetKind.Self, multiplier = 0f } } });

        SkillData toto = MakeSkill("토토", "토토 — 도박 버튼(엔 1,000 → 33%)",
            "사장님 10-06 확정: 이 유닛을 고르면 명령 카드에 「토토」 칸이 뜬다(GameHud FlexKind.Toto). 엔 1,000을 내고 33% 확률로 금화 3,000엔 / 목재 1 / 위습 1 중 하나(각 1/3). 실패하면 엔만 잃는다. 쿨 없음(엔이 곧 제한). 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura,
            new SkillLevel { cooldown = 0f, triggerChance = 1f, effects = new List<SkillEffect>() });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { minions, earn, wanted, fugitive, toto };
        unit.unitName = Title;
        unit.trait = null;   // 사장님 확정: 특성강화 없앰
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_김민규"), count = 1 },   // 김민규 미래를보는눈
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_홍인창"), count = 1 },   // 홍인창 고슴도치
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_유재헌"), count = 1 },    // 유재헌 토토교수
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_이은엽"), count = 1 },    // 이은엽 드럼신동
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },  // 초월위습(모든 초월 식에 무조건)
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        int missing = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"유재헌 앰생파조장 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · trait {(unit.trait == null ? "없음" : unit.trait.name)} · 소환수 {minionA.name}·{minionB.name}(공격력 {minionA.attackPower:F0}) · 재료 {recipe.ingredients.Count}종{(missing > 0 ? $" · ⚠️ 빈 재료 {missing}" : "")} · 입력말 {recipe.chatPhrase}";
    }
}
