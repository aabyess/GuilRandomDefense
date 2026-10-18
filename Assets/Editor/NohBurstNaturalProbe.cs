using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 노태현 돌발행동 「자연 발동」 실측(구현담당1, 10-18) — CastSkillLevel 직접 호출이 아니라 평타로 체력 게이지 100을 채워 터지는 경로.
// gameshot: call:NohBurstNaturalProbe.Setup  → wait:N → call:NohBurstNaturalProbe.Report
static class NohBurstNaturalProbe
{
    static UnitAttacker unit;
    static readonly List<EnemyDummy> enemies = new List<EnemyDummy>();
    static readonly List<float> dist = new List<float>();
    static float[] prevHp;
    static int prevGauge;
    static float t0, lastAttackT;
    static int attacksSinceBurst, frames;
    static readonly StringBuilder log = new StringBuilder();
    static readonly FieldInfo gaugeF = typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance);
    static int bursts;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        enemies.Clear(); dist.Clear(); log.Clear(); bursts = 0; attacksSinceBurst = 0; frames = 0;
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset");
        var go = sp.Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        unit = go.GetComponent<UnitAttacker>();
        EnemyData d = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        var hpF = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        float r = 700f / WorldScale.Value;
        int i = 0;
        foreach (float f in new[] { 0.15f, 0.3f, 0.45f, 0.6f, 0.75f, 0.9f, 1.25f, 1.6f })
        {
            Vector3 p = go.transform.position + new Vector3(Mathf.Cos(i * 0.8f), 0f, Mathf.Sin(i * 0.8f)) * r * f;
            var e = Object.Instantiate(d.prefab, p, Quaternion.identity);
            if (e.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var ed = e.GetComponent<EnemyDummy>(); ed.Initialize(d, 1f); ed.SetLane(0);
            hpF.SetValue(ed, 1e9f);
            enemies.Add(ed); dist.Add(Vector3.Distance(e.transform.position, go.transform.position) * WorldScale.Value);
            i++;
        }
        prevHp = enemies.Select(e => e.Hp).ToArray();
        prevGauge = (int)gaugeF.GetValue(unit);
        t0 = Time.time; lastAttackT = t0;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        return $"세움: 노태현 {go.name} · 반경(월드) {r:F1} · 적 {enemies.Count}기 거리(원작단위) {string.Join(",", dist.Select(x => x.ToString("F0")))}";
    }

    static void Tick()
    {
        if (!Application.isPlaying || unit == null) { EditorApplication.update -= Tick; return; }
        frames++;
        int g = (int)gaugeF.GetValue(unit);
        float[] cur = enemies.Select(e => e != null ? e.Hp : 0f).ToArray();
        float[] dl = new float[cur.Length];
        for (int k = 0; k < cur.Length; k++) dl[k] = prevHp[k] - cur[k];
        bool burst = dl.Count(x => x > 1e7f) >= 1 || (prevGauge - g) >= 90;
        if (burst)
        {
            bursts++;
            int hit = dl.Count(x => x > 1e7f);
            log.AppendLine($"[{Time.time - t0:F1}s] 돌발행동 #{bursts}: 게이지 {prevGauge}→{g}, 직전 버스트 뒤 평타 {attacksSinceBurst}번, 맞은 적 {hit}/{enemies.Count}");
            for (int k = 0; k < cur.Length; k++) log.AppendLine($"     거리 {dist[k]:F0}: Δ{dl[k]:F0} ({dl[k] / prevHp[k] * 100f:F1}%)");
            attacksSinceBurst = 0;
        }
        else if (dl.Any(x => x > 1f)) attacksSinceBurst++;
        prevHp = cur; prevGauge = g;
    }

    static string Report()
    {
        int g = unit != null ? (int)gaugeF.GetValue(unit) : -1;
        var sb = new StringBuilder($"경과 {Time.time - t0:F1}s · 프레임 {frames} · 현재 게이지 {g} · 버스트 {bursts}회 · 평타 속도배율 {(unit != null ? unit.CurrentAttackSpeedMultiplier : 0):F2}\n");
        sb.Append(log);
        return sb.ToString();
    }
}
