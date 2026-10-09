using UnityEngine;

/// <summary>패배 창 촬영(10-09): 내 플레이어를 패배 처리한다(진짜 패배 경로 일부 — MarkDead만). gameshot x.png 1 1920x1080 click?:보통 wait:2 call:DefeatProbe.Lose wait:1 snap:a.png</summary>
static class DefeatProbe
{
    static string Lose()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        PlayerContext me = PlayerContext.Local;
        if (me == null) return "❌ 내 플레이어 없음";
        me.MarkDead("라인의 유닛이70마리가 되어 패배하셨습니다 !");
        return "패배 처리";
    }
}
