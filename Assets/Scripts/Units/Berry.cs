using UnityEngine;

/// <summary>
/// 베리 — 원랜디 S2 2.323의 세이브 메타 화폐(Sx, 사장님 10-18 「S2 아이디어 넣자」). 세이브(PlayerSaveData.berry)에 영구 누적된다.
/// 쓰는 곳(치장 상점, S2 대시보드)은 아직 없다 — 지금은 쌓이고 보이기만 한다.
///
/// 주는 곳은 전부 이 창구로(호스트·싱글에서만 효과, 클라에서 불러도 무시):
///   Berry.Grant(playerId, n, "사유")  — 한 사람
///   Berry.GrantAll(n, "사유")          — 자리에 있는 전원(원작 「전원 1베리」)
/// 보는 곳: Berry.Get(playerId) — 호스트는 세이브 값, 멀티 클라는 NetPlayer.Berry(호스트가 복제).
/// 판이 끝날 때(FinishRun) 파일에 같이 쓰이고, 멀티 원격 슬롯은 그 친구 PC 파일로 돌아간다(NetSaves).
/// </summary>
public static class Berry
{
    /// <summary>값이 바뀌었을 때(호스트·싱글에서 지급 직후) — 인자 playerId. 클라는 NetPlayer 복제로 따라오므로 표시는 Get을 매 프레임 읽을 것.</summary>
    public static event System.Action<int> Changed;

    public const string Color = "#00D9FF";   // 원작 |cff00d9ff

    public static bool Grant(int playerId, int amount, string reason = null, bool announce = true)
    {
        if (amount <= 0 || !GameAuthority.IsServer) return false;
        PlayerContext context = PlayerContext.Get(playerId);
        if (context == null || !context.IsOccupied || context.PersistentSave == null) return false;
        context.PersistentSave.AddBerry(amount);
        // 원작 문구 결: 「|cff04ffc4[사유]|r … |cff00d9ffN 베리|r 획득!」(j:58494·58498·51760·8626)
        if (announce)
        {
            string head = string.IsNullOrEmpty(reason) ? "" : $"<color=#04FFC4>[{reason}]</color> ";
            PlayerNotification.Show(playerId, $"{head}<color={Color}>{amount} 베리</color> 획득!", 10f);
        }
        Debug.Log($"[베리] 플레이어 {playerId} +{amount} ({reason}) → {context.PersistentSave.Data.berry}");
        Changed?.Invoke(playerId);
        return true;
    }

    public static void GrantAll(int amount, string reason = null, bool announce = true)
    {
        foreach (PlayerContext context in PlayerContext.Occupied) Grant(context.PlayerId, amount, reason, announce);
    }

    /// <summary>지금 베리(세이브 누적 + 이번 판). 모르면 0.</summary>
    public static int Get(int playerId)
    {
        if (MatchConfig.Active && !GameAuthority.IsServer)
        {
            foreach (NetPlayer player in NetPlayer.All) if (player != null && player.Slot == playerId) return player.Berry;
            return 0;
        }
        PlayerContext context = PlayerContext.Get(playerId);
        return context != null && context.PersistentSave != null ? context.PersistentSave.Data.berry : 0;
    }

    /// <summary>이번 판에 얻은 베리(호스트·싱글만 정확, 클라는 0).</summary>
    public static int Session(int playerId)
    {
        PlayerContext context = PlayerContext.Get(playerId);
        return context != null && context.PersistentSave != null ? context.PersistentSave.SessionBerry : 0;
    }
}
