using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 제한됨 8종 중 막히지 않은 것 적용(사장님 10-06 + PM 지시, 설계표 Docs/design/LIMITED_RECIPES_DESIGN_2026-10-06.md). 호출: call LimitedApply.Apply (다시 불러도 안전).
///  재료: 김강민·이유범 희귀 박기찬 → 특별 박기찬 · 김민규 희귀 강주혁 → 특별 강주혁·전설 김용태 → 특별 김용태(행운의토큰 1개 비용은 이미 있다). 박성호(「분실된지갑」)는 사장님 답 대기라 안 건드림.
///  김민규: 스턴(평타 1/6 반경 405 1.0초) · 방무뎀(평타 1/10 깡딜 1,000,000 AP 방어 무시) · 이감(평타 1/8 0.5·3초) · 체력스킬 유닛삭제(체력 게이지 50 — 박민석 공복상태 틀 KillNormalEnemies) · 유닛회유(불멸 RecruitEnemy 그대로: 평타 0.8%·5기·팔면 37%)
///    — 쿨스킬 「반경 1000 보물위치공개」는 사장님 답 대기라 뺌.
///  강보명: 스플래시(attackSplashRadius 300) · 범퍼(평타 15% 맞은 적 중심 반경 300 현재체력 2% 방무) · 보잡(×1.3) · 단일암브(평타 1/8 −5, 원작 평타 발동 방깎 1~9 중앙) · 멀티샷(attackExtraTargets 3, 이이삭·박기찬 선례) · 광폭화(B06B 상대 ×1.5, 문필환 선례)
///  이충민: 두뇌Lv.G.O.D = 마젠 3.5 + 체젠 3.5(반경 850 오라, 불멸 고도현 틀) · 발명품제작 = 마나 120 → 금화 3,000엔/목재 1/랜덤유닛 위습 1/소환수 1기(20초)/상붕카 1기 균등(GrantInvention) · 확률조작 = 오라 반경 850 평타 확률 스킬 확률 ×1.25(SkillTriggerChanceBonus) · 보조딜 = 역할 표시(스킬 없음)
///  전법규: 마나 145 → 반경 600 스턴 2.5초 + 대상 잃은 체력 6%(끝딜 규칙: 마나 스킬 = 잃은체력, 보스 상한 없음 — 「분신4%」는 사장님 답 대기라 뺌) · 이감 50% 오라(반경 850, 신지우 확정과 같은 읽기) · 순간이동(배성령 틀, 쿨 12)
/// 수치는 전부 제안값(원작·선례 근거를 각 설명에 적었다). 제한됨은 채팅 전용이 아니다 — 조합 버튼 그대로.
/// </summary>
static class LimitedApply
{
    const string SkillFolder = "Assets/Data/UnitSkills";
    const string RecruitPath = "Assets/Data/Units/Summons/Summon_회유_적.asset";
    const string InventionSummonTemplate = "Assets/Data/Units/Summons/Summon_시노부_분신.asset";
    const string InventionSummonPath = "Assets/Data/Units/Summons/Summon_이충민_발명품.asset";

