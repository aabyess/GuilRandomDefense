// 난이도 6종(2026-09-11, PM 지시 — 사장님 「원작대로」 확정, Docs/reference/DIFFICULTY_SPEC_2026-09-11.md
// §0/§7/§8-1/§8-2/§8-3 + PM이 원문(war3map_new.j Trig_Select_effect_Actions, war3map.w3q)으로
// 직접 검증한 표). 그 문서의 §3·§4·§9는 이 표를 만들 때 쓰지 않았다 — PM이 별도로 검증한
// 값(라운드 구간별 R00A/R00T/R00V/R00U/R00W 가산표)을 대신 썼다(리서치담당이 §3·§4·§9는
// 여전히 정정 중).
//
// 새 enum이니 순서는 자유였지만(PM 지시), 이후로는 append-only — 중간에 끼우면 이 enum을
// 참조하는 씬 직렬화 값이 밀린다.
public enum DifficultyMode
{
    Easy,       // 쉬움   (원작 Mode_int=1)
    Normal,     // 보통   (2)
    Hard,       // 어려움 (3)
    Hell,       // 지옥   (4)
    God,        // 신     (5)
    Nightmare,  // 악몽   (6)
}

public static class DifficultyModeExtensions
{
    public static string KoreanName(this DifficultyMode mode)
    {
        switch (mode)
        {
            case DifficultyMode.Easy: return "쉬움";
            case DifficultyMode.Normal: return "보통";
            case DifficultyMode.Hard: return "어려움";
            case DifficultyMode.Hell: return "지옥";
            case DifficultyMode.God: return "신";
            case DifficultyMode.Nightmare: return "악몽";
            default: return mode.ToString();
        }
    }
}

// 모드 하나의 수치 전부. 전부 PM이 원문에서 직접 검증한 값 — 지어낸 항목은 없다.
public readonly struct DifficultyModeData
{
    public readonly int totalRounds;

    // 일반 몹 공통(R00A, 가산 %). 전 라운드에 항상 걸린다.
    public readonly int mobCommonPercent;

    // 라운드 구간별 추가 가산(%, R00T/V/U/W) — 어려움~악몽만 값이 있고 쉬움·보통은 전부 0.
    // R15-29 / R31-49(R39 제외) / R51-59 / R61-75 넷뿐이다 — 그 사이(R1-14, 그리고 보스
    // 라운드인 R30/40/50/60)는 0(=R00A만 적용).
    public readonly int band15to29Percent;
    public readonly int band31to49Percent;
    public readonly int band51to59Percent;
    public readonly int band61to75Percent;

    // 보스 전용(%) — R00A/구간가산을 전혀 안 받고 이 값 하나만 적용된다(보스 전용 업그레이드
    // R00B/N/K/L/M, PM 확인).
    public readonly int bossPercent;

    // Aegr(마법 피해 배율) — 표 기록용(사람이 읽는 참고값). 🔴 2026-09-11 정정(PM 리뷰):
    // 실제 적용은 이 값을 곱하는 게 아니라 아래 aegrLevelOffset(레벨 오프셋)으로 한다.
    // 원작은 난이도가 Aegr **기본 레벨**을 정하고(16/11/6) 스킬 스택은 그 위에 등차로
    // 더해지다 레벨31에서 꺾인다 — 배율을 통째로 곱하면 스택이 있을 때 원작과 어긋난다
    // (EnemyDummy.EffectiveMagicMultiplier 주석 참고).
    public readonly float magicMultiplier;

    // Aegr 레벨 오프셋 — 난이도 기본레벨(16/11/6)을 자산 역산 레벨(16, magicArmorMultiplier=1f
    // 기준) 대비 얼마나 내리는지. 쉬움·보통·어려움 0(레벨16 그대로) · 지옥·신 −5(레벨11) ·
    // 악몽 −10(레벨6). EnemyDummy.AegrBaseLevel이 이 오프셋을 더해 최종 기본 레벨을 낸다.
    public readonly int aegrLevelOffset;

    // 어려움에서만 사이드보스(62/66/71)가 제외된다(원작 조건 정확히 Mode!=어려움 하나).
    public readonly bool sideBossExcluded;

    // 악몽 전용 플래그(H) — 밴 시스템은 아직 없어 이 필드만 두고 아무것도 안 걸었다.
    public readonly bool isNightmare;

    // 41라운드에서 70 대신 적용할 레인당 유닛 카운트 한계. 0이면 "이 모드는 41라운드에
    // 아무것도 안 바뀐다"(쉬움·보통·어려움) — 지옥60·신55·악몽50만 값이 있다.
    public readonly int round41UnitCountLimit;

    public DifficultyModeData(int totalRounds, int mobCommonPercent,
        int band15to29Percent, int band31to49Percent, int band51to59Percent, int band61to75Percent,
        int bossPercent, float magicMultiplier, int aegrLevelOffset, bool sideBossExcluded, bool isNightmare,
        int round41UnitCountLimit)
    {
        this.totalRounds = totalRounds;
        this.mobCommonPercent = mobCommonPercent;
        this.band15to29Percent = band15to29Percent;
        this.band31to49Percent = band31to49Percent;
        this.band51to59Percent = band51to59Percent;
        this.band61to75Percent = band61to75Percent;
        this.bossPercent = bossPercent;
        this.magicMultiplier = magicMultiplier;
        this.aegrLevelOffset = aegrLevelOffset;
        this.sideBossExcluded = sideBossExcluded;
        this.isNightmare = isNightmare;
        this.round41UnitCountLimit = round41UnitCountLimit;
    }
}

