using System.IO;
using UnityEngine;

/// <summary>파칭코 흐름 점검(10-14): gameshot … call:PachinkoProbe.Start wait:10 — ClaudeBridge/g1n_pachinko.txt = win|revive|reach|miss|random. 결과는 Game 뷰 로그([파칭코] …)로 본다.</summary>
static class PachinkoProbe
{
    static string Start()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string mode = File.ReadAllText("ClaudeBridge/g1n_pachinko.txt").Trim();
        PachinkoController.Begin(PachinkoPlan.Roll(mode == "random" ? null : mode));
        return $"발동 {mode} · 정지 {GamePause.CutinHold}";
    }

    static string State() => $"활성 {PachinkoController.Active} · 컷인정지 {GamePause.CutinHold} · timeScale {Time.timeScale} · 위습 {Wisp.Active.Count}";
}
