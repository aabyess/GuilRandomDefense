using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 컷인 중 게임 시간 정지(사장님 10-08) 실측 탐침(구현담당2) — gameshot:
//   gameshot x.png 2 1920x1080 click?:쉬움 wait:3 call:CutinPauseProbe.Start wait:19 call:CutinPauseProbe.Report
// 시나리오(실시간 초): 0 컷인 A 시작 · 1.0 P 켬 · 1.6 P 끔(컷인 중이라 정지 유지여야) · 4.0 컷인 B · 4.5 P 켬(컷인 끝나도 정지 유지) · 8.5 P 끔 · 10.5 컷인 둘 연속(큐).
// 표본: 0.2초마다 (실시간, 게임 시간 Time.time, timeScale, 이동 유닛 위치, AudioListener.pause, 컷인 정지·사용자 정지).
static class CutinPauseProbe
{
    struct Sample { public float real, game, scale, x; public bool audioPaused, cutin, user; }
    static readonly List<Sample> samples = new List<Sample>();
    static readonly SortedList<float, System.Action> script = new SortedList<float, System.Action>();
    static float t0, nextSample;
    static GameObject mover;
    static readonly StringBuilder notes = new StringBuilder();

    static void Acquire()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_최상호_AD.asset");
        spawner.Spawn(unit, LaneMarker.Get(0).LaneCenter, 0);
    }

    static string Start()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        CutinOverlay.DebugSpeed = 1f;
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var walker = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/흔함_강주혁.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        mover = spawner.Spawn(walker, c, 0);
        var agent = mover.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) { agent.Warp(c); agent.SetDestination(c + new Vector3(900f, 0f, 0f)); }
        samples.Clear(); script.Clear(); notes.Clear();
        t0 = Time.realtimeSinceStartup; nextSample = 0f;
        script[0.2f] = Acquire;
        script[1.0f] = () => { GamePause.Set(true); };
        script[1.6f] = () => { GamePause.Set(false); notes.Append($"\nP 끔(컷인 중) 직후 timeScale {Time.timeScale}(0 기대)"); };
        script[4.0f] = Acquire;
        script[4.5f] = () => { GamePause.Set(true); };
        script[8.5f] = () => { notes.Append($"\n컷인 B 끝난 뒤 P 켠 채 timeScale {Time.timeScale}(0 기대) · 컷인 정지 {GamePause.CutinHold}(false 기대)"); GamePause.Set(false); };
        script[10.5f] = () => { Acquire(); Acquire(); };
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return "시작";
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        float r = Time.realtimeSinceStartup - t0;
        while (script.Count > 0 && script.Keys[0] <= r) { var a = script.Values[0]; script.RemoveAt(0); a(); }
        if (r >= nextSample)
        {
            nextSample = r + 0.2f;
            samples.Add(new Sample { real = r, game = Time.time, scale = Time.timeScale, x = mover != null ? mover.transform.position.x : 0f, audioPaused = AudioListener.pause, cutin = GamePause.CutinHold, user = GamePause.Paused });
        }
    }

    static string Report()
    {
        EditorApplication.update -= Tick;
        var sb = new StringBuilder();
        sb.Append($"표본 {samples.Count}개{notes}");
        // 구간 요약: 연속한 (timeScale, cutin, user) 상태 묶음마다 실시간 길이 · 게임 시간 변화 · 이동 거리
        int i = 0;
        while (i < samples.Count)
        {
            int j = i;
            while (j + 1 < samples.Count && samples[j + 1].scale == samples[i].scale && samples[j + 1].cutin == samples[i].cutin && samples[j + 1].user == samples[i].user) j++;
            var a = samples[i]; var b = samples[j];
            sb.Append($"\n실시간 {a.real:F1}~{b.real:F1}s · 컷인정지 {a.cutin} 사용자정지 {a.user} → timeScale {a.scale} · 게임시간 {b.game - a.game:F2}s 흐름 · 이동 {b.x - a.x:F1} · 소리정지 {a.audioPaused}");
            i = j + 1;
        }
        bool anyCutinAudio = false; foreach (var s in samples) if (s.cutin && s.audioPaused) anyCutinAudio = true;
        sb.Append($"\n컷인 정지 중 AudioListener.pause가 켜진 적 {anyCutinAudio}(false 기대)");
        return sb.ToString();
    }
}
