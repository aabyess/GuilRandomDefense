using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 상위 등급 공격력 재이식 점검(10-07) — gameshot: call:ShopSlotProbe.Fund wait:8 call:AttackBossProbe.Setup wait:1 call:AttackBossProbe.Mark wait:10 call:AttackBossProbe.Report
// 3기(초월 강재규·최상호AP·황준석)를 각자 R50 보스 앞(체력 10억, 이동 끔)에 세워 10초 동안 넣은 피해·평타·공격력·공속을 잰다.
static class AttackBossProbe
{
    static readonly string[] Names = { "초월_강재규_AP", "초월_최상호_AP", "초월_황준석_ADAP" };
    static readonly List<(UnitAttacker unit, EnemyDummy boss)> pairs = new List<(UnitAttacker, EnemyDummy)>();
    static readonly Dictionary<UnitAttacker, (float hp, int hits)> mark = new Dictionary<UnitAttacker, (float, int)>();

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var boss = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" }).Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(e => e != null && e.isBoss && e.prefab != null && e.name.Contains("50"));
        if (boss == null) boss = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" }).Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(e => e != null && e.isBoss && e.prefab != null);
        var hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance);
        pairs.Clear();
        int i = 0;
        foreach (string n in Names)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
            Vector3 p = LaneMarker.Get(0).LaneCenter + new Vector3(-300f + i * 150f, 0f, 200f);
            var go = spawner.Spawn(d, p, 0);
            var u = go.GetComponent<UnitAttacker>();
            var ag = go.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) ag.Warp(p);
            GameObject bg = Object.Instantiate(boss.prefab, p + new Vector3(0f, 0f, 22f), Quaternion.identity);
            if (bg.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = bg.GetComponent<EnemyDummy>(); e.Initialize(boss, 1f); e.SetLane(0);
            hpField.SetValue(e, 1e9f);
            pairs.Add((u, e)); i++;
        }
        return $"세움 {pairs.Count}기 · 보스 {boss.name} 방어 {pairs[0].boss.EffectiveArmor:F1}";
    }
    static string Mark() { mark.Clear(); foreach (var p in pairs) mark[p.unit] = (p.boss.Hp, p.unit.BasicHitCount); return "표식"; }
    static string Report()
    {
        var sb = new StringBuilder();
        foreach (var p in pairs)
        {
            var (hp0, h0) = mark[p.unit];
            sb.AppendLine($"  {p.unit.GetComponent<UnitIdentity>().Data.name,-16} 공격력 {p.unit.AttackDamage,8:F0} · 공속 ×{p.unit.CurrentAttackSpeedMultiplier:F2} 간격 {p.unit.AttackInterval:F3}초 · 10초 평타 {p.unit.BasicHitCount - h0} · 보스 방어 {p.boss.EffectiveArmor:F1} · 10초 피해 {hp0 - p.boss.Hp:N0}");
        }
        return sb.ToString();
    }
}
