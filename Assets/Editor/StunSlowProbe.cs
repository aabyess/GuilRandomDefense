using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 스턴·이감 반영(2026-09-30) 실측 — 유닛마다 떨어진 자리에 표적 셋을 세우고, 표적이 실제로
// 스턴(IsStunned)·이감(EffectiveSlowMultiplier<1)에 걸린 구간을 매 프레임 기록해 횟수·지속시간·배수를 낸다.
// gameshot:
//   gameshot x.png 1 1920x1080 click?:보통 wait:2 call:StunSlowProbe.Arena wait:60 call:StunSlowProbe.Report
static class StunSlowProbe
{
    // 에셋 기대값(원작): 박은석 A05P 강타 확률 100%·2.25초 / 정내연 A0QH 더미 폭풍망치 2초 /
    // 최영민 A0QX 이감 0.3·2.5초 / 이타도리 A0LE 이감 0.15(→하한 0.233)·A0LD 0.3·3초 / 김건 A07E 발구르기 2.75초
    static readonly string[] Units = { "불멸_박은석", "희귀함_정내연", "제한_최영민", "랜덤_이타도리_유지", "전설적인_김건" };

    class Track
    {
        public string unit;
        public float stunStart = -1f, slowStart = -1f, slowMin = 1f;
        public readonly List<float> stuns = new List<float>();
        public readonly List<(float dur, float mult)> slows = new List<(float, float)>();
    }

    static readonly Dictionary<EnemyDummy, Track> tracks = new Dictionary<EnemyDummy, Track>();

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.moveSpeed > 0f && e.name.Contains("R2"));
        if (spawner == null || lane == null || dummyData == null) return "❌ UnitSpawner·레인·표적 적 없음";

        tracks.Clear();
        Vector3 c = lane.LaneCenter;
        var sb = new StringBuilder();
        for (int u = 0; u < Units.Length; u++)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Units[u]}.asset");
            if (data == null) { sb.Append($"{Units[u]} 없음 · "); continue; }
            // 유닛끼리 서로의 표적을 안 치게 떨어뜨린다 — 이웃 간격 2·400·sin36° ≈ 470 > 최대 사거리 240(월드 단위, 정내연).
            Vector3 home = c + Quaternion.Euler(0f, u * 72f, 0f) * Vector3.forward * 400f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f;
                GameObject go = Object.Instantiate(dummyData.prefab, at, Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (go.TryGetComponent(out EnemyDummy dummy))
                {
                    dummy.Initialize(dummyData, 1e6f);
                    dummy.SetLane(-1);
                    tracks[dummy] = new Track { unit = Units[u] };
                }
            }
            if (spawner.Spawn(data, home, 0) != null) sb.Append($"{Units[u]} · ");
        }
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        Time.timeScale = 2f;
        return $"표적 {tracks.Count}({dummyData.name}, 이속 {dummyData.moveSpeed}) · 유닛 {sb}· 2배속";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        float now = Time.time;
        foreach (var kv in tracks)
        {
            EnemyDummy d = kv.Key; Track t = kv.Value;
            if (d == null) continue;
            bool stunned = d.IsStunned;
            if (stunned && t.stunStart < 0f) t.stunStart = now;
            else if (!stunned && t.stunStart >= 0f) { t.stuns.Add(now - t.stunStart); t.stunStart = -1f; }

            float m = d.EffectiveSlowMultiplier;
            if (m < 0.999f)
            {
                if (t.slowStart < 0f) { t.slowStart = now; t.slowMin = m; }
                else t.slowMin = Mathf.Min(t.slowMin, m);
            }
            else if (t.slowStart >= 0f) { t.slows.Add((now - t.slowStart, t.slowMin)); t.slowStart = -1f; }
        }
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        Time.timeScale = 1f;
        var sb = new StringBuilder();
        foreach (var g in tracks.Values.GroupBy(t => t.unit))
        {
            var stuns = g.SelectMany(t => t.stuns).ToList();
            var slows = g.SelectMany(t => t.slows).ToList();
            sb.Append($"\n{g.Key}: 스턴 {stuns.Count}회");
            if (stuns.Count > 0) sb.Append($" (최소 {stuns.Min():0.00}·중앙 {stuns.OrderBy(x => x).ElementAt(stuns.Count / 2):0.00}·최대 {stuns.Max():0.00}초)");
            sb.Append($" · 이감 {slows.Count}회");
            if (slows.Count > 0)
                sb.Append($" (지속 최소 {slows.Min(s => s.dur):0.00}·최대 {slows.Max(s => s.dur):0.00}초 · 배수 {string.Join("/", slows.Select(s => s.mult.ToString("0.000")).Distinct().Take(4))})");
            int open = g.Count(t => t.stunStart >= 0f || t.slowStart >= 0f);
            if (open > 0) sb.Append($" · 측정 끝에 걸려 있던 표적 {open}");
        }
        return sb.ToString();
    }
}