public static class DifficultyTable
{
    // PM 원문 검증표(2026-09-11 배정 메시지) 그대로:
    //   총 라운드: 쉬움50·보통60·나머지75
    //   R00A(일반 몹 공통, %): 쉬움0·보통10·어려움50·지옥100·신140·악몽150
    //   구간 가산(%, 어려움~악몽만): R15-29=27/54/81/108, R31-49=192/480/576/576(R39 제외),
    //                                R51-59=200/500/700/800, R61-75=181/586/667/748
    //   보스 전용(%): 쉬움-15·보통0·어려움200·지옥450·신725·악몽725
    //   Aegr: 쉬움·보통·어려움 1.00, 지옥·신 0.95, 악몽 0.90
    //   사이드보스 제외: 어려움만
    //   41라운드 유닛수 한계: 지옥60·신55·악몽50(나머지는 안 바뀜=0)
    static readonly DifficultyModeData[] Table =
    {
        // Easy      aegrLevelOffset 0(레벨16)
        new DifficultyModeData(50, 0, 0, 0, 0, 0, -15, 1.00f, 0, false, false, 0),
        // Normal    aegrLevelOffset 0(레벨16)
        new DifficultyModeData(60, 10, 0, 0, 0, 0, 0, 1.00f, 0, false, false, 0),
        // Hard      aegrLevelOffset 0(레벨16)
        new DifficultyModeData(75, 50, 27, 192, 200, 181, 200, 1.00f, 0, true, false, 0),
        // Hell      aegrLevelOffset -5(레벨11)
        new DifficultyModeData(75, 100, 54, 480, 500, 586, 450, 0.95f, -5, false, false, 60),
        // God       aegrLevelOffset -5(레벨11)
        new DifficultyModeData(75, 140, 81, 576, 700, 667, 725, 0.95f, -5, false, false, 55),
        // Nightmare aegrLevelOffset -10(레벨6)
        new DifficultyModeData(75, 150, 108, 576, 800, 748, 725, 0.90f, -10, false, true, 50),
    };

    public static DifficultyModeData Get(DifficultyMode mode) => Table[(int)mode];

    // 일반 몹(보스 아님) 최종 HP 배율. 가산 후 1회 곱(엔진표준, PM §4 판정 그대로).
    //
    // ⚠️ R39는 전 모드 공통으로 R00A조차 안 받는다(PM 지시 원문: "R39 제외 — R39는 R00A도
    // 안 받는다") — 구간 가산뿐 아니라 공통 가산까지 통째로 빠지는 유일한 예외 라운드다.
    public static float MobHpMultiplier(DifficultyMode mode, int round)
    {
        if (round == 39) return 1f;

        DifficultyModeData data = Get(mode);
        int percent = data.mobCommonPercent + BandBonusPercent(data, round);
        return 1f + percent / 100f;
    }

    static int BandBonusPercent(DifficultyModeData data, int round)
    {
        if (round >= 15 && round <= 29) return data.band15to29Percent;
        if (round >= 31 && round <= 49) return data.band31to49Percent; // 39는 위에서 이미 걸러짐
        if (round >= 51 && round <= 59) return data.band51to59Percent;
        if (round >= 61 && round <= 75) return data.band61to75Percent;
        return 0; // R1-14, 그리고 보스 라운드(R30/40/50/60)의 "혹시 있을" 일반 몹
    }

    // 보스 최종 HP 배율. R00A/구간가산 전부 안 타고 이 값 하나만 적용된다.
    public static float BossHpMultiplier(DifficultyMode mode) => 1f + Get(mode).bossPercent / 100f;
}
