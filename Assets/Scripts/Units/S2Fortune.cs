using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 2.323 「운세 점치기」(바질 호킨스 스킬, j:29312~29340 B2X). 채팅 「-운세」로 한 번 점친다 — 판정은 호스트.
/// 굴림은 원작대로 단계마다 따로(elseif 사슬이라 실제 확률은 표기보다 낮다):
///   2% 대길(전설위습 e015, 전체 공지) → 3% 희귀함위습(e019) → 5% 특별함위습(e016) → 12% 안흔함위습(e017) → 25% 랜덤위습(e0IX) → 실패.
/// 끝에 1/3 확률로 「한번 더 점을 칠 수 있습니다」(쿨 초기화). 원작 쿨타임 값은 j에 없어(능력 A0PE 수치는 w3a 밖) 우리 가정 30초.
/// </summary>
public static class S2Fortune
{
    public const float CooldownSeconds = 30f;
    static readonly float[] nextAllowed = { 0f, 0f, 0f, 0f };

    static WispData FindWisp(string assetName)
    {
        foreach (WispData w in Resources.FindObjectsOfTypeAll<WispData>())
            if (w != null && w.name == assetName) return w;
        return null;
    }

    static void Grant(PlayerContext context, string wispAsset)
    {
        WispData wisp = FindWisp(wispAsset);
        if (wisp != null && RewardDistributor.Instance != null)
            RewardDistributor.Instance.GrantWisps(context, new List<WispReward> { new WispReward { wisp = wisp, count = 1 } });
    }

    /// <summary>한 번 점친다. 돌려주는 값은 오류 문구(쿨 중 등), 정상이면 null. rng 인자는 탐침이 고정 값을 넣어 단계별로 확인하려고 둔 것(null이면 무작위).</summary>
    public static string Cast(int playerId, System.Func<int, int> roll100 = null, System.Func<int, int> roll3 = null)
    {
        PlayerContext context = PlayerContext.GetOccupied(playerId);
        if (context == null || context.IsDead) return "점칠 수 없는 상태입니다.";
        int idx = Mathf.Clamp(playerId, 0, nextAllowed.Length - 1);
        if (Time.time < nextAllowed[idx]) return $"운세는 {Mathf.CeilToInt(nextAllowed[idx] - Time.time)}초 뒤에 다시 점칠 수 있습니다.";
        nextAllowed[idx] = Time.time + CooldownSeconds;

        System.Func<int> d100 = () => roll100 != null ? roll100(100) : Random.Range(1, 101);
        string result;
        string name = PlayerDisplayName.Of(playerId);
        if (d100() <= 2)
        {
            Grant(context, "Wisp_전설히든");
            foreach (PlayerContext c in PlayerContext.Occupied)
                PlayerNotification.Show(c.PlayerId, $"<color=#FF69B4>대길!</color> {name}님이 <color=#FF0000>전설위습</color>을 획득!", 10f);
            result = "대길";
        }
        else if (d100() <= 3) { Grant(context, "Wisp_희귀함"); result = "<color=#FF69B4>길한 기운이 모여듭니다!</color> <color=#FF00FF>희귀함위습</color> 1개 획득!"; }
        else if (d100() <= 5) { Grant(context, "Wisp_특별함"); result = "<color=#FF69B4>행운이 당신을 바라봅니다!</color> <color=#0080FF>특별함위습</color> 1개 획득!"; }
        else if (d100() <= 12) { Grant(context, "Wisp_안흔함"); result = "<color=#FF69B4>운세는 순탄한 여정을 기약하는 듯합니다.</color> <color=#9553EC>안흔함위습</color> 1개 획득!"; }
        else if (d100() <= 25) { Grant(context, "Wisp_랜덤유닛"); result = "<color=#FF69B4>미약한 운세를 보입니다.</color> <color=#FFD700>랜덤위습</color> 1개 획득!"; }
        else result = "<color=#FF69B4>운세가 당신을 점지해주지 않고 있습니다.</color>";

        bool again = (roll3 != null ? roll3(3) : Random.Range(1, 4)) == 2;
        if (again)
        {
            nextAllowed[idx] = 0f;
            result += "\n<color=#C6FBFB>한번 더 점을 칠 수 있습니다.</color>";
        }
        if (result != "대길") PlayerNotification.Show(playerId, result, 5f);
        LastResult = result;
        return null;
    }

    public static string LastResult { get; private set; }
}