    static UnitData U(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static CombineRecipe R(string n) => AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{n}.asset");

    static SkillData MakeSkill(string unit, string suffix, string skillName, string description, SkillTriggerType trigger, float range, float chance, int hitThreshold, SkillGaugeKind gauge, params SkillEffect[] effects)
    {
        string path = $"{SkillFolder}/SkillData_사장님_제한_{unit}_{suffix}.asset";
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

    static SkillData Label(string unit, string suffix, string name, string description)
        => MakeSkill(unit, suffix, name, description, SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana);

    static int Replace(CombineRecipe r, string oldUnit, UnitData now)
    {
        int n = 0;
        foreach (RecipeIngredient ing in r.ingredients)
            if (ing != null && ing.unit != null && ing.unit.name == oldUnit) { ing.unit = now; n++; }
        return n;
    }

    static string Apply()
    {
        var log = new List<string>();
        var recruit = AssetDatabase.LoadAssetAtPath<UnitData>(RecruitPath);
        if (recruit == null) return "❌ 회유 유닛 에셋(Summon_회유_적) 없음 — ImmortalApplyC.Apply 먼저";

        // ── 재료 ──
        log.Add($"김강민 박기찬 {Replace(R("제한_김강민"), "희귀함_박기찬", U("특별함_박기찬"))}");
        log.Add($"이유범 박기찬 {Replace(R("제한_이유범"), "희귀함_박기찬", U("특별함_박기찬"))}");
        CombineRecipe minkyuRecipe = R("제한_김민규");
        log.Add($"김민규 강주혁 {Replace(minkyuRecipe, "희귀함_강주혁", U("특별함_강주혁"))} · 김용태 {Replace(minkyuRecipe, "전설적인_김용태", U("특별함_김용태"))}");
        foreach (string r in new[] { "제한_김강민", "제한_이유범", "제한_김민규" }) EditorUtility.SetDirty(R(r));

        // ── 김민규 ──
        UnitData minkyu = U("제한_김민규");
        var minkyuSkills = new List<SkillData>
        {
            MakeSkill("김민규", "스턴", "스턴 — 평타 발동", "사장님 10-06 「스턴」. 평타 1/6 확률로 반경 405 안 적 스턴 1.0초(팔레트).", SkillTriggerType.OnHitChance, 405f, 1f / 6f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 1f }),
            MakeSkill("김민규", "방무뎀", "방무뎀 — 방어 무시 깡딜", "사장님 10-06 「방무뎀」(방어 무시 피해). 평타 1/10 확률로 맞은 적에게 고정 1,000,000(AP = 방어 무시, 팔레트 깡딜).", SkillTriggerType.OnHitChance, 0f, 0.1f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.Flat, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 1000000f }),
            MakeSkill("김민규", "이감", "이감 — 평타 발동", "사장님 10-06 「이감」. 평타 1/8 확률로 맞은 적 이동속도 −50%(남는 속도 0.5) 3초(팔레트).", SkillTriggerType.OnHitChance, 0f, 0.125f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.SingleTarget, multiplier = 0.5f, duration = 3f }),
            MakeSkill("김민규", "유닛삭제", "체력스킬 — 유닛삭제", "사장님 10-06 「체력스킬(유닛삭제)」. 체력 게이지(평타 +1) 50(이 유닛 원작 LIFE 50)에 차면 사거리 700 안 가장 가까운 일반 적(보스·스토리 제외) 1기를 즉시 지운다 → 게이지 0. 박민석 공복상태·이태훈 약자멸시와 같은 틀(KillNormalEnemies).", SkillTriggerType.OnHitCount, 700f, 1f, 50, SkillGaugeKind.Life,
                new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self }),
            MakeSkill("김민규", "유닛회유", "유닛회유 — 평타 0.8%(팔기가능)", "사장님 10-06 「유닛회유(팔기가능)」 — 불멸 김용태·정준영과 같은 시스템 그대로(사장님 확정 재사용): 평타 1회당 0.8% 확률로 사거리 700 안 가장 가까운 일반 적 1기를 내 유닛으로(동시 5기, 공격력 = 평타 10%), 판매 버튼으로 팔면 37% 랜덤위습 1·그중 40% +100엔·목재 1.", SkillTriggerType.OnHitChance, 700f, 0.008f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.RecruitEnemy, target = SkillTargetKind.Self, summonUnits = new List<UnitData> { recruit }, multiplier = 0.10f, bonus = 5f }),
        };
        minkyu.skill = null; minkyu.skills = minkyuSkills; minkyu.lifeGaugeMax = 50f; minkyu.lifeGaugeCustomHitGain = false;
        EditorUtility.SetDirty(minkyu);
        log.Add($"김민규 스킬 {minkyuSkills.Count}");

        // ── 강보명 ──
        UnitData bomyeong = U("제한_강보명");
        var bomyeongSkills = new List<SkillData>
        {
            Label("강보명", "스플래시", "스플래시", "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300(UnitData.attackSplashRadius)의 같은 레인 적에게도 같은 피해(원작 최상호 AD 300 선례). 이 스킬 자체엔 효과가 없다(이름·설명만, 로스터 필드가 일한다)."),
            MakeSkill("강보명", "범퍼", "범퍼 — 현재체력 2%(반경 300)", "사장님 10-06 「범퍼(현재체력2%)」(범퍼 = 범위 전체 확정). 평타 15% 확률로 맞은 적 중심 반경 300 안 모든 적에게 현재 체력의 2%(AD, 방어 무시 — %체력 일괄 규칙, 신문철 사고뭉치와 같은 값).", SkillTriggerType.OnHitChance, 300f, 0.15f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetCurrentHpPercent, target = SkillTargetKind.Enemies, damageType = DamageType.AD, attackType = AttackType.Unassigned, multiplier = 0.02f, armorIgnoreRatio = 1f }),
            MakeSkill("강보명", "보잡", "보잡 — 보스 피해 ×1.3", "사장님 10-06 「보잡」(용어: 보스 상대 피해 ×1.3 — 김만경·박민석 확정과 같은 값).", SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.BossDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f }),
            MakeSkill("강보명", "단일암브", "단일암브 — 평타 발동", "사장님 10-06 「단일암브」(암브 = 단일 방깍 = 아머브레이크). 평타 1/8 확률로 맞은 적 한 기의 방어 −5(원작 평타 발동 방깎 1~9의 중앙값 — 사장님이 수치를 안 줘서 문필환 「꺽을수없는고집」과 같게).", SkillTriggerType.OnHitChance, 0f, 0.125f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.ArmorBreak, target = SkillTargetKind.SingleTarget, multiplier = 5f }),
            Label("강보명", "멀티샷", "멀티샷", "사장님 10-06 「멀티샷」. 평타가 주 대상 + 곁의 적 3기에게 같은 피해(UnitData.attackExtraTargets 3, 반경 = 사거리 — 사장님이 발수를 안 줘서 불멸 이이삭 「멀티샷(3)」·박기찬과 같게). 이 스킬 자체엔 효과가 없다(이름·설명만)."),
            MakeSkill("강보명", "광폭화", "광폭화 — 광폭화 몹 상대 강화", "사장님 10-06 「광폭화」(원랜디 광폭화 전담 읽기). 광폭화 몹(B06B)을 칠 때 이 유닛 평타·스킬 최종 피해 ×1.5(문필환 「꺽을수없는고집」과 같은 값, 제안값).", SkillTriggerType.Aura, 0f, 1f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.DamageVsTargetBuff, target = SkillTargetKind.Self, multiplier = 0.5f, buffId = "B06B" }),
        };
        bomyeong.skill = null; bomyeong.skills = bomyeongSkills;
        bomyeong.attackSplashRadius = 300f;
        bomyeong.attackExtraTargets = 3; bomyeong.attackExtraTargetRadius = bomyeong.attackRange;
        EditorUtility.SetDirty(bomyeong);
        log.Add($"강보명 스킬 {bomyeongSkills.Count}");

        // ── 이충민 ──
        UnitData chungmin = U("제한_이충민");
        UnitData summon = AssetDatabase.LoadAssetAtPath<UnitData>(InventionSummonPath);
        if (summon == null && AssetDatabase.CopyAsset(InventionSummonTemplate, InventionSummonPath)) summon = AssetDatabase.LoadAssetAtPath<UnitData>(InventionSummonPath);
        if (summon != null)
        {
            summon.prefab = AssetDatabase.LoadAssetAtPath<UnitData>(InventionSummonTemplate).prefab;
            summon.unitName = "발명품"; summon.attackPower = Mathf.Round(chungmin.attackPower * 0.3f); summon.attackRange = chungmin.attackRange; summon.attackSpeed = chungmin.attackSpeed;
            summon.moveSpeed = 0f; summon.hp = 100f; summon.skill = null; summon.skills = new List<SkillData>(); summon.trait = null;
            EditorUtility.SetDirty(summon);
        }
        WispData wisp = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_랜덤유닛.asset");
        SkillEffect Chance(SkillTargetKind t) => new SkillEffect { kind = SkillEffectKind.SkillTriggerChanceBonus, target = t, multiplier = 0.25f, buffId = "CHUNG_CHANCE" };
        var chungminSkills = new List<SkillData>
        {
            Label("이충민", "두뇌", "두뇌Lv.G.O.D — 마젠 3.5 · 체젠 3.5 · 보조딜", "사장님 10-06 「보조딜, 마젠3.5, 체젠3.5」(불멸 고도현 확정과 같은 읽기: 마젠·체젠 = 반경 850 주변 아군(자기 포함) 마나 게이지·체력 게이지 재생 오라 +3.5/초). 보조딜은 역할 표시(스킬 없음). 값은 UnitData.manaAura·lifeAura 필드에 걸려 있어 이 스킬 자체엔 효과가 없다(이름·설명만)."),
            MakeSkill("이충민", "발명품제작", "발명품제작 — 마나 스킬(금화·목재·위습·소환수·상붕카)", "사장님 10-06 「마나스킬(금화, 목재, 랜덤위습, 소환수, 상붕카)」(설계표 제안값). 마나 게이지(평타 +1) 120(원작 초월 마나 임계 115~160 안)에 차면 다섯 중 하나를 균등으로: 금화 3,000엔 / 목재 1 / 랜덤유닛 위습 1 / 소환수 1기(20초, 공격력 본체 30%) / 상붕카 1기(정식 유닛) → 게이지 0. 유재헌 토토와 같은 3,000엔·목재 1·위습 1.", SkillTriggerType.OnHitCount, 0f, 1f, 120, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.GrantInvention, target = SkillTargetKind.Self, multiplier = 3000f, bonus = 1f, rewardWisp = wisp, summonUnits = new List<UnitData> { summon, U("안흔함_상붕카") } }),
            MakeSkill("이충민", "확률조작", "확률조작 — 스킬발동확률증가(오라)", "사장님 10-06 「스킬발동확률증가」(설계표 제안값). 반경 850 안 아군(자기 포함)의 평타 확률 발동 스킬 확률이 ×1.25(상한 100%). 같은 오라끼리는 최댓값 하나.", SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
                Chance(SkillTargetKind.Allies), Chance(SkillTargetKind.Self)),
        };
        chungmin.skill = null; chungmin.skills = chungminSkills;
        chungmin.manaMax = 120f; chungmin.manaGaugePerMana = 1f;
        chungmin.manaAuraRegenPerSecond = 3.5f; chungmin.manaAuraRange = 850f; chungmin.manaAuraIncludesSelf = true; chungmin.manaAuraBuffId = "CHUNG_MANA";
        chungmin.lifeAuraRegenPerSecond = 3.5f; chungmin.lifeAuraRange = 850f; chungmin.lifeAuraIncludesSelf = true; chungmin.lifeAuraBuffId = "CHUNG_LIFE";
        EditorUtility.SetDirty(chungmin);
        log.Add($"이충민 스킬 {chungminSkills.Count}");

        // ── 전법규 ──
        UnitData beopgyu = U("제한_전법규");
        var beopgyuSkills = new List<SkillData>
        {
            MakeSkill("전법규", "마나스킬", "마나스킬 — 스턴 + 끝딜(잃은 체력 6%)", "사장님 10-06 「마나스킬(스턴)」 + 「끝딜(잃은체력6%)」(끝딜 규칙: 마나 스킬 = 잃은 체력 비례, 보스 상한 없음 — 「분신4%」는 사장님 답 대기라 뺌). 마나 게이지(평타 +1) 145(이 유닛 원작 마나)에 차면 반경 600 안 적 스턴 2.5초(원작 조로 Zoro_tiger 500범위·2.5초) + 맞은 적 한 기가 잃은 체력(최대−현재)의 6%(AP 방어 무시) → 게이지 0.", SkillTriggerType.OnHitCount, 600f, 1f, 145, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Stun, target = SkillTargetKind.Enemies, duration = 2.5f },
                new SkillEffect { kind = SkillEffectKind.Damage, basis = SkillEffectBasis.TargetMissingHpPercent, target = SkillTargetKind.SingleTarget, damageType = DamageType.AP, attackType = AttackType.Spells, multiplier = 0.06f }),
            MakeSkill("전법규", "이감", "이감 — 오라 −50%", "사장님 10-06 「이감(50%)」(불멸 신지우 확정과 같은 읽기: 이감 N% = 오라). 반경 850 안 적 이동속도 −50%(남는 속도 0.5).", SkillTriggerType.Aura, 850f, 1f, 0, SkillGaugeKind.Mana,
                new SkillEffect { kind = SkillEffectKind.Slow, target = SkillTargetKind.Enemies, multiplier = 0.5f, buffId = "BEOPGYU_SLOW" }),
        };
        SkillData teleport = MakeSkill("전법규", "순간이동", "순간이동 — 액티브(땅 지점 클릭)", "사장님 10-06 「순간이동」(배성령 「암살스킬」과 같은 틀 — 공용 TeleportToPoint): 명령 카드 액티브 칸 → 땅을 좌클릭하면 그 지점으로 순간이동(우클릭 취소). 쿨 12초. 호스트/싱글만.", SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.TeleportToPoint, target = SkillTargetKind.Self, multiplier = 0f });
        teleport.levels[0].cooldown = 12f; teleport.levels[0].needsPointClick = true;
        EditorUtility.SetDirty(teleport);
        beopgyuSkills.Add(teleport);
        beopgyu.skill = null; beopgyu.skills = beopgyuSkills; beopgyu.manaMax = 145f; beopgyu.manaGaugePerMana = 1f;
        EditorUtility.SetDirty(beopgyu);
        log.Add($"전법규 스킬 {beopgyuSkills.Count}");

        AssetDatabase.SaveAssets();
        return "제한됨 적용: " + string.Join(" · ", log);
    }
}
