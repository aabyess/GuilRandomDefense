using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 불멸 8종 중 새 코드가 필요 없는 넷(정윤식·이승우·신지우·박은석) — 사장님 10-06 사양 + 확정(설계표 Docs/research/IMMORTAL_DESIGN_2026-10-06.md).
/// 호출: call ImmortalApplyA.Yunsig / Seungu / Jiu / Eunseok / All (다시 불러도 안전). 원작 이식 스킬을 통째 교체 · trait 비움 · 칭호 · 입력말(recipe.chatPhrase 직접).
/// 범퍼 = 범위(사장님 확정): 맞은 적 중심 반경 300 안 적 전부 각자 비례 피해(구현담당3 값). %체력 피해는 전부 방어 무시.
/// </summary>
static class ImmortalApplyA
{
    const float BumperRadius = 300f;

    static string All() => Yunsig() + "\n" + Seungu() + "\n" + Jiu() + "\n" + Eunseok();

    static string Finish(string key, UnitData unit, CombineRecipe recipe, string phrase, int oldCount)
    {
        unit.trait = null;   // 원작 능력교체형 특성이 새 스킬을 덮지 않게 — 특성 에셋은 그대로
        EditorUtility.SetDirty(unit);
        recipe.chatPhrase = phrase;
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"{key} 적용: 스킬 {oldCount}개 제거 → {unit.skills.Count}개 · 칭호 「{unit.unitName}」 · 마나 {unit.manaMax} · 체력게이지 {unit.lifeGaugeMax} · 입력말 {recipe.chatPhrase} · 재료 {ImmortalKit.Names(recipe)}";
    }

    // ───────── 정윤식 윤식파대장 (마딜) ─────────
    static string Yunsig()
    {
        const string K = "정윤식";
        var unit = ImmortalKit.Unit("불멸_정윤식"); var recipe = ImmortalKit.Recipe("불멸_정윤식");
        if (unit == null || recipe == null) return "❌ 정윤식 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillData dom = ImmortalKit.OnHit(K, "지배자의능력", "지배자의능력 — 깡딜(평타 1/10 150만) + 스플래시",
            "사장님 10-06 「스플래시, 깡딜」. 평타 1/10 확률로 맞은 적에게 고정 1,500,000(신 기준 공통 팔레트 — 원작 정윤식 5절대쿨 1/50·1.75M 닻) + 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(스플래시, UnitData.attackSplashRadius).",
            0.10f, 0f, ImmortalKit.Flat(1500000f, DamageType.AP));
        SkillData yield1 = ImmortalKit.OnHit(K, "굴복_마방깍", "굴복 — 마방깍(9)",
            "사장님 10-06 「마방깍(9)」. 평타 1/8 확률로 맞은 적의 마법 방어를 +9 약화(AegrStack, 박민석 선례).",
            0.125f, 0f, ImmortalKit.Aegr(9f));
        SkillData yield2 = ImmortalKit.OnHit(K, "굴복_스턴", "굴복 — 스턴(1)",
            "사장님 10-06 「스턴(1)」. 평타 1/6 확률로 범위 405 안 적 스턴 1.0초(구주호 선례).",
            1f / 6f, 405f, ImmortalKit.Stun(1f, SkillTargetKind.Enemies));
        SkillData hand = ImmortalKit.Label(K, "내손안에있다", "내손안에있다 — 범위증폭 10%",
            "사장님 10-06 「범위증폭10%」. 생매장(마나 스킬)의 범위 피해가 ×1.1이다 — 값은 생매장의 5%에 이미 곱해 두었다(5.5%). 이 스킬 자체엔 효과가 없다(이름·설명만).");
        SkillData alarm = ImmortalKit.AuraSkill(K, "경보발령", "경보발령 — 공격속도 오라(맵 전체 +15%)",
            "사장님 10-06 「공격속도오라(맵전체)」. 맵 전체 아군 공격속도 +15%(원작 센고쿠 A062 range 99999 +11% 선례, 사장님 확정 +15%). range 50000 = 월드 12000 — 맵 전체.",
            50000f,
            ImmortalKit.AsAura(0.15f, "YUNSIG_AS_AURA", SkillTargetKind.Allies), ImmortalKit.AsAura(0.15f, "YUNSIG_AS_AURA", SkillTargetKind.Self));
        SkillData bury = ImmortalKit.ManaSkill(K, "생매장", "생매장 — 마나 스킬(범위 750 잃은 체력 5.5%)",
            "사장님 10-06 「마나스킬(잃은체력5%, 범위증폭10%)」. 마나 게이지(평타 +1)가 115에 차면 범위 750 안 적의 잃은 체력(최대−현재)의 5% × 범위증폭 1.1 = 5.5%를 방어 무시로 입힌다(마나 스킬 = 잃은체력, 보스 상한 없음) → 게이지 0.",
            115, 750f, ImmortalKit.Pct(SkillEffectBasis.TargetMissingHpPercent, 0.055f, DamageType.AP, SkillTargetKind.Enemies));

