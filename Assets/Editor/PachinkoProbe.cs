using UnityEngine;

/// <summary>파칭코 오버레이 촬영용(플레이 중 call): gameshot `call:PachinkoProbe.Jackpot wait:… snap:…`. 컨트롤러 없이 Preview로 한 판을 돈다(PUSH는 5초 뒤 자동).</summary>
static class PachinkoProbe
{
    static string Go(string mode)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        PachinkoPlan p = PachinkoPlan.Roll(mode);
        PachinkoOverlay.Preview(p);
        return $"{mode}: {p} · 보류 {p.PreludeSeconds():F1}초 후 PUSH · 결과 {p.ResultSeconds():F1}초";
    }
    static string Jackpot() => Go("jackpot");
    static string Win() => Go("win");
    static string Revive() => Go("revive");
    static string Reach() => Go("reach");
    static string Miss() => Go("miss");
    static string Random() => Go(null);
}
