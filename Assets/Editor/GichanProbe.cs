using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 초월 박기찬 실측(10-06 구현담당3) — gameshot call:로 부른다. 에디터 전용.
///   call:GichanProbe.Setup    초월 박기찬 + 흔함 박민수(아군)를 우리에 세운다
///   call:GichanProbe.Targets  0번 레인 가운데에 표적 6마리(안 움직임, 체력 ×1e3)를 세우고 박기찬·박민수를 그 곁(30)에 Warp
///   call:GichanProbe.Report   표적: 방어 실효 vs 데이터 방어(차) · 맞은 수(체력 < 최대) · 스턴 수 · 가장 느린 이속 배율 / 아군 공속 배율 / 박기찬 체력 게이지·멀티샷 설정
/// </summary>
public static class GichanProbe
{
    static readonly List<EnemyDummy> targets = new List<EnemyDummy>();
    static UnitAttacker Find(string asset) => Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None).FirstOrDefault(a => a.TryGetComponent(out UnitIdentity id) && id.Data != null && id.Data.name == asset);
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    static void Spawn(string name, StringBuilder sb)
    {
        UnitData data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(data, LaneMarker.Get(0).TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {name} → {(go != null ? go.name : "null")}");
    }

    public static string Setup() { StringBuilder sb = new StringBuilder(); Spawn("초월_박기찬_AD", sb); Spawn("흔함_박민수", sb); return sb.ToString(); }

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
            d.Initialize(data, 1e3f); d.SetLane(-1);   // 1e9면 float 눈금에 작은 피해가 묻힌다(맞은 수 0) — 6e8이면 38,000도 보인다
            targets.Add(d);
        }
        UnitAttacker g = Find("초월_박기찬_AD"), p = Find("흔함_박민수");
        if (g != null && g.TryGetComponent(out UnityEngine.AI.NavMeshAgent a1)) a1.Warp(c + Vector3.back * 30f);
        if (p != null && p.TryGetComponent(out UnityEngine.AI.NavMeshAgent a2)) a2.Warp(c + Vector3.back * 45f);
        return $"   표적 {targets.Count}마리(데이터 방어 {data.armor}) + 박기찬·박민수 곁에 세움";
    }

    public static string Report()
    {
        UnitAttacker g = Find("초월_박기찬_AD"), p = Find("흔함_박민수");
        if (g == null) return "❌ 박기찬 없음";
        UnitData d = g.GetComponent<UnitIdentity>().Data;
        StringBuilder sb = new StringBuilder($"   「{d.unitName}」 스킬 {d.skills.Count}: {string.Join(" / ", d.skills.Select(s => s.skillName.Split('(')[0].Trim()))}\n");
        sb.AppendLine($"   멀티샷: 추가 {d.attackExtraTargets}마리 · 반경 {d.attackExtraTargetRadius:F0} · 체력 게이지 {Get<int>(g, "lifeGaugeCounter")}/{d.lifeGaugeMax} · trait {(d.trait == null ? "없음" : d.trait.name)}");
        sb.AppendLine($"   공속 배율: 박기찬(자신) {g.CurrentAttackSpeedMultiplier:F3} · 박민수(아군) {(p != null ? p.CurrentAttackSpeedMultiplier.ToString("F3") : "-")}");
        if (targets.Count > 0)
        {
            float baseArmor = Get<EnemyData>(targets[0], "data").armor;
            var armors = targets.Select(t => t.EffectiveArmor).ToList();
            int hit = targets.Count(t => t.Hp < t.MaxHp - 0.5f), stunned = targets.Count(t => t.IsStunned);
            float slowest = targets.Min(t => t.EffectiveSlowMultiplier);
            sb.AppendLine($"   표적 6: 데이터 방어 {baseArmor:F1} → 실효 {armors.Min():F1}~{armors.Max():F1} (차 {armors.Min() - baseArmor:F1}~{armors.Max() - baseArmor:F1}) · 맞은 수 {hit} · 스턴 {stunned}마리 · 가장 느린 이속 배율 {slowest:F2}");
        }
        return sb.ToString();
    }

    /// 체력 게이지를 49로 → 다음 평타에 50 → 무언가의분출(둔화) 발동 기대.
    public static string Gauge49()
    {
        UnitAttacker g = Find("초월_박기찬_AD");
        typeof(UnitAttacker).GetField("lifeGaugeInitialized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, true);
        typeof(UnitAttacker).GetField("lifeGaugeCounter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, 49);
        return "   박기찬 체력 게이지 → 49";
    }
}