        ImmortalKit.SetUnit(unit, "윤식파대장", new List<SkillData> { dom, yield1, yield2, hand, alarm, bury }, 115f);
        unit.attackSplashRadius = 300f;
        return Finish(K, unit, recipe, "구일의지배자정윤식", old);
    }

    // ───────── 이승우 재앙의탄생 (마딜) ─────────
    static string Seungu()
    {
        const string K = "이승우";
        var unit = ImmortalKit.Unit("불멸_이승우"); var recipe = ImmortalKit.Recipe("불멸_이승우");
        if (unit == null || recipe == null) return "❌ 이승우 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillData fall = ImmortalKit.Label(K, "자유낙하", "자유낙하 — 스플래시",
            "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(UnitData.attackSplashRadius). 스킬 자체엔 효과가 없다(이름·설명만).");
        SkillData steel = ImmortalKit.Label(K, "강철과같은단단함", "강철과같은단단함 — 방무뎀(30%)",
            "사장님 10-06 「방무뎀(30%)」(PM 결정: 적 방어의 30% 무시). UnitData.attackArmorIgnoreRatio 0.3 — 평타·스킬 피해가 적 방어의 30%를 무시한다(배성령 무방비상태와 같은 틀). 스킬 자체엔 효과가 없다(이름·설명만, 값은 로스터 필드).");
        SkillData crush1 = ImmortalKit.OnHit(K, "압사_스턴", "압사 — 스턴(1.5)",
            "사장님 10-06 「스턴(1.5)」. 평타 1/6 확률로 범위 405 안 적 스턴 1.5초.",
            1f / 6f, 405f, ImmortalKit.Stun(1.5f, SkillTargetKind.Enemies));
        SkillData crush2 = ImmortalKit.OnHit(K, "압사_범퍼", "압사 — 범퍼(현재체력 2%)",
            $"사장님 10-06 「범퍼(현재체력2%)」 — 범퍼 = 범위 안 적 전체 체력 비례(사장님 확정). 평타 25% 확률로 맞은 적 중심 반경 {BumperRadius} 안 적 전부에게 각자 현재 체력 2%(방어 무시).",
            0.25f, BumperRadius, ImmortalKit.Pct(SkillEffectBasis.TargetCurrentHpPercent, 0.02f, DamageType.AP, SkillTargetKind.Enemies));
        SkillData cry = ImmortalKit.ManaSkill(K, "파괴의외침", "파괴의외침 — 마나 스킬(범위 500 최대체력 3%)",
            "사장님 10-06 「마나스킬(최대체력3%)」(적힌 대로 최대체력 — 사장님 확정). 마나 게이지(평타 +1)가 160에 차면 범위 500 안 적 최대 체력의 3%를 방어 무시로 입힌다 → 게이지 0.",
            160, 500f, ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.03f, DamageType.AP, SkillTargetKind.Enemies));
        SkillData deAs = ImmortalKit.AuraSkill(K, "디버프_공속", "디버프 — 공격속도 −10%",
            "사장님 10-06 「디버프: 공속10%」. 자기 공격속도 −10%(공용 디버프 「문」 −15%와 별개 — 사장님 확정, 새로 만든 것).",
            0f, ImmortalKit.AsAura(-0.10f, "DEBUFF_SEUNGU_AS", SkillTargetKind.Self));
        SkillData deAd = ImmortalKit.AuraSkill(K, "디버프_공격력", "디버프 — 공격력 −10%",
            "사장님 10-06 「디버프: 공격력감소10%」. 자기 공격력 −10%(공용 디버프 「01」 −15%와 별개 — 사장님 확정, 새로 만든 것).",
            0f, ImmortalKit.AdAura(-0.10f, "DEBUFF_SEUNGU_AD", SkillTargetKind.Self));

        ImmortalKit.SetUnit(unit, "재앙의탄생", new List<SkillData> { fall, steel, crush1, crush2, cry, deAs, deAd }, 160f);
        unit.attackSplashRadius = 300f;
        unit.attackArmorIgnoreRatio = 0.3f;
        return Finish(K, unit, recipe, "태초의악마이승우", old);
    }

    // ───────── 신지우 구일최강의여성 (마딜) ─────────
    static string Jiu()
    {
        const string K = "신지우";
        var unit = ImmortalKit.Unit("불멸_신지우"); var recipe = ImmortalKit.Recipe("불멸_신지우");
        if (unit == null || recipe == null) return "❌ 신지우 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillData body = ImmortalKit.Label(K, "경이로운육체", "경이로운육체 — 스플래시",
            "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(UnitData.attackSplashRadius). 스킬 자체엔 효과가 없다(이름·설명만).");
        SkillData gap = ImmortalKit.OnHit(K, "현실과의괴리", "현실과의괴리 — 스턴(0.8)",
            "사장님 10-06 「스턴(0.8)」. 평타 1/6 확률로 범위 405 안 적 스턴 0.8초.",
            1f / 6f, 405f, ImmortalKit.Stun(0.8f, SkillTargetKind.Enemies));
        SkillData fear = ImmortalKit.AuraSkill(K, "원초적공포", "원초적공포 — 이감 오라(−50%)",
            "사장님 10-06 「이감(50%)」(확정: 오라). 반경 850 안 적 이동속도 −50%(남는 속도 0.5, 원작 김용태 A0AR 0.5 선례).",
            850f, ImmortalKit.SlowAura(0.5f, "JIU_SLOW"));
        SkillData weight = ImmortalKit.OnHit(K, "압도적인중량", "압도적인중량 — 범퍼(최대체력 0.5%)",
            $"사장님 10-06 「범퍼(최대체력0.5%)」 — 범퍼 = 범위 안 적 전체 체력 비례(사장님 확정). 평타 25% 확률로 맞은 적 중심 반경 {BumperRadius} 안 적 전부에게 각자 최대 체력 0.5%(방어 무시).",
            0.25f, BumperRadius, ImmortalKit.Pct(SkillEffectBasis.TargetMaxHpPercent, 0.005f, DamageType.AP, SkillTargetKind.Enemies));
        SkillData eat = ImmortalKit.OnHit(K, "먹성", "먹성 — 유닛삭제(평타 1/16)",
            "사장님 10-06 「유닛삭제」. 평타 1/16 확률로 사거리 700 안 가장 가까운 일반 적(보스·PV≥200·B06B 제외) 1기를 즉사(김건 포식과 같은 틀).",
            1f / 16f, 700f, new SkillEffect { kind = SkillEffectKind.KillNormalEnemies, target = SkillTargetKind.Self });
        SkillData blink = ImmortalKit.Skill(K, "소환주문", "소환주문 — 순간이동(액티브: 땅 지점 클릭)",
            "사장님 10-06 「순간이동」. 명령 카드 액티브 칸을 누르고 땅을 좌클릭하면 그 지점으로 순간이동한다(쿨 12초·사거리 제한 없음은 배성령 암살스킬과 같은 제안값).",
            SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.TeleportToPoint, target = SkillTargetKind.Self, multiplier = 0f });
        blink.levels[0].cooldown = 12f;
        blink.levels[0].needsPointClick = true;
        EditorUtility.SetDirty(blink);

        ImmortalKit.SetUnit(unit, "구일최강의여성", new List<SkillData> { body, gap, fear, weight, eat, blink }, 0f, 0f);
        unit.attackSplashRadius = 300f;
        return Finish(K, unit, recipe, "공포를몰아오는신", old);
    }

    // ───────── 박은석 거인 (물딜) ─────────
    static string Eunseok()
    {
        const string K = "박은석";
        var unit = ImmortalKit.Unit("불멸_박은석"); var recipe = ImmortalKit.Recipe("불멸_박은석");
        if (unit == null || recipe == null) return "❌ 박은석 에셋 없음";
        int old = unit.skills != null ? unit.skills.Count : 0;

        SkillData giant = ImmortalKit.Label(K, "거인", "거인 — 스플래시",
            "사장님 10-06 「스플래시」. 평타가 맞은 적 주변 반경 300 같은 레인 적에게도 같은 피해(UnitData.attackSplashRadius). 스킬 자체엔 효과가 없다(이름·설명만).");
        SkillData slayer = ImmortalKit.OnHit(K, "난쟁이학살자", "난쟁이학살자 — 범퍼(잃은체력 5%)",
            $"사장님 10-06 「범퍼(잃은체력5%)」 — 범퍼 = 범위 안 적 전체 체력 비례(사장님 확정). 평타 25% 확률로 맞은 적 중심 반경 {BumperRadius} 안 적 전부에게 각자 잃은 체력(최대−현재) 5%(방어 무시). 공속 0.44라 평타 횟수가 적어 이 값.",
            0.25f, BumperRadius, ImmortalKit.Pct(SkillEffectBasis.TargetMissingHpPercent, 0.05f, DamageType.AD, SkillTargetKind.Enemies));
        SkillData smash = ImmortalKit.OnHit(K, "내려치기", "내려치기 — 방깍(단일 45)",
            "사장님 10-06 「방깍(단일45{암브3})」. 평타 1/6 확률로 맞은 적 한 기의 방어 −45(PM/사장님 확정 해석: 단일 −45 + 암브 −3 둘 다 — 암브 −3은 말뚝박기 옆 내려치기_암브).",
            1f / 6f, 0f, ImmortalKit.ArmorBreak(45f));
        SkillData smashAmb = ImmortalKit.OnHit(K, "내려치기_암브", "내려치기 — 암브(−3)",
            "사장님 10-06 「{암브3}」. 평타 1/8 확률로 맞은 적 한 기의 방어 −3(아머브레이크).",
            0.125f, 0f, ImmortalKit.ArmorBreak(3f));
        SkillData pile = ImmortalKit.AuraSkill(K, "말뚝박기", "말뚝박기 — 방깍 오라(−30)",
            "사장님 10-06 「방깍(범위30{암브1})」. 반경 850 안 모든 적 방어 −30(오라) — 암브 −1은 말뚝박기_암브.",
            850f, ImmortalKit.ArmorAura(30f, "EUNSEOK_ARMOR"));
        SkillData pileAmb = ImmortalKit.OnHit(K, "말뚝박기_암브", "말뚝박기 — 암브(범위 −1)",
            "사장님 10-06 「{암브1}」. 평타 1/8 확률로 맞은 적 주변 반경 300 안 적 전부 방어 −1(아머브레이크).",
            0.125f, 300f, ImmortalKit.ArmorBreak(1f, SkillTargetKind.Enemies));
        SkillData block1 = ImmortalKit.OnHit(K, "길막_단일스턴", "길막 — 단일스턴",
            "사장님 10-06 「단일스턴」. 평타 1/6 확률로 맞은 적 한 기 스턴 1.5초.",
            1f / 6f, 0f, ImmortalKit.Stun(1.5f, SkillTargetKind.SingleTarget));
        SkillData block2 = ImmortalKit.OnHit(K, "길막_스턴", "길막 — 스턴(1)",
            "사장님 10-06 「스턴(1)」. 평타 1/6 확률로 범위 405 안 적 스턴 1.0초.",
            1f / 6f, 405f, ImmortalKit.Stun(1f, SkillTargetKind.Enemies));
        SkillData share = ImmortalKit.AuraSkill(K, "무료나눔", "무료나눔 — 공격력 오라(+15%)",
            "사장님 10-06 「공증15%」. 반경 850 안 아군(자기 포함) 공격력 +15%.",
            850f, ImmortalKit.AdAura(0.15f, "EUNSEOK_AD_AURA", SkillTargetKind.Allies), ImmortalKit.AdAura(0.15f, "EUNSEOK_AD_AURA", SkillTargetKind.Self));
        SkillData sea = ImmortalKit.Label(K, "부동력", "부동력 — 바다이동(비행)",
            "사장님 10-06 「바다이동」. 이 유닛은 바다 위도 이동한다(UnitData.movementAbility Flying — 구현담당2 FlyingMover). 스킬 자체엔 효과가 없다(이름·설명만, 값은 로스터 필드).");
        SkillData blink = ImmortalKit.Skill(K, "구일의주인", "구일의주인 — 순간이동(액티브: 땅 지점 클릭)",
            "사장님 10-06 「순간이동」. 명령 카드 액티브 칸을 누르고 땅을 좌클릭하면 그 지점으로 순간이동한다(쿨 12초·사거리 제한 없음은 배성령 암살스킬과 같은 제안값).",
            SkillTriggerType.ActiveButton, 0f, 1f, 0, SkillGaugeKind.Mana,
            new SkillEffect { kind = SkillEffectKind.TeleportToPoint, target = SkillTargetKind.Self, multiplier = 0f });
        blink.levels[0].cooldown = 12f;
        blink.levels[0].needsPointClick = true;
        EditorUtility.SetDirty(blink);

        ImmortalKit.SetUnit(unit, "거인", new List<SkillData> { giant, slayer, smash, smashAmb, pile, pileAmb, block1, block2, share, sea, blink }, 0f);
        unit.attackSplashRadius = 300f;
        unit.movementAbility = MovementAbility.Flying;
        return Finish(K, unit, recipe, "구일전설박은석", old);
    }
}
