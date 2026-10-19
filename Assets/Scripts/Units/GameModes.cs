using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 2.323 모드 토글(j:57895~57935 CUL·CUM, 58060~58075, 문구 j:1746~1747). 난이도 선택 뒤 방장이 고른다 — 지옥 이상에서만:
///   변질모드  : 상위테크 유닛의 30%가 조합이 불가능해진다(원작 CNn: 조합표 인형 14개를 뽑아 밴 — 「이 유닛은 밴입니다」).
///   이세계모드: 이세계 유닛과 이세계 도박(도박소 「랜덤유닛 도박」)이 켜진다.
/// 우리 적용: 변질 = 조합식 중 결과가 희귀함·히든·전설적인인 것의 30%를 판 시작에 무작위로 밴(CombineSystem.TryCombine이 거절). 원작 인형 14개 풀 대응이 우리 조합표에 없어 등급 풀로 근사.
/// 이세계 = 지옥 미만은 지금처럼 늘 켜짐, 지옥 이상은 방장이 끌 수 있다(원작은 기본 꺼짐 — OtherWorldDefaultOn 한 줄로 뒤집는다).
/// 판정은 호스트. 멀티 클라는 도박 잠김 표시가 호스트 값과 다를 수 있다(실제 실행은 호스트가 막는다).
/// </summary>
public static class GameModes
{
    public const float AlteredBanFraction = 0.30f;
    public const bool OtherWorldDefaultOn = true;   // 원작은 기본 꺼짐. 켜 둔 채 가야 지금 판들과 같다 — 사장님이 원작대로 원하면 false.

    public static bool Altered { get; private set; }
    public static bool OtherWorld { get; private set; } = OtherWorldDefaultOn;
    static readonly HashSet<string> banned = new HashSet<string>();
    public static IReadOnlyCollection<string> Banned => banned;

    public static void ResetMatch()
    {
        Altered = false; OtherWorld = OtherWorldDefaultOn; banned.Clear();
    }

    public static bool ModesAvailable(DifficultyMode mode) => mode == DifficultyMode.Hell || mode == DifficultyMode.God || mode == DifficultyMode.Nightmare;

    /// <summary>DifficultySelectHud: 방장이 난이도를 고르기 직전에 토글 값을 넣는다(지옥 미만이면 무시).</summary>
    public static void Choose(DifficultyMode mode, bool altered, bool otherWorld)
    {
        if (!S2Features.Enabled) { return; }
        if (!GameAuthority.IsServer) return;
        if (!ModesAvailable(mode)) return;
        Altered = altered;
        OtherWorld = otherWorld;
        if (altered)
        {
            ApplyBans();
            Announce("<color=#8B3EB4>변질모드</color>가 선택되었습니다.\n<color=#5F9EA0>상위테크 유닛의 30%가 조합이 불가능해집니다.</color>");
        }
        if (otherWorld != OtherWorldDefaultOn)
            Announce(otherWorld ? "<color=#51DDF5>이세계모드</color>가 선택되었습니다.\n<color=#5F9EA0>게임에 이세계 유닛이 추가되며 도박소에 이세계 도박이 활성화됩니다.</color>"
                                : "<color=#51DDF5>이세계모드</color>가 꺼졌습니다. 도박소의 이세계 도박이 비활성화됩니다.");
    }

    static void ApplyBans()
    {
        banned.Clear();
        CombineSystem combine = Object.FindFirstObjectByType<CombineSystem>();
        if (combine == null || combine.Recipes == null) return;
        var pool = new List<CombineRecipe>();
        foreach (CombineRecipe r in combine.Recipes)
            if (r != null && r.result != null && !string.IsNullOrEmpty(r.commandId) &&
                (r.result.grade == UnitGrade.Rare || r.result.grade == UnitGrade.Hidden || r.result.grade == UnitGrade.Legendary))
                pool.Add(r);
        int n = Mathf.RoundToInt(pool.Count * AlteredBanFraction);
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int k = Random.Range(0, pool.Count);
            banned.Add(pool[k].commandId);
            pool.RemoveAt(k);
        }
        Debug.Log($"[변질모드] 조합식 {banned.Count}개 밴");
    }

    public static bool IsBanned(CombineRecipe recipe) => S2Features.Enabled && Altered && recipe != null && !string.IsNullOrEmpty(recipe.commandId) && banned.Contains(recipe.commandId);

    /// <summary>도박소 옵션이 이세계 도박(랜덤유닛 도박)인가 — 모드가 꺼졌으면 잠긴다.</summary>
    public static bool BlocksGambling(GamblingOptionData option) =>
        S2Features.Enabled && !OtherWorld && option != null && option.category == GamblingCategory.Unit && option.primaryResultGrade == UnitGrade.RandomUnit;

    static void Announce(string message)
    {
        foreach (PlayerContext c in PlayerContext.Occupied) S2Notify.Show(c.PlayerId, message, 6f);
    }
}
