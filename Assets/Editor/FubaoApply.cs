using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 김민준 「푸바오」(초월_김민준_AP, 마딜) 적용(사장님 10-06): 구 게이트 스킬 3개를 빼고 사장님 스킬 4 + 공용 디버프 「외동」으로 교체 · 칭호 · 재료 · 입력말(외동판다!) · 소환 유닛 「산하 동료」.
/// 호출: call FubaoApply.Apply (다시 불러도 안전 — 같은 이름 에셋은 덮어쓴다). 설계표: Docs/design/FUBAO_DESIGN_2026-10-06.md.
///  ① 찍어누르기 = 마나 스킬 끝딜: 마나 게이지 135(원작 본인 타시기 Tasigi_03 135) · 대상 잃은 체력의 2%(TargetMissingHpPercent) AP — 「잃은 체력 2%」는 사장님 수치, 135는 제안값.
///  ② 포커싱오더 = 액티브 토글(Q): 켜면 본인+소환수가 사거리 안에서 잃은 체력 많은 적을 먼저 친다(SkillLevel.toggleMode · UnitAttacker.FocusLostHp) — 사장님 확정.
///  ③ 특출난분석력: 평타 25% · 대상 마법방어 −6%(AegrStack +6레벨, 레벨당 0.01) 영구 누적 — 사장님 확정(25%·영구 누적).
///  ④ 산하동료호출: 평타 10% · 「산하 동료」 1기 20초(SummonUnit) — 사장님 확정, 수치는 제안값(공격력 본체 50%·hp 100·제자리).
/// </summary>
static class FubaoApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_김민준_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_김민준_AP.asset";
    const string SummonPath = "Assets/Data/Units/Summons/Summon_푸바오_산하동료.asset";
    const string SummonTemplate = "Assets/Data/Units/Summons/Summon_시노부_분신.asset";
    const string Title = "푸바오";
    const string Phrase = "외동판다!";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, params SkillLevel[] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_김민준_AP_{suffix}.asset";
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

    static UnitData MakeSummon(UnitData body)
    {
        UnitData summon = AssetDatabase.LoadAssetAtPath<UnitData>(SummonPath);
        if (summon == null)
        {
            if (!AssetDatabase.CopyAsset(SummonTemplate, SummonPath)) return null;   // 시노부 분신 형식(프리팹·시스템 유닛 표지)을 그대로 복사
            summon = AssetDatabase.LoadAssetAtPath<UnitData>(SummonPath);
        }
        summon.unitName = "산하 동료";   // 화면 이름은 DisplayPerson이 「푸바오 산하 동료」로 만든다(에셋 이름 둘째 토막)
        summon.skinAlias = "";
        summon.damageType = body.damageType;
        summon.attackType = body.attackType;
        summon.hp = 100f;
        summon.attackPower = Mathf.Round(body.attackPower * 0.5f);   // 제안값 — 본체 공격력의 50%
        summon.attackSpeed = body.attackSpeed;
        summon.attackRange = body.attackRange;
        summon.moveSpeed = 0f;                                          // 제자리(시노부 분신과 같다)
        summon.skill = null;
        summon.skills = new List<SkillData>();
        summon.trait = null;
        EditorUtility.SetDirty(summon);
        return summon;
    }

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var debuff = AssetDatabase.LoadAssetAtPath<SkillData>(MinseokApply.DebuffPath);
        if (unit == null || recipe == null) return "❌ 초월_김민준_AP 유닛·조합식 에셋 없음";
        if (debuff == null) return $"❌ 공용 디버프 「외동」 에셋 없음({MinseokApply.DebuffPath}) — MinseokApply.Apply 먼저";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        UnitData summon = MakeSummon(unit);
        if (summon == null) return $"❌ 소환 유닛 복사 실패({SummonTemplate})";

        SkillData crush = MakeSkill("찍어누르기", "찍어누르기 — 마나스킬(끝딜: 잃은 체력 2%)",
            "사장님 10-06 「끝딜(잃은 체력 2%)」, 끝딜 분류상 마나 스킬. 마나 게이지 135에 닿으면 대상 한 기에게 그 적이 잃은 체력(최대−현재)의 2% 마법 피해. 게이지 135는 원작 본인(해군의 홍일점 타시기 Tasigi_03) 마나 135 기준 — 제안값. 보스 상한 없음.",
            SkillTriggerType.OnHitCount,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 1f, range = 0f,
                gaugeKind = SkillGaugeKind.Mana, hitCountThreshold = 135, resetTo = 0,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMissingHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.02f },
                },
            });

        SkillData focus = MakeSkill("포커싱오더", "포커싱오더",
            "사장님 10-06 「활성화 토글」. 켜면 푸바오 본인과 푸바오가 부른 소환수만, 사거리 안에서 잃은 체력(최대−현재)이 가장 많은 적을 먼저 친다. 다시 누르면 꺼진다. 다른 아군에는 영향 없음. 새 표적을 고를 때부터 적용(지금 치는 적이 살아 있으면 그 적이 죽은 뒤).",
            SkillTriggerType.ActiveButton,
            new SkillLevel { cooldown = 0f, toggleMode = true, effects = new List<SkillEffect>() });

        SkillData analysis = MakeSkill("특출난분석력", "특출난분석력 — 마법방어 감소(발동)",
            "사장님 10-06 「적 마방 −6%」. 평타 25% 확률로 맞은 적의 마법방어를 6% 깎는다(마방깎 +6레벨, 영구 누적 — 박민석 외동Lv.Devil의 마방깍과 같은 축, 엔진이 정한 상한까지). 확률 25%·영구 누적은 사장님 확정.",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 0.25f, range = 0f,
                gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.AegrStack, target = SkillTargetKind.SingleTarget, multiplier = 6f },
                },
            });

        SkillData call = MakeSkill("산하동료호출", "산하동료호출 — 소환",
            "사장님 10-06. 평타 10% 확률로 「산하 동료」 1기를 푸바오 앞에 20초 동안 소환한다. 이미 있으면 새로 안 부르고 남은 시간을 20초로 되돌린다. 소환수는 유닛 수·판매·조합 재료에서 제외. 소환 유닛 수치(공격력 본체 50%·hp 100·제자리)·모델(임시)은 제안값.",
            SkillTriggerType.OnHitChance,
            new SkillLevel
            {
                cooldown = 0f, triggerChance = 0.10f, range = 0f,
                gaugeKind = SkillGaugeKind.Mana,
                effects = new List<SkillEffect>
                {
                    new SkillEffect { kind = SkillEffectKind.SummonUnit, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { summon }, summonLifetime = 20f, summonFanDegrees = 35f, summonRadius = 70f },
                },
            });

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { crush, focus, analysis, call, debuff };
        unit.unitName = Title;
        EditorUtility.SetDirty(unit);

        recipe.ingredients = new List<RecipeIngredient>
        {
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("전설적인_김민준"), count = 1 },   // 김민준(초대마스터)
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("히든_여은서"), count = 1 },        // 여은서 마빡이
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("희귀함_장하민"), count = 1 },      // 장하민 캐나다갱단
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_박민수"), count = 1 },      // 박민수 루키인싸
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("특별함_김정래"), count = 1 },      // 김정래 프로그래머
            new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = Roster("초월위습_박은석"), count = 1 },    // 초월위습(모든 초월 식에 무조건)
        };
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);

        AssetDatabase.SaveAssets();
        int nulls = recipe.ingredients.FindAll(i => i.unit == null).Count;
        return $"푸바오 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.DisplayName}」 · 재료 {recipe.ingredients.Count}종{(nulls > 0 ? $" · ⚠️ 빈 재료 {nulls}" : "")} · 입력말 {recipe.chatPhrase} · 소환 「{summon.DisplayName}」 공격력 {summon.attackPower:F0}";
    }
}
