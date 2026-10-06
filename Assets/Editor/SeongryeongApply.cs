using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 배성령 「투명색으로얼룩진감정」(초월_배성령_AD, 물딜) 적용(사장님 10-06 16번째, 설계표 Docs/research/TRANSCEND_SEONGRYEONG_DESIGN_2026-10-06.md — 「사장님 확정」 절).
/// 호출: call SeongryeongApply.Apply (다시 불러도 안전). 스킬 4:
///  · 무방비상태 = 방무뎀 50%: UnitData.attackArmorIgnoreRatio 0.5 — 평타·스킬 피해가 적 방어의 50%를 무시(적 방어 ×0.5). 스킬 자체엔 효과 없음(이름·설명만, 값은 로스터 필드).
///  · 확실한일처리 = 끝딜(마나 스킬): 마나 게이지 115 → 대상 잃은 체력 5%(보스 상한 없음, 강재규 끝딜과 같은 틀).
///  · 거짓된마음 = 이감: 평타 12.5% 맞은 적 남는 속도 0.5 3초(제안값).
///  · 암살스킬 = 순간이동: ActiveButton + 땅 지점 클릭 → NavMeshAgent.Warp(쿨 12초 제안값, 사거리 제한 없음) — 공용 SkillEffectKind.TeleportToPoint + SkillLevel.needsPointClick.
/// 재료: 전설 노태현 → 전설 임장혁(전설적인_임장혁), 희귀 배성령 제거 → 4칸. 칭호 「투명색으로얼룩진감정」 · 입력말 「인간의탈을쓴암살자」 · trait 비움.
/// </summary>
static class SeongryeongApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_배성령_AD.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_배성령_AD.asset";
    const string Title = "투명색으로얼룩진감정";
    const string Phrase = "인간의탈을쓴암살자";

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_배성령_AD_{suffix}.asset";
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
        if (unit == null || recipe == null) return "❌ 초월_배성령_AD 유닛·조합식 에셋 없음";
        UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

        SkillData noDefense = MakeSkill("무방비상태", "무방비상태 — 방무뎀 50%(패시브)",
            "사장님 10-06 「방무뎀(50%)」 확정: 적 방어의 50%를 무시한다 — 평타·스킬 피해 계산 때 적 방어 ×0.5(신 보스 방어 317.6 → 158.8: 방어 계수 0.136 → 0.239, 평타 실피해 약 1.76배). 값은 로스터 UnitData.attackArmorIgnoreRatio 0.5. 이 스킬 자체엔 효과가 없다(이름·설명만).",
            SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

        SkillData finisher = MakeSkill("확실한일처리", "확실한일처리 — 끝딜(마나 스킬 · 잃은 체력 5%)",
            "사장님 10-06 「끝딜(잃은체력)」 → 마나 스킬(끝딜 3종 규칙: 마나 스킬 = 잃은 체력 비례, 보스 상한 없음). 마나 게이지(평타 +1)가 115(원작 초월 마나 임계 115~160 안 — 이 유닛 기존 마나)에 차면 대상이 잃은 체력(최대−현재)의 5%(강재규 끝딜과 같은 값, 제안값) 피해, 보스도 같은 식 → 게이지 0.",
            SkillTriggerType.OnHitCount, 0f, 1f, 115, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMissingHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.05f });

        SkillData slow = MakeSkill("거짓된마음", "거짓된마음 — 이감(발동)",
            "사장님 10-06 「이감」. 평타 12.5% 확률로 맞은 적 이동속도 −50%(남는 속도 0.5) 3초 — 원작 초월 이감 발동형 0.25~0.75·2~5초 중앙, 양재모 로키포트 1/8·0.6·3초 선례. 제안값.",
            SkillTriggerType.OnHitChance, 0f, 0.125f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.5f, duration = 3f });

        SkillData teleport = MakeSkill("암살스킬", "암살스킬 — 순간이동(액티브: 땅 지점 클릭)",
            "사장님 10-06 확정 「버튼 눌러 고른 곳으로 순간이동」. 명령 카드 액티브 칸을 누르고 땅을 좌클릭하면 그 지점으로 순간이동한다(NavMesh 밖이면 가장 가까운 NavMesh 점, 우클릭 취소). 쿨 12초·사거리 제한 없음은 제안값. 돌아오지 않는다. 호스트/싱글만(멀티 클라는 대상 지정 액티브와 같은 제한).",
            SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.TeleportToPoint, target = SkillTargetKind.Self, multiplier = 0f });
        teleport.levels[0].cooldown = 12f;
        teleport.levels[0].needsPointClick = true;
        EditorUtility.SetDirty(teleport);

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        unit.skill = null;
        unit.skills = new List<SkillData> { noDefense, finisher, slow, teleport };
        unit.unitName = Title;
        unit.trait = null;
        unit.attackArmorIgnoreRatio = 0.5f;
        unit.manaMax = 115f;
        unit.manaGaugePerMana = 1f;
        EditorUtility.SetDirty(unit);

        // 재료: 전설 노태현 → 전설 임장혁(commandId로 대조 — 별칭 「짱스파단장」), 희귀 배성령 제거(사장님 표에 없음).
        UnitData jang = Roster("전설적인_임장혁");
        int swapped = 0;
        foreach (RecipeIngredient ing in recipe.ingredients)
            if (ing != null && ing.unit != null && ing.unit.name == "전설적인_노태현" && jang != null) { ing.unit = jang; swapped++; }
        int removed = recipe.ingredients.RemoveAll(i => i != null && i.unit != null && i.unit.name == "희귀함_배성령");
        recipe.chatPhrase = Phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        var names = new List<string>();
        foreach (RecipeIngredient ing in recipe.ingredients) names.Add(ing.unit != null ? ing.unit.name : "빈칸");
        return $"배성령 투명색으로얼룩진감정 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 방무뎀 {unit.attackArmorIgnoreRatio} · 마나 {unit.manaMax} · 재료 {recipe.ingredients.Count}종(노태현→임장혁 {swapped}건, 배성령 {removed}칸 삭제): {string.Join(", ", names)} · 입력말 {recipe.chatPhrase}";
    }
}
