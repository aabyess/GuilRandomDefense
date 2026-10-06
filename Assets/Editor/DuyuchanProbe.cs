using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 두유찬 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:DuyuchanProbe.Setup    초월 두유찬을 우리에 세운다
///   call:DuyuchanProbe.Targets  표적 6마리(안 움직임, 체력 ×1e3 — 6e8)를 0번 레인 가운데에 세우고 두유찬을 곁에 Warp
///   call:DuyuchanProbe.Gauge49  체력 게이지 49 → 다음 평타에 혼신의일격 기대
///   call:DuyuchanProbe.Report   스킬 · 체력 게이지 · 표적 체력 감소 최대(%) · 스턴 수 · 가장 느린 이속 배율
/// </summary>
public static class DuyuchanProbe
{
    static readonly List<EnemyDummy> targets = new List<EnemyDummy>();
    static UnitAttacker Du() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_두유찬_AD");
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    public static string Setup()
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_두유찬_AD.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        return $"   소환 초월_두유찬_AD → {(go != null ? go.name : "null")}";
    }

    public static string Targets()
    {
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R45_이현빈.asset");
        if (lane == null || data == null || data.prefab == null) return "❌ 레인·적 없음";
        Vector3 c = lane.LaneCenter;
        targets.Clear();
        for (int i = 0; i < 6; i++)
        {
            GameObject go = Object.Instantiate(data.prefab, c + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 8f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            EnemyDummy d = go.GetComponent<EnemyDummy>();
            d.Initialize(data, 1e3f); d.SetLane(-1);
            targets.Add(d);
        }
        UnitAttacker u = Du();
        if (u != null && u.TryGetComponent(out UnityEngine.AI.NavMeshAgent a)) a.Warp(c + Vector3.back * 30f);
        return $"   표적 {targets.Count}마리(최대 체력 {targets[0].MaxHp:F0}) + 두유찬 곁에 세움";
    }

    public static string Gauge49()
    {
        UnitAttacker u = Du();
        typeof(UnitAttacker).GetField("lifeGaugeInitialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(u, true);
        typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(u, 49);
        return "   두유찬 체력 게이지 → 49";
    }

    public static string Report()
    {
        UnitAttacker u = Du();
        if (u == null) return "❌ 두유찬 없음";
        UnitData d = u.GetComponent<UnitIdentity>().Data;
        StringBuilder sb = new StringBuilder($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('(')[0].Trim()))} · 체력 게이지 {Get<int>(u, "lifeGaugeCounter")}/{d.lifeGaugeMax} · trait {(d.trait == null ? "없음" : d.trait.name)}\n");
        if (targets.Count > 0)
        {
            float maxLossPct = targets.Max(t => (t.MaxHp - t.Hp) / t.MaxHp * 100f);
            int hit = targets.Count(t => t.Hp < t.MaxHp - 0.5f), stunned = targets.Count(t => t.IsStunned);
            float slowest = targets.Min(t => t.EffectiveSlowMultiplier);
            sb.AppendLine($"   표적 6: 맞은 수 {hit} · 체력 감소 최대 {maxLossPct:F2}% · 스턴 {stunned}마리 · 가장 느린 이속 배율 {slowest:F2}");
        }
        return sb.ToString();
    }
}
