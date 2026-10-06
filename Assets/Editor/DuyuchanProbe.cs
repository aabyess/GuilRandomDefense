using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 두유찬 공속·체력 게이지 조정 실측(10-07 구현담당3) — gameshot call:DuyuchanProbe.R60 wait:70 call:DuyuchanProbe.Report (또는 .R45).
/// 두유찬과 대조군(양재모)을 각자 보스 하나와 단둘이 세우고(신 보스 ×8.25) 60초 동안: 혼신의일격 발동 횟수·평균 간격 · 처음 10초 보스 체력 감소 %/초 · 정보 공속.
/// </summary>
static class DuyuchanProbe
{
    class Duel { public UnitData data; public EnemyDummy boss; public GameObject unit; public float bornAt, lastT, at10 = -1f; public float hpAt10; }
    static readonly List<Duel> duels = new List<Duel>();
    static string bossName;
    static float multiplier;

    static string R60() => Build("Enemy_R60_정윤식", 8.25f);
    static string R45() => Build("Enemy_R45_이현빈", 8.25f);

    static string Build(string enemyName, float mult)
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData boss = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{enemyName}.asset");
        if (spawner == null || lane == null || boss == null) return "❌ 준비 실패";
        bossName = enemyName; multiplier = mult;
        duels.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        string[] names = { "초월_두유찬_AD", "초월_양재모_AD" };
        for (int u = 0; u < names.Length; u++)
        {
            UnitData source = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{names[u]}.asset");
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 180f, 0f) * Vector3.forward * 3000f;
            GameObject go = Object.Instantiate(boss.prefab, home + Vector3.forward * 4f, Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            EnemyDummy dummy = go.GetComponent<EnemyDummy>();
            dummy.Initialize(boss, mult);
            dummy.SetLane(-1);
            GameObject unit = spawner.Spawn(source, home, 0);
            duels.Add(new Duel { data = source, boss = dummy, unit = unit, bornAt = Time.time, lastT = Time.time });
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
        return $"결투 {duels.Count} · 보스 {enemyName} ×{mult}(체력 {boss.hp * mult:0} · 방어 {boss.armor})";
    }

    static void Sample()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Sample; return; }
        foreach (Duel d in duels)
            if (d.at10 < 0f && d.boss != null && Time.time - d.bornAt >= 10f) { d.at10 = Time.time - d.bornAt; d.hpAt10 = d.boss.HpRatio; }
    }

    static string Report()
    {
        EditorApplication.update -= Sample;
        var sb = new StringBuilder($"\n보스 {bossName} ×{multiplier} · 게임 시간 {Time.time - (duels.Count > 0 ? duels[0].bornAt : 0f):0.0}초");
        foreach (Duel d in duels)
        {
            var casts = SkillTelemetry.CastLog.Where(c => c.unit == d.data && c.skill.Contains("혼신")).Select(c => c.time).ToList();
            string cadence = casts.Count >= 2 ? $"평균 간격 {(casts[casts.Count - 1] - casts[0]) / (casts.Count - 1):0.0}초" : "간격 측정 불가";
            UnitAttacker atk = d.unit != null ? d.unit.GetComponent<UnitAttacker>() : null;
            float elapsed = Time.time - d.bornAt;
            sb.Append($"\n{d.data.name}: 공속 {d.data.attackSpeed}(실제 간격 {(atk != null ? atk.AttackInterval : 0f):0.000}초) · 평타 {SkillTelemetry.HitsOf(d.data)}타 · 혼신의일격 {casts.Count}회 {cadence}"
                      + $" · 처음 {d.at10:0.0}초 체력 감소 {(1f - d.hpAt10) * 100f / Mathf.Max(0.1f, d.at10):0.00}%/초 · 지금 남은 체력 {(d.boss != null ? d.boss.HpRatio : 0f):P1}");
        }
        return sb.ToString();
    }
}
