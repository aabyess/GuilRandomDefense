using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 배타 분기(SkillLevel.exclusiveGroup) 실측(2026-09-30, 구조 칸 백로그 3번) — 묶음이 걸린 로스터를 표적 셋과 세우고,
// 묶음 스킬별 시전 수 ÷ 평타 수(= 주변 확률)와 「같은 평타에 묶음 스킬 둘이 같이 터진 횟수」(0이어야 한다)를 낸다.
// gameshot: gameshot x.png 1 1920x1080 click?:보통 wait:2 call:ExclusiveProbe.Arena wait:80 call:ExclusiveProbe.Report
static class ExclusiveProbe
{
    static readonly string[] Rosters = { "불멸_고도현", "불멸_이이삭", "전설적인_박민수", "전설적인_임건웅", "제한_강보명", "초월_배성령_AD", "랜덤_한마_바키" };
    static readonly List<UnitData> fielded = new List<UnitData>();
    static readonly Dictionary<EnemyDummy, Vector3> targets = new Dictionary<EnemyDummy, Vector3>();
    static EnemyData enemy;

    static EnemyDummy Target(Vector3 at)
    {
        GameObject go = Object.Instantiate(enemy.prefab, at, Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        if (!go.TryGetComponent(out EnemyDummy dummy)) return null;
        dummy.Initialize(enemy, 1e9f / Mathf.Max(1f, enemy.hp));
        dummy.SetLane(-1);
        return dummy;
    }

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        enemy = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R01"));
        if (spawner == null || lane == null || enemy == null) return "❌ 준비 실패";
        fielded.Clear(); targets.Clear();
        SkillTelemetry.Reset();
        SkillTelemetry.Enabled = true;
        for (int u = 0; u < Rosters.Length; u++)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Rosters[u]}.asset");
            if (data == null) continue;
            Vector3 home = lane.LaneCenter + Quaternion.Euler(0f, u * 360f / Rosters.Length, 0f) * Vector3.forward * 2600f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = home + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 4f;
                targets[Target(at)] = at;
            }
            spawner.Spawn(data, home, 0);
            fielded.Add(data);
        }
        RoundManager rm = Object.FindFirstObjectByType<RoundManager>();
        typeof(RoundManager).GetField("deathCountEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(rm, false);
        EditorApplication.update -= Refill;
        EditorApplication.update += Refill;
        Time.timeScale = 6f;
        return $"로스터 {fielded.Count} · 6배속";
    }

    // 죽은 표적은 같은 자리에 다시 세운다(평타가 끊기지 않게).
    static void Refill()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Refill; return; }
        foreach (var kv in targets.ToList())
            if (kv.Key == null || kv.Key.IsDead) { targets.Remove(kv.Key); targets[Target(kv.Value)] = kv.Value; }
    }

    static string Report()
    {
        EditorApplication.update -= Refill;
        Time.timeScale = 1f;
        var sb = new StringBuilder();
        foreach (UnitData u in fielded)
        {
            var grouped = new List<SkillData>();
            for (int i = 0; i < u.SkillCount; i++)
            {
                SkillData s = u.SkillAt(i);
                if (s != null && s.levels.Count > 0 && s.levels[0].exclusiveGroup != 0) grouped.Add(s);
            }
            int hits = SkillTelemetry.HitsOf(u);
            var names = new HashSet<string>(grouped.Select(s => s.skillName));
            List<SkillTelemetry.CastEvent> casts = SkillTelemetry.CastLog.Where(e => e.unit == u && names.Contains(e.skill)).ToList();
            int both = casts.GroupBy(e => e.time).Count(g => g.Select(e => e.skill).Distinct().Count() > 1);
            sb.Append($"\n{u.name}: 평타 {hits} · 같은 타에 둘 다 {both}");
            foreach (SkillData s in grouped)
            {
                int n = casts.Count(e => e.skill == s.skillName);
                sb.Append($" · 「{(s.skillName.Length > 18 ? s.skillName.Substring(0, 18) : s.skillName)}」 {n}회 = {(float)n / Mathf.Max(1, hits):P2}(조건부 {s.levels[0].triggerChance:P2})");
            }
        }
        return sb.ToString();
    }
}
