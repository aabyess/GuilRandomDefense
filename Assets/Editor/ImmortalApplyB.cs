using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 불멸 8종 중 고도현·이이삭(새 코드: 영구 아군 버프 따라가기 · 방깍량 비례 피해) — 사장님 10-06 사양 + 확정(설계표 Docs/research/IMMORTAL_DESIGN_2026-10-06.md).
/// 호출: call ImmortalApplyB.Dohyeon / Isag / All (다시 불러도 안전).
/// </summary>
static class ImmortalApplyB
{
    static string All() => Dohyeon() + "\n" + Isag() + "\n" + Yongtae();

    static string Finish(string key, UnitData unit, CombineRecipe recipe, string phrase, int oldCount)
    {
        unit.trait = null;
        EditorUtility.SetDirty(unit);
        recipe.chatPhrase = phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"{key} 적용: 스킬 {oldCount}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 체력게이지 {unit.lifeGaugeMax} · 입력말 {recipe.chatPhrase} · 재료 {ImmortalKit.Names(recipe)}";
    }

    // ───────── 고도현 만물통달 (마딜) ─────────
    static string Dohyeon()
    {
        const string K = "고도현";
        var unit = ImmortalKit.Unit("불멸_고도현"); var recipe = ImmortalKit.Recipe("불멸_고도현");
        if (unit == null || recipe == null) return "❌ 고도현 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillEffect nuke = ImmortalKit.Flat(3450000f, DamageType.AP, SkillTargetKind.Enemies);
        nuke.armorIgnoreRatio = 1f;
        SkillData mana = ImmortalKit.ManaSkill(K, "무중생유", "무중생유 — 마나 스킬(범위 500 고정 3,000,000 × 범위증폭 1.15)",
            "사장님 10-06 「마나스킬(범위증폭15%)」(확정: 마나 160 → 범위 500 고정 3,000,000, 방어 무시). 범위증폭 15% = 이 스킬의 범위 피해 ×1.15 → 3,450,000(신 기준 R49 일반 7.75M의 44%, 방어 무시 AP).",
            160, 500f, nuke);
        SkillData mag = ImmortalKit.OnHit(K, "그의앞에서는무의미", "그의앞에서는무의미 — 마방깍(10)",
            "사장님 10-06 「마방깍(10)」. 평타 1/8 확률로 맞은 적의 마법 방어를 +10 약화(AegrStack, 박민석 선례).",
            0.125f, 0f, ImmortalKit.Aegr(10f));
        SkillData boom = ImmortalKit.AuraSkill(K, "폭발증폭기", "폭발증폭기 — 폭뎀 30%(스플래시 피해 ×1.3)",
            "사장님 10-06 「폭뎀30%」. 평타 스플래시(범위) 피해가 ×1.3(SkillEffectKind.SplashDamageMultiplier). 범위증폭 15%는 무중생유에 곱해 두었다(「같은 것의 둘」이 아니라 각자 자기 범위 피해에).",
            0f, new SkillEffect { kind = SkillEffectKind.SplashDamageMultiplier, target = SkillTargetKind.Self, multiplier = 1.3f });
        SkillData brain = ImmortalKit.Label(K, "두뇌LvMax", "두뇌Lv.Max — 마젠 3 · 체젠 3(주변 아군 오라)",
            "사장님 10-06 「마젠3, 체젠3」(확정: 반경 850 주변 아군 오라, 자기 포함). 주변 아군 마나 게이지 재생 +3/초(UnitData.manaAura*) · 체력 게이지 재생 +3/초(UnitData.lifeAura*, 게이지가 있는 아군만). 스킬 자체엔 효과가 없다(이름·설명만, 값은 로스터 필드).");
        SkillData cure = ImmortalKit.Skill(K, "약처방", "약처방 — 아군 1기 선택 영구 버프(공격력 +10% · 공속 +10%)",
            "사장님 10-06 「1기유닛선택(공증10%,공속10%,체젠1)」(확정: 영구, 대상을 옮기면 따라감). 명령 카드 칸을 누른 뒤 아군 유닛 하나를 클릭하면 그 유닛에 공격력 +10%·공격속도 +10%가 영구로 걸리고, 다른 아군을 다시 고르면 옛 대상에서 떼고 새 대상에 건다(쿨 5초 제안). 체젠 1(대상 체력 재생)은 우리 유닛에 체력이 없어 대상 쪽 훅이 없다 — 구현하지 않았다.",
            SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.10f, buffId = "DOHYEON_AD", duration = 0f },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Allies, multiplier = 0.10f, buffId = "DOHYEON_AS", duration = 0f });
        cure.levels[0].cooldown = 5f;
        cure.levels[0].needsAllyClick = true;
        EditorUtility.SetDirty(cure);

        ImmortalKit.SetUnit(unit, "만물통달", new List<SkillData> { mana, mag, boom, brain, cure }, 160f, 0f);
        unit.manaAuraRegenPerSecond = 3f; unit.manaAuraRange = 850f; unit.manaAuraBuffId = "DOHYEON_MANA"; unit.manaAuraIncludesSelf = true;
        unit.lifeAuraRegenPerSecond = 3f; unit.lifeAuraRange = 850f; unit.lifeAuraBuffId = "DOHYEON_LIFE"; unit.lifeAuraIncludesSelf = true;
        return Finish(K, unit, recipe, "전교1등고도현", old);
    }

    // ───────── 이이삭 천상천하유아독존 (물딜) ─────────
    static string Isag()
    {
        const string K = "이이삭";
        var unit = ImmortalKit.Unit("불멸_이이삭"); var recipe = ImmortalKit.Recipe("불멸_이이삭");
        if (unit == null || recipe == null) return "❌ 이이삭 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillEffect line = ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.005f, DamageType.AD, SkillTargetKind.Enemies);
        line.lineLength = 600f; line.lineStartRadius = 75f; line.lineEndRadius = 75f;
        SkillData seoul = ImmortalKit.OnHit(K, "서울수도권을먹은자", "서울수도권을먹은자 — 라인딜(최대체력 0.5%)",
            "사장님 10-06 「라인딜(전체체력0.5%)」. 평타 1/8 확률로 맞은 적 쪽으로 길이 600·폭 150의 직선 안 적 전부에게 각자 최대 체력의 0.5%(방어 무시).",
            0.125f, 0f, line);
        SkillData punch1 = ImmortalKit.OnHit(K, "핵펀치", "핵펀치 — 깡딜(평타 1/10 150만)",
            "사장님 10-06 「깡딜」. 평타 1/10 확률로 맞은 적에게 고정 1,500,000(신 기준 공통 팔레트).",
            0.10f, 0f, ImmortalKit.Flat(1500000f, DamageType.AD));
        SkillData punch2 = ImmortalKit.OnHit(K, "핵펀치_보잡", "핵펀치 — 보잡(보스 최대체력 5%)",
            "사장님 10-06 「보잡(전체체력5%)」. 평타 1/8 확률로 맞은 적이 보스(PV≥200)면 최대 체력의 5%를 방어 무시로 입힌다(신 기준 R50 보스 4.4M).",
            0.125f, 0f, ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.05f, DamageType.AD, SkillTargetKind.SingleTarget, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f));
        SkillData strongest = ImmortalKit.Label(K, "지상최강의남자", "지상최강의남자 — 스플래시 + 멀티샷(3)",
            "사장님 10-06 「스플래시, 멀티샷(3)」. 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(UnitData.attackSplashRadius) + 평타 때 공격자 반경 700 안 다른 적 3마리에게도 평타(attackExtraTargets). 스킬 자체엔 효과가 없다(이름·설명만, 값은 로스터 필드).");
        SkillData unstop = ImmortalKit.OnHit(K, "저지불가", "저지불가 — 단일스턴(평타마다 0.5초)",
            "사장님 10-06 「단일스턴(100%)」(확정: 평타 매번 0.5초). 평타가 맞을 때마다 그 적 한 기를 0.5초 스턴.",
            1f, 0f, ImmortalKit.Stun(0.5f, SkillTargetKind.SingleTarget));
        SkillData legend1 = ImmortalKit.AuraSkill(K, "3대1의전설", "3대1의전설 — 방깍 오라(−60)",
            "사장님 10-06 「방깍(60)」. 반경 850 안 모든 적 방어 −60(신지우 A0EJ −60/−70 선례).",
            850f, ImmortalKit.ArmorAura(60f, "ISAG_ARMOR"));
        SkillData legend2 = ImmortalKit.AuraSkill(K, "3대1의전설_방깍비례", "3대1의전설 — 방깍이높을수록데미지증가",
            "사장님 10-06 「방깍이높을수록데미지증가」(확정: 방깍 1당 +0.5%·최대 +50%). 맞는 적이 방깍(원본 방어 − 현재 방어)을 N 받고 있으면 이 유닛 피해가 ×(1 + min(0.5, 0.005 × N)).",
            0f, new SkillEffect { kind = SkillEffectKind.DamagePerTargetArmorShred, target = SkillTargetKind.Self, multiplier = 0.005f, bonus = 0.5f });

        ImmortalKit.SetUnit(unit, "천상천하유아독존", new List<SkillData> { seoul, punch1, punch2, strongest, unstop, legend1, legend2 }, 0f, 0f);
        unit.attackSplashRadius = 300f;
        unit.attackExtraTargets = 3; unit.attackExtraTargetRadius = 700f;
        return Finish(K, unit, recipe, "싸움패왕이이삭", old);
    }
    // ───────── 김용태 피지컬돼지 (물딜) — 유닛회유는 새 시스템이라 다음 단계(ImmortalApplyC) ─────────
    static string Yongtae()
    {
        const string K = "김용태";
        var unit = ImmortalKit.Unit("불멸_김용태"); var recipe = ImmortalKit.Recipe("불멸_김용태");
        var park = ImmortalKit.Unit("전설적인_박성호");
        if (unit == null || recipe == null || park == null) return "❌ 김용태 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillData splash = ImmortalKit.Label(K, "스플래시", "스플래시",
            "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(UnitData.attackSplashRadius). 스킬 자체엔 효과가 없다(이름·설명만).");
        SkillData burst = ImmortalKit.OnHit(K, "깡딜", "깡딜 — 평타 1/10 150만",
            "사장님 10-06 「깡딜」. 평타 1/10 확률로 맞은 적에게 고정 1,500,000(신 기준 공통 팔레트 — 원작 김용태 천신 8.5%·800k+1M 닻).",
            0.10f, 0f, ImmortalKit.Flat(1500000f, DamageType.AD));
        SkillData shred20 = ImmortalKit.AuraSkill(K, "방깍20", "방깍(20) — 오라",
            "사장님 10-06 「방깍(20)」. 반경 850 안 모든 적 방어 −20(원작 정윤식 A0ES −20 닻).",
            850f, ImmortalKit.ArmorAura(20f, "YONGTAE_ARMOR"));
        SkillData shred45 = ImmortalKit.OnHit(K, "방깍단일45", "방깍(단일 45)",
            "사장님 10-06 「방깍(단일45)」. 평타 1/6 확률로 맞은 적 한 기의 방어 −45(PM 확정 해석: 암브 방식 단일 방깍).",
            1f / 6f, 0f, ImmortalKit.ArmorBreak(45f));
        SkillData whole = ImmortalKit.OnHit(K, "전체체력데미지", "전체체력데미지 — 평타 1/10 최대체력 5%",
            "사장님 10-06 「전체체력데미지」(전체체력 = 대상 최대 체력). 평타 1/10 확률로 맞은 적 한 기에게 최대 체력의 5%를 방어 무시로 입힌다(신 기준 R49 일반 약 390만).",
            0.10f, 0f, ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.05f, DamageType.AD, SkillTargetKind.SingleTarget));
        SkillData bossJob = ImmortalKit.OnHit(K, "보스잡", "보스잡 — 평타 1/10 보스 최대체력 2%",
            "사장님 10-06 「보스잡(전체체력데미지)」. 평타 1/10 확률로 맞은 적이 보스(PV≥200)면 최대 체력의 2%를 방어 무시로 입힌다.",
            0.10f, 0f, ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.02f, DamageType.AD, SkillTargetKind.SingleTarget, SkillEffectTargetCondition.TargetPointValueAtLeast, 200f));
        SkillData lifeSkill = ImmortalKit.Skill(K, "체력스킬", "체력스킬 — 공격력 +30% · 공격속도 +30%(10초)",
            "사장님 10-06 「체력스킬(공격력증가, 공격속도증가)」 + 「주위유닛사망시체력회복」(확정: 주위 적 사망 시 체력 게이지 +5). 체력 게이지(평타 +1, 반경 850 안 적이 죽을 때마다 +5)가 100에 차면 자기 공격력 +30%·공격속도 +30%를 10초(제안값) → 게이지 0.",
            SkillTriggerType.OnHitCount, 0f, 1f, 100, SkillGaugeKind.Life,
            new SkillEffect { kind = SkillEffectKind.AttackPowerBuffPercent, target = SkillTargetKind.Self, multiplier = 0.30f, duration = 10f, buffId = "YONGTAE_AD" },
            new SkillEffect { kind = SkillEffectKind.AttackSpeedBuffPercent, target = SkillTargetKind.Self, multiplier = 0.30f, duration = 10f, buffId = "YONGTAE_AS" });
        SkillData fly = ImmortalKit.Label(K, "전지역이동", "전지역이동 — 비행",
            "사장님 10-06 「전지역이동」. 이 유닛은 바다 위도 이동한다(UnitData.movementAbility Flying — 구현담당2 FlyingMover). 스킬 자체엔 효과가 없다(이름·설명만, 값은 로스터 필드).");

        ImmortalKit.SetUnit(unit, "피지컬돼지", new List<SkillData> { splash, burst, shred20, shred45, whole, bossJob, lifeSkill, fly }, 0f, 100f);
        unit.attackSplashRadius = 300f;
        unit.movementAbility = MovementAbility.Flying;
        unit.lifeGaugeOnEnemyDeath = 5f; unit.lifeGaugeOnEnemyDeathRange = 850f;
        ImmortalKit.ReplaceIngredient(recipe, "제한_박성호", park);
        return Finish(K, unit, recipe, "유명인사김용태", old);
    }
}
