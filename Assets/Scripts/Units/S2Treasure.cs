using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 2.323 보물상자 + 「원피스」(j:55640~55700 CRW, 지도 5장 j:4750 LV, 안내문 j:10298).
/// 라인몹을 잡으면 낮은 확률로 보물상자가 나와 바로 열린다(원작은 상자를 팔면 열림 — 확률 숫자는 j에 없어 사장님 확정 10-18: 1%).
/// 굴림 1~100: ≤20 목재 1 · 21~40 랜덤위습 1 · ≥96이면 보물지도(플레이어마다 5장, 다 쓰면 금화) · 나머지 2000~5000 엔.
/// 지도 한 장마다 맵에 탐색할 보물 3개(TreasureHunt.AddChests). 마지막(5번째) 지도 = 「원피스」: 전체 공지 + 10베리 +
/// 내 라인존 폭발형 피해 +10%(원작 LL+10 = 폭발형 데미지 N% 증가, j:20699·아이템 문구). LK+10(라인존 마법방어력 −10%)은 우리에 라인별 마저항 장치가 없어 미구현.
/// </summary>
public static class S2Treasure
{
    // 사장님 확정 10-18: 1% (원작 j에 상자 드롭 확률이 없다).
    public const float ChestDropChance = 0.01f;
    public const int MapsPerPlayer = 5, ChestsPerMap = 3, OnePieceExplosiveLevels = 2;   // 폭발형 +10% = A11S 2레벨(레벨당 5%)

    static readonly int[] mapsLeft = { MapsPerPlayer, MapsPerPlayer, MapsPerPlayer, MapsPerPlayer };
    static readonly bool[] onePiece = new bool[4];
    public static string LastResult { get; private set; }

    public static void ResetMatch()
    {
        for (int i = 0; i < 4; i++) { mapsLeft[i] = MapsPerPlayer; onePiece[i] = false; }
        LastResult = null;
    }

    public static bool HasOnePiece(int playerId) => playerId >= 0 && playerId < 4 && onePiece[playerId];

    /// <summary>EnemyDummy.PercentDamageTakenMultiplier가 부른다 — 이 라인(= 주인 번호)의 원피스 보너스 폭증 레벨.</summary>
    public static int ExtraExplosiveLevels(int lane) => HasOnePiece(lane) ? OnePieceExplosiveLevels : 0;

    static WispData FindWisp(string assetName)
    {
        foreach (WispData w in Resources.FindObjectsOfTypeAll<WispData>())
            if (w != null && w.name == assetName) return w;
        return null;
    }

    /// <summary>RewardDistributor.GrantKillReward: 레인 주인의 라인몹 처치 하나(호스트).</summary>
    public static void OnLaneKill(int ownerId)
    {
        if (!S2Features.Enabled) { return; }
        if (Random.value >= ChestDropChance) return;
        Open(ownerId);
    }

    /// <summary>상자를 연다. roll(1~100)은 탐침용(0이면 무작위). 돌려주는 값은 결과 이름(테스트용).</summary>
    public static string Open(int ownerId, int forcedRoll = 0)
    {
        PlayerContext context = PlayerContext.GetOccupied(ownerId);
        if (context == null) return null;
        int idx = Mathf.Clamp(ownerId, 0, 3);
        int roll = forcedRoll > 0 ? forcedRoll : Random.Range(1, 101);
        string result;

        if (roll <= 20)
        {
            context.ResourceWallet?.Add(ResourceType.Wood, 1);
            S2Notify.Show(ownerId, "보물상자에서 목재를 발견했습니다.\n<color=#7AD823>1 목재</color> 획득!", 5f);
            result = "목재";
        }
        else if (roll <= 40)
        {
            WispData wisp = FindWisp("Wisp_랜덤유닛");
            if (wisp != null && RewardDistributor.Instance != null)
                RewardDistributor.Instance.GrantWisps(context, new List<WispReward> { new WispReward { wisp = wisp, count = 1 } });
            S2Notify.Show(ownerId, "보물상자에서 위습을 발견했습니다.\n<color=#FFD700>랜덤위습 1개</color> 획득!", 5f);
            result = "위습";
        }
        else if (roll >= 96 && mapsLeft[idx] > 0)
        {
            mapsLeft[idx]--;
            string name = PlayerDisplayName.Of(ownerId);
            if (mapsLeft[idx] == 0)
            {
                onePiece[idx] = true;
                Berry.Grant(ownerId, 10, "원피스", announce: false);   // 전체 공지는 아래에서(원작 j:55672)
                foreach (PlayerContext c in PlayerContext.Occupied)
                    S2Notify.Show(c.PlayerId, $"{name} 님이 <color=#FF69B4>원피스</color>를 찾았습니다!\n보상으로 <color=#00D9FF>10 베리</color>를 획득했습니다!", 10f);
                S2Notify.Show(ownerId, "<color=#FF8200>★</color> 내 라인존 폭발형 피해 +10% (<color=#FF69B4>원피스</color>)", 10f);
                result = "원피스";
            }
            else
            {
                TreasureHunt.Instance?.AddChests(ChestsPerMap);
                foreach (PlayerContext c in PlayerContext.Occupied)
                    S2Notify.Show(c.PlayerId, $"{name} 님이 <color=#00CED1>보물지도</color>를 찾으셨습니다! 남은 보물지도 : {mapsLeft[idx]}개\n추가적으로 탐색 가능한 '보물'이 {ChestsPerMap}개 추가됩니다.", 10f);
                result = "지도";
            }
        }
        else
        {
            int gold = Random.Range(2000, 5001);
            context.GoldWallet?.Add(gold);
            S2Notify.Show(ownerId, $"보물상자에서 금화를 발견했습니다.\n<color=#FFD700>{gold:N0} 엔</color> 획득!", 5f);
            result = "엔";
        }
        LastResult = result;
        return result;
    }
}
