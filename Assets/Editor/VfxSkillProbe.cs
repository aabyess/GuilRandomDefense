using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 스킬별 팩 이펙트(SkillVfxTable, 09-30) 실판 점검 — 표에 팩 프리팹이 걸린 특별함 이상 유닛 여섯을 0번 레인 가운데 한 줄로 세우고
// 앞에 표적 셋씩(체력 1e6, 멈춤). 판 동안 SkillVfx.PlayedPrefab을 세서 「무엇이 몇 번 떴나」를 낸다. 사진은 gameshot snap:으로.
//   gameshot vfxskill.png 1 1920x1080 click?:보통 wait:2 call:VfxSkillProbe.Arena wait:6 snap:vfxskill_a wait:3 snap:vfxskill_b wait:3 call:VfxSkillProbe.Report
static class VfxSkillProbe
{
    static readonly Dictionary<int, int> played = new Dictionary<int, int>();
    static readonly List<string> picked = new List<string>();

    static string Arena()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        SkillVfxTable table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        EnemyData dummyData = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (table == null || spawner == null || lane == null || dummyData == null) return "❌ 표·UnitSpawner·레인·표적 없음";

        // 팩 프리팹(범위 칸 우선)이 걸린 스킬을 가진 특별함 이상 유닛 — 프리팹이 겹치지 않게 여섯.
        var skillToEntry = table.entries.Where(e => e.skill != null).ToDictionary(e => e.skill, e => e);
        var usedPrefabs = new HashSet<int>();
        var units = new List<UnitData>();
        foreach (UnitData u in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
                     .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
                     .Where(u => u != null && u.grade >= UnitGrade.Special).OrderByDescending(u => u.grade))
        {
            SerializedObject so = new SerializedObject(u);
            var skills = new List<SkillData>();
            SerializedProperty one = so.FindProperty("skill");
            if (one != null && one.objectReferenceValue is SkillData s1) skills.Add(s1);
            SerializedProperty many = so.FindProperty("skills");
            if (many != null && many.isArray)
                for (int i = 0; i < many.arraySize; i++)
                    if (many.GetArrayElementAtIndex(i).objectReferenceValue is SkillData s2) skills.Add(s2);
            SkillVfxTable.Entry hit = skills.Where(skillToEntry.ContainsKey).Select(s => skillToEntry[s])
                .FirstOrDefault(e => e.area.prefab >= 0 && !usedPrefabs.Contains(e.area.prefab));
            if (hit == null) continue;
            usedPrefabs.Add(hit.area.prefab);
            units.Add(u);
            picked.Add($"{u.name} ← {hit.skill.name} 범위 {table.prefabs[hit.area.prefab].name}"
                       + (hit.hit.prefab >= 0 ? $" · 적중 {table.prefabs[hit.hit.prefab].name}" : "")
                       + (hit.caster.prefab >= 0 ? $" · 시전자 {table.prefabs[hit.caster.prefab].name}" : ""));
            if (units.Count >= 6) break;
        }

        Vector3 c = lane.LaneCenter;
        for (int k = 0; k < units.Count; k++)
        {
            Vector3 home = c + new Vector3((k - (units.Count - 1) * 0.5f) * 140f, 0f, -40f);
            for (int i = 0; i < 3; i++)
            {
                GameObject go = Object.Instantiate(dummyData.prefab, home + new Vector3((i - 1) * 25f, 0f, 70f), Quaternion.Euler(0f, 180f, 0f));
                if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
                if (go.TryGetComponent(out EnemyDummy d)) { d.Initialize(dummyData, 1e6f); d.SetLane(-1); }
            }
            spawner.Spawn(units[k], home, 0);
        }
        played.Clear();
        SkillVfx.PlayedPrefab -= OnPlayed;
        SkillVfx.PlayedPrefab += OnPlayed;
        if (cam != null) cam.MoveTo(c);
        return $"유닛 {units.Count}:\n   " + string.Join("\n   ", picked);
    }

    static void OnPlayed(int index, Vector3 position, float diameter, bool ground) =>
        played[index] = played.TryGetValue(index, out int n) ? n + 1 : 1;

    static string Report()
    {
        SkillVfx.PlayedPrefab -= OnPlayed;
        SkillVfxTable table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        var sb = new StringBuilder("팩 이펙트 재생 횟수:\n");
        foreach (var kv in played.OrderByDescending(k => k.Value))
            sb.AppendLine($"   {table.prefabs[kv.Key].name} × {kv.Value}");
        if (played.Count == 0) sb.AppendLine("   ⚠️ 0 — 한 번도 안 떴다");
        return sb.ToString();
    }
}
