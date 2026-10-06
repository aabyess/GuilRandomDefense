using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 이태훈 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:TaehunProbe.Setup    초월 이태훈을 우리에 세운다
///   call:TaehunProbe.Targets  일반 몹 6마리(잃은 체력 0·10·20·30·40·50% — 3번째 이후가 더 많이 잃음)와 보스 1마리를 0번 레인 가운데에 세우고 이태훈을 곁에 Warp(몹 체력 ×1e3)
///   call:TaehunProbe.Mana139  마나 게이지 139 → 다음 평타에 약자멸시 기대
///   call:TaehunProbe.Report   스킬 · 마나 게이지 · 몹별 생존/체력(잃은 체력 큰 순 삭제되는지) · 보스 생존 · 방어 실효 · 스턴 수 · 가장 느린 이속
/// </summary>
public static class TaehunProbe
{
    static readonly List<EnemyDummy> mobs = new List<EnemyDummy>();
    static EnemyDummy boss;
    static UnitAttacker Th() => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == "초월_이태훈_AP");

    public static string Setup()
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_이태훈_AP.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        return $"   소환 초월_이태훈_AP → {(go != null ? go.name : "null")}";
    }

    static EnemyDummy Make(string asset, Vector3 at, float lostFraction)
    {
        EnemyData data = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{asset}.asset");
        GameObject go = Object.Instantiate(data.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        EnemyDummy d = go.GetComponent<EnemyDummy>();
        d.Initialize(data, 1e3f); d.SetLane(-1);
        typeof(EnemyDummy).GetField("hp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, d.MaxHp * (1f - lostFraction));
        return d;
    }

    public static string Targets()
    {
        LaneMarker lane = LaneMarker.Get(0);
        if (lane == null) return "❌ 레인 없음";
        Vector3 c = lane.LaneCenter;
        mobs.Clear();
        for (int i = 0; i < 6; i++) mobs.Add(Make("Enemy_R45_이현빈", c + Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 8f, i * 0.10f));
        boss = Make("Enemy_R60_정윤식", c + Vector3.forward * 20f, 0.6f);   // 보스는 잃은 체력이 가장 크지만(60%) 삭제 대상이 아니어야 한다
        UnitAttacker t = Th();
        if (t != null && t.TryGetComponent(out UnityEngine.AI.NavMeshAgent a)) a.Warp(c + Vector3.back * 30f);
        return $"   몹 6(잃은 체력 0~50%) + 보스 1(잃은 체력 60%) + 이태훈 곁에 세움";
    }

    public static string Mana139()
    {
        UnitAttacker t = Th();
        typeof(UnitAttacker).GetField("manaGaugeInitialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(t, true);
        typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(t, 139);
        return "   이태훈 마나 게이지 → 139";
    }

    public static string Report()
    {
        UnitAttacker t = Th();
        if (t == null) return "❌ 이태훈 없음";
        UnitData d = t.GetComponent<UnitIdentity>().Data;
        int mana = (int)typeof(UnitAttacker).GetField("manaGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(t);
        StringBuilder sb = new StringBuilder($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('(')[0].Trim()))} · 마나 게이지 {mana}/{d.manaMax} · trait {(d.trait == null ? "없음" : d.trait.name)}\n");
        string alive = string.Join(" ", mobs.Select((m, i) => m == null || m.IsDead ? $"[{i}:삭제]" : $"[{i}:{(1f - m.Hp / m.MaxHp) * 100f:F1}%]"));
        float baseArmor = mobs.Count > 0 && mobs[0] != null ? mobs[0].EffectiveArmor : 0f;
        sb.AppendLine($"   몹 6(잃은 체력): {alive} · 보스 {(boss == null || boss.IsDead ? "삭제됨" : $"생존({(1f - boss.Hp / boss.MaxHp) * 100f:F1}%)")}");
        var live = mobs.Where(m => m != null && !m.IsDead).ToList();
        if (live.Count > 0) sb.AppendLine($"   생존 몹: 방어 실효 {live.Min(m => m.EffectiveArmor):F1}~{live.Max(m => m.EffectiveArmor):F1} · 스턴 {live.Count(m => m.IsStunned)}마리 · 가장 느린 이속 배율 {live.Min(m => m.EffectiveSlowMultiplier):F2}");
        return sb.ToString();
    }
}
