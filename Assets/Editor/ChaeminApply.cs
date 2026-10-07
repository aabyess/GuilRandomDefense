using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 초월 임채민 「청렴결백」(초월_임채민_AP) 적용(사장님 10-07 확정, PM 전달) — 원작 검은수염 이식 스킬을 빼고 사장님 스킬 4개로 교체. 호출: call ChaeminApply.Apply (다시 불러도 안전).
///  · 예베시간 = 공속 8%: 반경 850 주변 아군(자기 포함) 공격속도 +8% 오라
///  · 지상낙원 = 발동(마젠4, 범위증폭10%) 3초: 평타 25% 확률로 반경 935(=850×1.1) 안 아군(자기 포함) 마나 재생 +4/초 3초
///  · 축복의땅 = 발동(지정, 체젠4, 디버프해제99%) 3초: [지정] 칸으로 아군 1기 클릭 지정(다시 고르면 바뀜) + 평타 25% 확률로 그 유닛 체력 재생 +4/초 · 아군발 디버프 해제(99%) 3초
///  · 천벌 = 마나스킬(스턴3초, 전체체력1%): 마나 120 → 반경 500 적 전부 스턴 3초 + 최대 체력 1%(방어 무시). 특성강화 3pt → 1% 대신 고정 3,000,000(방어 무시)
/// 평타 발동 확률 25%·마나 120·쿨 1초는 제안값(원작 근거 없음).
/// </summary>
static class ChaeminApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string UnitPath = "Assets/Data/Units/Roster/초월_임채민_AP.asset";
    const string RecipePath = "Assets/Data/Recipes/초월_임채민_AP.asset";
    const string TraitPath = "Assets/Data/Traits/Trait_초월_임채민_AP.asset";
    const float ProcChance = 0.25f;

    static SkillData MakeSkill(string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[][] levels)
    {
        string path = $"{SkillFolder}/SkillData_사장님_초월_임채민_AP_{suffix}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillData>(); AssetDatabase.CreateAsset(skill, path); }
        skill.skillName = skillName;
        skill.description = description;
        skill.triggerType = trigger;
        skill.levels = new List<SkillLevel>();
        foreach (SkillEffect[] effects in levels)
            skill.levels.Add(new SkillLevel { cooldown = 0f, triggerChance = chance, range = range, hitCountThreshold = hitThreshold, resetTo = 0, gaugeKind = gauge, effects = new List<SkillEffect>(effects) });
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillEffect[] One(params SkillEffect[] e) => e;

    static string Apply()
    {
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>(UnitPath);
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);
        var trait = AssetDatabase.LoadAssetAtPath<UnitTraitData>(TraitPath);
        if (unit == null || recipe == null) return "❌ 초월_임채민_AP 유닛·조합식 에셋 없음";

        SkillData worship = MakeSkill("예베시간", "예베시간 — 공속 8%(주변 아군 오라)",
            "사장님 10-07 「공속8%」(확정: 주변 아군 오라). 반경 850 안 아군과 자기의 공격속도 +8%. 이름은 원문 그대로 「예베시간」(예배시간 오타일 수 있음).",
            SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.08f, buffId = "CHAEMIN_AS" },
                new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.08f, buffId = "CHAEMIN_AS" }));

        SkillData paradise = MakeSkill("지상낙원", "지상낙원 — 발동: 마젠 4(범위증폭 10%) 3초",
            "사장님 10-07 「발동(마젠4, 범위증폭10%) 3초」(확정: 평타 확률 발동 3초 지속). 평타 25%(제안값) 확률로 반경 935(850 × 범위증폭 1.1) 안 아군과 자기의 마나 게이지 재생 +4/초를 3초간.",
            SkillTriggerType.OnHitChance, 935f, ProcChance, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.ManaRegenBuff, target = SkillTargetKind.Allies, multiplier = 4f, duration = 3f, buffId = "CHAEMIN_MANA" },
                new SkillEffect { kind = SkillEffectKind.ManaRegenBuff, target = SkillTargetKind.Self, multiplier = 4f, duration = 3f, buffId = "CHAEMIN_MANA" }));

        SkillData blessPick = MakeSkill("축복의땅_지정", "축복의땅 — 지정(아군 1기 클릭)",
            "사장님 10-07 「발동(지정, …)」(확정: 플레이어가 아군 1기를 클릭해 지정, 다시 클릭하면 대상이 바뀐다). 명령 카드 칸을 누른 뒤 내 아군을 클릭하면 그 유닛이 지정 아군이 된다. 효과는 「축복의땅 — 발동」이 낸다.",
            SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.DesignateAlly, target = SkillTargetKind.Self }));
        blessPick.levels[0].cooldown = 1f;
        blessPick.levels[0].needsAllyClick = true;
        EditorUtility.SetDirty(blessPick);

        SkillData bless = MakeSkill("축복의땅", "축복의땅 — 발동: 지정 아군 체젠 4 · 디버프 해제 99% 3초",
            "사장님 10-07 「발동(지정, 체젠4, 디버프해제99%) 3초」. 평타 25%(제안값) 확률로 지정 아군(축복의땅 — 지정)에게 체력 게이지 재생 +4/초를 3초간, 99% 확률로 아군발 디버프(이속 감소·공격력 감소 오라)를 3초간 무시하게 한다. 지정 아군이 없으면 나가지 않는다.",
            SkillTriggerType.OnHitChance, 0f, ProcChance, 0, SkillGaugeKind.Mana,
            One(new SkillEffect { kind = SkillEffectKind.LifeRegenBuff, target = SkillTargetKind.DesignatedAlly, multiplier = 4f, duration = 3f, buffId = "CHAEMIN_LIFE" },
                new SkillEffect { kind = SkillEffectKind.DispelAllyDebuffs, target = SkillTargetKind.DesignatedAlly, multiplier = 1f, duration = 3f, chance = 0.99f, buffId = "CHAEMIN_DISPEL" }));

        // 천벌 — 레벨 1 최대체력 1%, 레벨 2(특성강화 3pt) 고정 3,000,000. 둘 다 반경 500 스턴 3초 + 방어 무시 AP.
        SkillEffect stun = new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 3f };
        SkillEffect pct = ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.01f, DamageType.AP, SkillTargetKind.Enemies);
        SkillEffect fixedHit = ImmortalKit.Flat(3000000f, DamageType.AP, SkillTargetKind.Enemies); fixedHit.armorIgnoreRatio = 1f;
        SkillEffect stun2 = new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 3f };
        SkillData wrath = MakeSkill("천벌", "천벌 — 마나스킬(반경 500 스턴 3초 + 최대체력 1%, 특성강화 시 고정 3,000,000)",
            "사장님 10-07 「마나스킬(스턴3초, 전체체력1%)」·「특성강화3(전체체력에서 고정데미지로 변경)」(확정: 반경 500 적 전부, 방어 무시, 고정 3,000,000). 마나 120이 차면 반경 500 안 적 전부 스턴 3초 + 각자 최대 체력의 1%(마법, 방어 무시). 특성 포인트 3으로 강화하면 1% 대신 고정 3,000,000(방어 무시). 마나 120은 제안값.",
            SkillTriggerType.OnHitCount, 500f, 1f, 120, SkillGaugeKind.Mana,
            One(stun, pct), One(stun2, fixedHit));

        var old = new List<string>();
        if (unit.skills != null) foreach (SkillData s in unit.skills) if (s != null) old.Add(s.name);
        ImmortalKit.SetUnit(unit, "청렴결백", new List<SkillData> { worship, paradise, blessPick, bless, wrath }, 120f, 0f);
        if (trait != null)
        {
            trait.targetUnit = unit;
            trait.traitName = "천벌 특성강화";
            trait.description = "사장님 10-07: 특성 포인트 3개로 강화하면 천벌의 최대 체력 1% 피해가 고정 3,000,000(방어 무시)으로 바뀐다(한 번). 스킬승급형(skillLevelUnlockIndex 1) — 천벌의 레벨 2다.";
            trait.costTraitPoints = 3;
            trait.skillLevelUnlockIndex = 1;
            trait.effects = new List<TraitEffect>();
            EditorUtility.SetDirty(trait);
            unit.trait = trait;
        }
        EditorUtility.SetDirty(unit);
        recipe.chatPhrase = "흔들리지않는신앙심";
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"임채민 청렴결백 적용: 스킬 {old.Count}개({string.Join(",", old)}) 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 특성 {(trait != null ? trait.traitName : "❌없음")} · 입력말 {recipe.chatPhrase} · 재료 {ImmortalKit.Names(recipe)}";
    }
}
