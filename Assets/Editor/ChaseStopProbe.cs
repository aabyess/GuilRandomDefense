using System.Text;
using UnityEditor;
using UnityEngine;

// 추격 정지 A/B(10-08, UnitCombat.StopAtRangeEveryFrame) — gameshot: call:ChaseStopProbe.Run(스위치 켬) wait:8 call:ChaseStopProbe.Report call:ChaseStopProbe.RunOff wait:8 call:ChaseStopProbe.Report
// 사거리 85·200·360 유닛 셋이 각자 가만히 선 적(레인 0, 간격 220)을 향해 걷는다 → 멈춘 뒤 적까지 거리 ÷ 사거리.
static class ChaseStopProbe
{
    static readonly System.Collections.Generic.List<(UnitIdentity u, EnemyDummy e)> pairs = new System.Collections.Generic.List<(UnitIdentity, EnemyDummy)>();
    static string Run() => Start(true);
    static string RunOff() => Start(false);

    static string Start(bool on)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitCombat.StopAtRangeEveryFrame = on;
        foreach (var p in pairs) { if (p.u != null) Object.Destroy(p.u.gameObject); if (p.e != null) Object.Destroy(p.e.gameObject); }
        pairs.Clear();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var sampleEnemy = EnemyDummy.Active.Find(e => e != null && !e.IsBoss);
        EnemyData ed = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R03_반항아_이승우.asset");
        string[] names = { "특별함_김태영", "희귀함_노태현", "초월_배성령_AD" };
        var sb = new StringBuilder($"스위치 {(on ? "켬(매 프레임)" : "끔(0.25초 주기)")}\n");
        for (int i = 0; i < names.Length; i++)
        {
            UnitData d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{names[i]}.asset");
            if (d == null) { sb.AppendLine($"  ❌ {names[i]}"); continue; }
            Vector3 home = lane.LaneCenter + new Vector3(-200f + i * 200f, 0f, -150f);
            GameObject go = spawner.Spawn(d, home, 0);
            var atk = go.GetComponent<UnitAttacker>();
            float aggro = (float)typeof(UnitCombat).GetField("aggroRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(go.GetComponent<UnitCombat>());
            float dist0 = atk.AttackRange * 2.2f;   // 레인 유닛은 탐색 범위 = 사거리(aggro 18 < 사거리)라 스스로는 안 걷는다 — A로 찍은 표적(forced 추격)만 사거리 밖에서 걷는다
            GameObject eg = Object.Instantiate(ed.prefab, home + new Vector3(0f, 0f, dist0), Quaternion.identity);
            if (eg.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = eg.GetComponent<EnemyDummy>(); e.Initialize(ed, 1000f); e.SetLane(0);
            go.GetComponent<UnitCombat>().AttackTarget(e);   // A 공격 명령 = 강제 표적 추격
            pairs.Add((go.GetComponent<UnitIdentity>(), e));
            sb.AppendLine($"  {d.DisplayName} 사거리 {atk.AttackRange:F1} · 탐색 {aggro:F0} · 처음 적까지 {Vector3.Distance(home, eg.transform.position):F0}");
        }
        return sb.ToString();
    }

    static string Report()
    {
        var sb = new StringBuilder("멈춘 뒤:\n");
        foreach (var p in pairs)
        {
            if (p.u == null || p.e == null) continue;
            var atk = p.u.GetComponent<UnitAttacker>();
            float dist = Vector3.Distance(p.u.transform.position, p.e.transform.position);
            sb.AppendLine($"  {p.u.Data.DisplayName} 사거리 {atk.AttackRange:F1} · 적까지 {dist:F1} = 사거리의 {dist / atk.AttackRange:P0} · 평타 {atk.BasicHitCount}회");
        }
        return sb.ToString();
    }
}
