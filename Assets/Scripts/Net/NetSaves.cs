using UnityEngine;

/// <summary>
/// 멀티 세이브(3단계 A안, Docs/MULTIPLAYER_SAVE_PLAN.md). 세이브는 각자 PC가 주인이고 판 동안만 호스트에 빌려준다.
///   대기실: 클라가 자기 player_0.json을 읽어 제출(NetPlayer.RPC_SubmitSave) → 호스트 MatchConfig에 보관
///   판 시작: 호스트의 원격 슬롯 PersistentSave가 그 제출값으로 채워진다(개시 보너스·조합/채팅 해금 조건이 친구 누적치로)
///   판 끝:  원격 슬롯 FinishRun 결과를 파일 대신 그 친구에게(NetPlayer.RPC_SaveResult) → 친구가 자기 파일에 쓰고 알림
/// 재전송·확인 응답은 안 한다(PM 결정 — 살아 있으면 신뢰 RPC가 닿고, 끊겼으면 받을 곳이 없다).
/// </summary>
public static class NetSaves
{
    /// <summary>클라: 내 NetPlayer가 생기면 한 번 제출한다.</summary>
    public static void SubmitOwn(NetPlayer self)
    {
        PlayerSaveData data = PersistentSave.ReadOwnSave();
        self.RPC_SubmitSave(data.cumulativePlayPoint, data.cumulativeClearCount, data.bestRunPoint, data.playerLevel);
        Debug.Log($"[MP] 세이브 제출: 누적 {data.cumulativePlayPoint}점 · 클리어 {data.cumulativeClearCount}회 · 최고 {data.bestRunPoint}");
    }

    /// <summary>호스트: 받은 값은 0 이상으로만 자른다(조작 방지는 안 함 — 친구 베타, 싱글과 같은 수준).</summary>
    public static void Receive(NetPlayer sender, int point, int clear, int best, int level)
    {
        var data = new PlayerSaveData
        {
            cumulativePlayPoint = Mathf.Max(0, point),
            cumulativeClearCount = Mathf.Max(0, clear),
            bestRunPoint = Mathf.Max(0, best),
            playerLevel = Mathf.Max(0, level),
        };
        MatchConfig.SetSubmittedSave(sender.Slot, data);
        Debug.Log($"[MP] 세이브 받음: 슬롯 {sender.Slot} — 누적 {data.cumulativePlayPoint}점 · 클리어 {data.cumulativeClearCount}회");
    }

    /// <summary>호스트: 원격 슬롯의 판 결과를 그 친구에게.</summary>
    public static void SendResult(int slot, PlayerSaveData data)
    {
        foreach (NetPlayer player in NetPlayer.All)
        {
            if (player == null || player.Slot != slot) continue;
            player.RPC_SaveResult(data.cumulativePlayPoint, data.cumulativeClearCount, data.bestRunPoint, data.playerLevel);
            Debug.Log($"[MP] 세이브 결과 보냄: 슬롯 {slot} — 누적 {data.cumulativePlayPoint}점 · 클리어 {data.cumulativeClearCount}회");
            return;
        }
        Debug.LogWarning($"[MP] 세이브 결과를 보낼 슬롯 {slot}의 접속자가 없습니다(나간 뒤) — 이번 판 몫은 남지 않습니다.");
    }

    /// <summary>클라: 결과를 받아 내 player_0.json에 쓰고 눈에 보이게 알린다(PM 요구).</summary>
    public static void WriteResult(int point, int clear, int best, int level)
    {
        var data = new PlayerSaveData { cumulativePlayPoint = point, cumulativeClearCount = clear, bestRunPoint = best, playerLevel = level };
        try
        {
            PersistentSave.WriteOwnSave(data);
            Debug.Log($"[MP] 세이브 저장: {PersistentSave.PathFor(0)} — 누적 {point}점 · 클리어 {clear}회 · 최고 {best}");
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "이번 판 기록을 저장했습니다.", 6f);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MP] 세이브 저장 실패: {e.Message}");
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "이번 판 기록을 저장하지 못했습니다.", 6f);
        }
    }
}
