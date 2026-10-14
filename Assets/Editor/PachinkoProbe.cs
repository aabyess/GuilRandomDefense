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

    /// <summary>확률 분포 확인(배치): Unity -batchmode -quit -executeMethod PachinkoProbe.Distribution — Roll(null)을 1000번 뽑아 결과별 비율을 찍는다.</summary>
    public static void Distribution()
    {
        int n = 1000, j = 0, w = 0, r = 0, m = 0, v = 0, sevenWin = 0;
        for (int i = 0; i < n; i++)
        {
            PachinkoPlan p = PachinkoPlan.Roll(null);
            if (p.jackpot) j++; else if (p.win) { w++; if (p.left == 7) sevenWin++; } else if (p.reach) r++; else m++;
            if (p.revive) v++;
        }
        Debug.Log($"[파칭코 분포] {n}회 — 777 {j} ({j / 10f:F1}%) · 같은숫자 {w} ({w / 10f:F1}%, 그중 7 {sevenWin}) · 리치꽝 {r} ({r / 10f:F1}%) · 그냥꽝 {m} ({m / 10f:F1}%) · 부활 {v}");
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(sevenWin == 0 ? 0 : 1);
    }
}
