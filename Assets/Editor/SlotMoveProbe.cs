using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>대기실 자리 이동 실측(10-06 구현담당3) — gameshot call:SlotMoveProbe.Report. 내 NetPlayer 슬롯 · 좌석 집합 · LocalPlayerId · 게임 씬이면 내 레인 번호.</summary>
public static class SlotMoveProbe
{
    public static string Report()
    {
        StringBuilder sb = new StringBuilder();
        NetPlayer me = NetPlayer.Local;
        sb.AppendLine($"   내 NetPlayer: {(me != null ? $"슬롯 {me.Slot} · 호스트 {me.IsHost} · 이름 {me.DisplayName}" : "없음")} · LocalPlayerId {LocalPlayer.LocalPlayerId} · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(s => s))}}} · 전체 NetPlayer {NetPlayer.All.Count}");
        foreach (NetPlayer p in NetPlayer.All) sb.AppendLine($"      - 슬롯 {p.Slot} {p.DisplayName} 호스트 {p.IsHost}");
        PlayerContext ctx = PlayerContext.Local;
        if (ctx != null) sb.AppendLine($"   PlayerContext.Local: playerId {ctx.PlayerId} · 점유 {ctx.IsOccupied}");
        LaneMarker lane = LaneMarker.Get(LocalPlayer.LocalPlayerId);
        if (lane != null) sb.AppendLine($"   내 레인 중심 {lane.LaneCenter:F0}");
        return sb.ToString();
    }
}
