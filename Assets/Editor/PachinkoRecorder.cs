using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 파칭코 오버레이 결정론 녹화(사장님 10-14 「소리 포함 실시간 녹화」): Time.captureFramerate=30으로 게임 시각을 프레임마다 1/30초로 고정하고
/// 매 프레임 스크린샷(~/GRD_pachinko_rec/&lt;이름&gt;/f_00000.png …)을 저장, 오버레이가 낸 소리(대사·영상 음악)의 시각을 events.json에 남긴다.
/// 그 뒤 Tools/pachinko/build_rec.py가 프레임 + 소리 클립을 ffmpeg로 합쳐 mp4를 만든다(게임 BGM·시스템 소리는 안 들어간다).
/// 호출(플레이 중, gameshot 해상도 1280x720): call PachinkoRecorder.Rec777 · Rec333 · RecMiss — 끝나면 스스로 멈춘다.
/// </summary>
static class PachinkoRecorder
{
    static bool recording;
    static int lastFrame = -1, index, idle;
    static float t0 = -1f;
    static string dir, recName;
    static readonly List<(string name, float time, float vol)> events = new List<(string, float, float)>();

    static string Rec777() => Begin("777", PachinkoPlan.Roll("jackpot"));
    static string Rec333()
    {
        PachinkoPlan p = PachinkoPlan.Roll("win");
        p.left = p.center = p.right = 3;
        return Begin("333", p);
    }
    static string RecMiss() => Begin("miss", PachinkoPlan.Roll("miss"));
    static string RecReach() => Begin("reachmiss", PachinkoPlan.Roll("reach"));

    static string Begin(string name, PachinkoPlan plan)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        if (recording) return "이미 녹화 중";
        recName = name;
        dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "GRD_pachinko_rec", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        events.Clear(); index = 0; idle = 0; t0 = -1f; lastFrame = -1;
        PachinkoOverlay.RecordMode = true;
        PachinkoOverlay.AudioEvent += OnAudio;
        Time.captureFramerate = 30;
        recording = true;
        PachinkoOverlay.FrameHook += Tick;
        PachinkoOverlay.Preview(plan);
        return $"녹화 시작 {name}: {plan}";
    }

    // 시각은 프레임 번호 기준(프레임 하나 = 1/30초) — 캡처가 한 프레임도 안 빠지게 오버레이 Update에서 부른다
    static void OnAudio(string clip, float time, float vol) => events.Add((clip, index / 30f, vol));

    static void Tick()
    {
        if (!Application.isPlaying) { Finish(); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"f_{index:00000}.png"));
        index++;
        if (!PachinkoOverlay.IsPlaying && index > 10) { idle++; if (idle > 20) Finish(); }
    }

    static void Finish()
    {
        PachinkoOverlay.FrameHook -= Tick;
        PachinkoOverlay.AudioEvent -= OnAudio;
        PachinkoOverlay.RecordMode = false;
        Time.captureFramerate = 0;
        recording = false;
        var sb = new StringBuilder();
        sb.Append("{\"fps\":30,\"frames\":").Append(index).Append(",\"events\":[");
        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":\"").Append(events[i].name).Append("\",\"time\":").Append((events[i].time).ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
              .Append(",\"vol\":").Append(events[i].vol.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append('}');
        }
        sb.Append("]}");
        File.WriteAllText(Path.Combine(dir, "events.json"), sb.ToString());
        Debug.Log($"[파칭코 녹화] {recName}: {index}프레임 → {dir}");
    }
}
