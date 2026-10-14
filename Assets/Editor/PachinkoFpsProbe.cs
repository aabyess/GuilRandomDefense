using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>파칭코 오버레이 실시간 FPS 측정(녹화 모드 아님): call PachinkoFpsProbe.StartTg / StartRzMiss / StartRzWin → 끝난 뒤 call PachinkoFpsProbe.Report.
/// 배경 영상 재생 구간과 그 밖을 따로 평균·최저(1% low)·프레임 간격 33ms 초과 수(끊김)로 낸다. 에디터 창 기준 수치다(빌드는 더 가볍다).</summary>
static class PachinkoFpsProbe
{
    static readonly List<float> inVideo = new List<float>(), outVideo = new List<float>();
    static bool hooked;
    static string label;
    static long lastVideoFrame; static double lastVideoTime; static int videoStalls;

    static void Tick()
    {
        if (!PachinkoOverlay.IsPlaying) return;
        float dt = Time.unscaledDeltaTime;
        if (PachinkoOverlay.BgVideoPlaying)
        {
            inVideo.Add(dt);
            long f = PachinkoOverlay.BgVideoFrame; double t = PachinkoOverlay.BgVideoTime;
            // 영상 시각이 앞으로 가는데 프레임 번호가 안 따라오면(0.1초 이상) 끊김으로 센다
            if (f >= 0 && f == lastVideoFrame && t - lastVideoTime > 0.1) { videoStalls++; lastVideoTime = t; }
            if (f != lastVideoFrame) { lastVideoFrame = f; lastVideoTime = t; }
        }
        else outVideo.Add(dt);
    }

    static string Begin(string name, PachinkoPlan plan)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        inVideo.Clear(); outVideo.Clear(); videoStalls = 0; lastVideoFrame = -1; lastVideoTime = 0; label = name;
        if (!hooked) { PachinkoOverlay.FrameHook += Tick; hooked = true; }
        PachinkoOverlay.Preview(plan);
        return $"FPS 측정 시작 {name}: {plan}";
    }

    static string StartTg() { PachinkoOverlay.Theme = 0; return Begin("도쿄구울(777)", PachinkoPlan.Roll("jackpot")); }
    static string StartRzMiss() { PachinkoOverlay.Theme = 1; return Begin("리제로 꽝", PachinkoPlan.Roll("miss")); }
    static string StartRzWin() { PachinkoOverlay.Theme = 1; return Begin("리제로 당첨", PachinkoPlan.Roll("win")); }

    static string Stat(string name, List<float> dts)
    {
        if (dts.Count == 0) return $"{name}: 표본 없음";
        float avg = 1f / dts.Average();
        var sorted = dts.OrderByDescending(x => x).ToList();
        float worst1 = sorted.Take(Mathf.Max(1, dts.Count / 100)).Average();
        int over33 = dts.Count(x => x > 0.0334f), over50 = dts.Count(x => x > 0.05f);
        return $"{name}: {dts.Count}프레임 · 평균 {avg:F0}fps · 최저(최장 간격) {1f / sorted[0]:F0}fps · 1% low {1f / worst1:F0}fps · 33ms 초과 {over33} · 50ms 초과 {over50}";
    }

    static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{label}]");
        sb.AppendLine(Stat("영상 재생 구간", inVideo));
        sb.AppendLine(Stat("그 밖", outVideo));
        sb.AppendLine($"영상 프레임 정체(0.1초↑) {videoStalls}회");
        return sb.ToString();
    }
}
