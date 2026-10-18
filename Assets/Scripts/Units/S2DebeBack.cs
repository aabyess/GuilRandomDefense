using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 2.323 「데비백파이트」(j:41545~41640 CCM·CCN, 풀 j:41540 CCL). 내 특별함 유닛 하나를 걸고 50:50.
///   지면: 그 유닛을 잃는다. 스킬 레벨 +1(3번째 패배에서는 위로 보상, 스킬 소멸).
///   이기면: 풀 8종에서 한 기를 얻고 스킬이 사라진다. 그래서 한 판에 최대 3번.
/// 원작에서 스킬(A12T)을 어떻게 얻는지는 j에 없어(능력 부여 줄 없음) 우리는 모든 플레이어가 처음부터 3번 쓸 수 있게 둔다.
/// 쓰는 법: 내 특별함 유닛 하나를 고르고 채팅 「-데비백」. 판정은 호스트(NetHudAction.DebeBack).
/// 풀: 원작 h05H·h08N·h05Q·h05O·h05P·h05R(특수함 6) + h05X(레일리) + h0BG — h0BG는 원작 이름을 못 찾아 우리 풀엔 7종.
/// 위로(원작 Bhd=아이템 발굴 스택)는 우리에 아이템 발굴이 없어 목재 1로 대신.
/// </summary>
public static class S2DebeBack
{
    public const int MaxTries = 3;
    const string RayleighAsset = "Unit_레일리_h05X";
    static readonly int[] triesDone = { 0, 0, 0, 0 };
    static readonly bool[] won = { false, false, false, false };

    public static string LastResult { get; private set; }
    public static void ResetMatch() { for (int i = 0; i < 4; i++) { triesDone[i] = 0; won[i] = false; } LastResult = null; }

    public static int TriesLeft(int playerId)
    {
        int i = Mathf.Clamp(playerId, 0, 3);
        return won[i] ? 0 : MaxTries - triesDone[i];
    }

    static List<UnitData> Pool()
    {
        var pool = new List<UnitData>();
        foreach (UnitData u in Resources.FindObjectsOfTypeAll<UnitData>())
            if (u != null && u.prefab != null && (u.grade == UnitGrade.Superior || u.name == RayleighAsset)) pool.Add(u);
        return pool;
    }

    /// <summary>호스트(싱글): 고른 유닛으로 한 번 건다. forcedRoll(1~100)은 탐침용. 돌려주는 값은 오류 문구, 정상이면 null.</summary>
    public static string Execute(Selectable target, int forcedRoll = 0)
    {
        if (target == null || !target.TryGetComponent(out UnitIdentity identity) || identity.Data == null || !target.TryGetComponent(out OwnedByPlayer owner))
            return "자신의 '특별' 유닛에게만 사용 가능합니다";
        int pid = owner.OwnerId;
        if (identity.Data.grade != UnitGrade.Special || identity.IsSummon || identity.IsCopy)
        {
            PlayerNotification.ShowFailure(pid, "<color=#FF0000>[오류]</color> 자신의 '특별' 유닛에게만 사용 가능합니다", 5f);
            return "특별 유닛 아님";
        }
        if (TriesLeft(pid) <= 0) { PlayerNotification.ShowFailure(pid, "<color=#FF0000>[오류]</color> 데비백파이트는 더 할 수 없습니다.", 5f); return "횟수 없음"; }

        List<UnitData> pool = Pool();
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (pool.Count == 0 || spawner == null) return "풀이 비어 있습니다";

        int idx = Mathf.Clamp(pid, 0, 3);
        Vector3 at = target.transform.position;
        bool lose = (forcedRoll > 0 ? forcedRoll : Random.Range(1, 101)) <= 50;
        triesDone[idx]++;
        if (lose)
        {
            S2Notify.Show(pid, "<color=#FF0000>[알림]</color> 데비백파이트에서 패배하여 동료를 빼앗깁니다.", 5f);
            identity.Consume();
            if (triesDone[idx] >= MaxTries)
            {
                PlayerContext c = PlayerContext.Get(pid);
                if (c != null && c.ResourceWallet != null) c.ResourceWallet.Add(ResourceType.Wood, 1);
                S2Notify.Show(pid, "<color=#04FFC4>[알림]</color> 위로 보상: 목재 1", 5f);
            }
            LastResult = "패배";
        }
        else
        {
            UnitData got = pool[Random.Range(0, pool.Count)];
            identity.Consume();
            spawner.Spawn(got, at, pid, combined: true);
            won[idx] = true;
            S2Notify.Show(pid, $"<color=#04FFC4>[알림]</color><color=#FF8200>'데비백파이트' 승리로 {got.unitName}을(를) 빼앗아 옵니다.</color>", 6f);
            LastResult = "승리:" + got.name;
        }
        return null;
    }
}
