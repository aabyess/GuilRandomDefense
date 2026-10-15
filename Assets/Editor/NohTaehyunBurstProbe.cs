using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 초월 노태현 돌발행동(범위 700 · 각 적 현재 체력 15%) 다수 적 동시 적중 확인(구현담당1, 10-16)
// gameshot: call:NohTaehyunBurstProbe.Run
static class NohTaehyunBurstProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        var u = sp.Spawn(AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_노태현_AP.asset"), c, 0);
        var att = u.GetComponent<UnitAttacker>();
        EnemyData d = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        var hpF = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        float r = 700f / WorldScale.Value;
        var list = new System.Collections.Generic.List<(EnemyDummy e, float dist)>();
        foreach (float f in new[] { 0.1f, 0.4f, 0.7f, 0.95f, 1.15f, 1.6f })
            for (int k = 0; k < 2; k++)
            {
                Vector3 p = u.transform.position + new Vector3(Mathf.Cos(k * 3.1f) , 0f, Mathf.Sin(k * 3.1f)).normalized * r * f;
                var go = Object.Instantiate(d.prefab, p, Quaternion.identity);
                if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
                var e = go.GetComponent<EnemyDummy>(); e.Initialize(d, 1f); e.SetLane(0);
                hpF.SetValue(e, 1e8f);
                list.Add((e, Vector3.Distance(e.transform.position, u.transform.position) * WorldScale.Value));
            }
        var skill = u.GetComponent<UnitIdentity>().Data.skills.First(s => s != null && s.name.Contains("돌발행동"));
        var cast = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        float[] pre = list.Select(x => x.e.Hp).ToArray();
        cast.Invoke(att, new object[] { skill.levels[0], skill.levels[0].WorldRange, null, 0f });
        var sb = new StringBuilder($"사거리 700(월드 {skill.levels[0].WorldRange:F1})\n");
        for (int i = 0; i < list.Count; i++)
            sb.AppendLine($"거리 {list[i].dist:F0} → Δ{pre[i] - list[i].e.Hp:F0} ({(pre[i] - list[i].e.Hp) / pre[i] * 100f:F1}%)");
        return sb.ToString();
    }
}
