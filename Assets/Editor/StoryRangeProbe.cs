using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 스토리존 착지·사거리 실측(10-08 「공격 범위 최대한 끝에서 바로 때리게」) — gameshot:
//   call:StoryRangeProbe.Setup(스토리 2를 세우고 체력 크게, 사거리 긴 둘+짧은 하나를 레인 0에 세워 스토리 포탈로 보냄) → wait:25 → call:StoryRangeProbe.Report → snap:
static class StoryRangeProbe
{
    static readonly System.Collections.Generic.List<UnitIdentity> units = new System.Collections.Generic.List<UnitIdentity>();
    static EnemyDummy enemy;

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var forceN = typeof(StoryHpProbe).GetMethod("ForceN", BindingFlags.Static | BindingFlags.NonPublic);
        string story = (string)forceN.Invoke(null, new object[] { 2 });
        var sm = StoryManager.Instance;
        enemy = typeof(StoryManager).GetField("activeEnemy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sm) as EnemyDummy;
        if (enemy == null) return "❌ 스토리 적 없음 · " + story;
        typeof(EnemyDummy).GetField("hp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, 1e9f);
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        GameObject portal = GameObject.Find("Lane1_스토리포탈");
        var roster = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
            .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(u => u != null && u.prefab != null && !u.isSystemUnit && u.attackRange > 0f && u.grade != UnitGrade.TranscendentWisp && u.grade != UnitGrade.Common).ToList();
        var ranged = roster.OrderByDescending(u => u.attackRange).Take(2).ToList();
        var melee = roster.OrderBy(u => u.attackRange).First();
        units.Clear();
        var sb = new StringBuilder(story + "\n");
        int i = 0;
        foreach (UnitData d in ranged.Append(melee))
        {
            GameObject go = spawner.Spawn(d, lane.LaneCenter + new Vector3(-60f + i * 40f, 0f, 20f), 0);
            units.Add(go.GetComponent<UnitIdentity>());
            go.GetComponent<UnitCombat>().IssueMoveCommand(portal.transform.position);
            sb.AppendLine($"  세움 {d.DisplayName} 사거리 {d.attackRange:F1}");
            i++;
        }
        return sb.ToString();
    }

    static string Report()
    {
        if (enemy == null) return "❌ Setup 먼저";
        var sb = new StringBuilder($"스토리 적 {enemy.transform.position:F0}\n");
        foreach (UnitIdentity u in units)
        {
            if (u == null) { sb.AppendLine("  (유닛 없음)"); continue; }
            var atk = u.GetComponent<UnitAttacker>();
            float flat = Vector2.Distance(new Vector2(u.transform.position.x, u.transform.position.z), new Vector2(enemy.transform.position.x, enemy.transform.position.z));
            float full = Vector3.Distance(u.transform.position, enemy.transform.position);
            var combat = u.GetComponent<UnitCombat>();
            string state = typeof(UnitCombat).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(combat)?.ToString();
            sb.AppendLine($"  {u.Data.DisplayName} 사거리 {atk.AttackRange:F1} · 적까지 {full:F1}(수평 {flat:F1}) = 사거리의 {full / atk.AttackRange:P0} · 상태 {state} · 평타 {atk.BasicHitCount}회 · 위치 {u.transform.position:F0}");
        }
        return sb.ToString();
    }
}
