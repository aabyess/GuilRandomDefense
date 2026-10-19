using System;
using UnityEngine;

/// <summary>
/// 한 판 동안의 플레이어별 통계(S2 2.323, 사장님 10-18 「S2 아이디어 넣자」) — 개인 퀘스트(S2Quests)·-점수판 명령·미션판이 같이 읽는다.
/// 호스트·싱글에서만 센다(도박·위습·조합이 전부 호스트에서 돈다). 판 시작(RoundManager.Start)에 ResetForNewRun.
/// 원작 변수: jw/jz/j1 도박 시도 · kg/kV 실패 · kB 선택위습 · j8 목재도박 · LY 흔함판매 · i3 유니크 획득 · Q7 최상위 조합 · mX MVP.
/// </summary>
public static class RunStats
{
    public const int MaxPlayers = 4;
    public enum GambleTier { Low = 0, Mid = 1, High = 2, OtherWorld = 3, Absalom = 4 }
    const int Tiers = 5;

    public class PlayerStats
    {
        public readonly int[] gambleTries = new int[Tiers];
        public readonly int[] gambleSuccess = new int[Tiers];
        public readonly int[] gambleFails = new int[Tiers];
        public int gambleCommon, gambleUncommon, gambleRare, gambleSpecial;   // 하급(흔함·안흔함)·고급(희귀·특별) 결과 수(-점수판)
        public int choiceWispUses;        // 흔함선택위습 사용(kB)
        public int choiceWispUsesBefore15; // 15라운드 시작 전까지 사용(오늘만산다)
        public int woodGambleTries, woodGambleSuccess;
        public int commonSells;            // 흔함 판매 누적(LY) — UnitUpgrades.CommonSellCount는 N마다 0으로 돌아가 따로 센다
        public int uniqueItemsAcquired;    // 유니크(희귀함 등급 아이템) 획득 누적(i3)
        public int topCombines;            // 최상위(초월·불멸·영원·제한) 조합 누적(Q7)
        public int storyMvps;              // 스토리 기여도 1등 횟수(mX)
        public int combinedGradeMask;      // 조합해 본 결과 등급(1 << (int)UnitGrade)
        public bool combinedItemRecipe;    // 아이템 재료가 든 조합식을 조합했다
        public int finalUnitCount = -1;    // 마지막 라운드 끝에 레인에 남은 몹 수(-1 = 아직)
    }

    static readonly PlayerStats[] stats = NewArray();
    static PlayerStats[] NewArray() { var a = new PlayerStats[MaxPlayers]; for (int i = 0; i < a.Length; i++) a[i] = new PlayerStats(); return a; }

    /// <summary>값이 바뀐 플레이어(퀘스트가 다시 판정한다).</summary>
    public static event Action<int> Changed;
    /// <summary>조합 완료(호스트) — (플레이어, 결과 유닛).</summary>
    public static event Action<int, UnitData> Combined;

    public static PlayerStats Of(int playerId) => playerId >= 0 && playerId < MaxPlayers ? stats[playerId] : null;

    public static void ResetForNewRun()
    {
        for (int i = 0; i < MaxPlayers; i++) stats[i] = new PlayerStats();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { ResetForNewRun(); Changed = null; Combined = null; }

    static void Touch(int playerId) => Changed?.Invoke(playerId);

    /// <summary>현재 라운드 번호(원작 X3) — 라운드가 「시작」될 때 바뀐다. 우리 CurrentRound는 라운드 끝에 오르므로 쉬는 동안은 하나 뺀다.</summary>
    public static int RoundStarted()
    {
        RoundManager rm = UnityEngine.Object.FindFirstObjectByType<RoundManager>();
        if (rm == null) return 0;
        return rm.IsWaitingForNextRound ? rm.CurrentRound - 1 : rm.CurrentRound;
    }

    /// <summary>원작 「N라운드 시작 전」(X3 ≤ N−1).</summary>
    public static bool BeforeRound(int n) => RoundStarted() <= n - 1;

    public static GambleTier? TierOf(GamblingOptionData option)
    {
        if (option == null) return null;
        string n = option.name;
        if (n.Contains("하급")) return GambleTier.Low;
        if (n.Contains("중급")) return GambleTier.Mid;
        if (n.Contains("고급도박")) return GambleTier.High;
        if (n.Contains("랜덤유닛 도박")) return GambleTier.OtherWorld;
        if (n.Contains("조도연")) return GambleTier.Absalom;
        return null;
    }

    public static void OnUnitGamble(int playerId, GamblingOptionData option, bool success, UnitData reward)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); GambleTier? t = TierOf(option);
        if (s == null || t == null) return;
        int i = (int)t.Value;
        s.gambleTries[i]++;
        if (success) s.gambleSuccess[i]++; else s.gambleFails[i]++;
        if (success && reward != null)
        {
            if (reward.grade == UnitGrade.Common) s.gambleCommon++;
            else if (reward.grade == UnitGrade.Uncommon) s.gambleUncommon++;
            else if (reward.grade == UnitGrade.Rare) s.gambleRare++;
            else if (reward.grade == UnitGrade.Special) s.gambleSpecial++;
        }
        Touch(playerId);
    }

    public static void OnChoiceWispUsed(int playerId)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); if (s == null) return;
        s.choiceWispUses++;
        if (BeforeRound(15)) s.choiceWispUsesBefore15++;
        Touch(playerId);
    }

    public static void OnWoodGamble(int playerId, bool success)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); if (s == null) return;
        s.woodGambleTries++; if (success) s.woodGambleSuccess++;
        Touch(playerId);
    }

    public static void OnCommonSold(int playerId)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); if (s == null) return;
        s.commonSells++;
        Touch(playerId);
    }

    public static void OnItemAcquired(int playerId, ItemData item)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); if (s == null || item == null) return;
        if (item.hasGrade && item.grade == UnitGrade.Rare) { s.uniqueItemsAcquired++; Touch(playerId); }
    }

    public static void OnStoryMvp(int playerId)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        PlayerStats s = Of(playerId); if (s == null) return;
        s.storyMvps++;
        Touch(playerId);
    }

    public static bool IsTopGrade(UnitGrade g) => g == UnitGrade.Transcendent || g == UnitGrade.Immortal || g == UnitGrade.Eternal || g == UnitGrade.Limited;

    /// <summary>조합 완료(조합표·히든·채팅 조합). recipe가 있으면 아이템 재료 여부도 본다.</summary>
    public static void OnCombined(int playerId, UnitData result, CombineRecipe recipe = null)
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        if (!GameAuthority.IsServer || result == null) return;
        PlayerStats s = Of(playerId); if (s == null) return;
        s.combinedGradeMask |= 1 << (int)result.grade;
        if (IsTopGrade(result.grade)) s.topCombines++;
        if (recipe != null && recipe.ingredients != null)
            foreach (RecipeIngredient ing in recipe.ingredients)
                if (ing != null && ing.kind == IngredientKind.SpecificItem) { s.combinedItemRecipe = true; break; }
        Combined?.Invoke(playerId, result);
        Touch(playerId);
    }

    public static void SnapshotFinalUnitCounts()
    {
        if (!S2Features.Enabled) return;   // 사장님 10-19: S2 따라간 기능은 꺼 둔다(S2Features)
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            PlayerStats s = Of(c.PlayerId); if (s == null) continue;
            s.finalUnitCount = PersistentSave.CountAliveInLane(c.PlayerId);
        }
    }
}
