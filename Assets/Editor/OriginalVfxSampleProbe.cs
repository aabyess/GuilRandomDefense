using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>원작 이펙트 실발동 표본(10-08 PM): 초월·불멸·영원·전설 각 1기를 0번 레인에 세우고 앞에 표적 셋씩. 원작 프리팹(OriginalVfxPlayer)이 걸린 스킬을 가진 유닛만 고른다.
///   gameshot vfxsample.png 1 1920x1080 click?:보통 wait:2 call:OriginalVfxSampleProbe.Arena wait:5 snap:a.png wait:3 snap:b.png wait:3 call:OriginalVfxSampleProbe.Report</summary>
static class OriginalVfxSampleProbe
{
    static readonly Dictionary<int, int> played = new Dictionary<int, int>();
    static readonly UnitGrade[] Grades = { UnitGrade.Transcendent, UnitGrade.Immortal, UnitGrade.Eternal, UnitGrade.Legendary };

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
        var skillToEntry = table.entries.Where(e => e.skill != null).ToDictionary(e => e.skill, e => e);
        bool IsOriginal(int idx) => idx >= 0 && idx < table.prefabs.Count && table.prefabs[idx] != null && table.prefabs[idx].GetComponent<OriginalVfxPlayer>() != null;

        var units = new List<UnitData>(); var lines = new List<string>();
        foreach (UnitGrade grade in Grades)
        {
            foreach (UnitData u in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" })
                         .Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g)))
                         .Where(u => u != null && u.grade == grade && !u.isSystemUnit).OrderBy(u => u.name))
            {
                var skills = new List<SkillData>();
                SerializedObject so = new SerializedObject(u);
                SerializedProperty one = so.FindProperty("skill");
                if (one != null && one.objectReferenceValue is SkillData s1) skills.Add(s1);
                SerializedProperty many = so.FindProperty("skills");
                if (many != null && many.isArray) for (int i = 0; i < many.arraySize; i++) if (many.GetArrayElementAtIndex(i).objectReferenceValue is SkillData s2) skills.Add(s2);
                SkillVfxTable.Entry hit = skills.Where(skillToEntry.ContainsKey).Select(s => skillToEntry[s]).FirstOrDefault(e => IsOriginal(e.area.prefab) || IsOriginal(e.hit.prefab) || IsOriginal(e.caster.prefab));
                if (hit == null) continue;
                units.Add(u);
                lines.Add($"{grade}: {u.name} ← {hit.skill.name} 범위 {(IsOriginal(hit.area.prefab) ? table.prefabs[hit.area.prefab].name : "-")} · 적중 {(IsOriginal(hit.hit.prefab) ? table.prefabs[hit.hit.prefab].name : "-")} · 시전자 {(IsOriginal(hit.caster.prefab) ? table.prefabs[hit.caster.prefab].name : "-")}");
                break;
            }
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
        played.Clear(); SkillVfx.PlayedPrefab -= OnPlayed; SkillVfx.PlayedPrefab += OnPlayed;
        if (cam != null) cam.MoveTo(c);
        return $"유닛 {units.Count}:\n   " + string.Join("\n   ", lines);
    }

    static void OnPlayed(int index, Vector3 position, float diameter, bool ground) { played[index] = played.TryGetValue(index, out int n) ? n + 1 : 1; }

    static string Report()
    {
        SkillVfx.PlayedPrefab -= OnPlayed;
        SkillVfxTable table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        var sb = new StringBuilder("재생 횟수:\n");
        foreach (var kv in played.OrderByDescending(k => k.Value)) sb.AppendLine($"   {table.prefabs[kv.Key].name} × {kv.Value}");
        if (played.Count == 0) sb.AppendLine("   ⚠️ 0 — 한 번도 안 떴다");
        return sb.ToString();
    }
}
