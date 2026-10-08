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
    static readonly List<(int index, Vector3 pos, float diameter, bool ground)> plays = new List<(int, Vector3, float, bool)>();
    static readonly List<(string name, Vector3 pos)> placed = new List<(string, Vector3)>();
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
        placed.Clear();
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
            placed.Add((units[k].name, home));
        }
        played.Clear(); plays.Clear(); SkillVfx.PlayedPrefab -= OnPlayed; SkillVfx.PlayedPrefab += OnPlayed;
        if (cam != null) cam.MoveTo(c);
        return $"유닛 {units.Count}:\n   " + string.Join("\n   ", lines);
    }

    static void OnPlayed(int index, Vector3 position, float diameter, bool ground) { played[index] = played.TryGetValue(index, out int n) ? n + 1 : 1; if (plays.Count < 200) plays.Add((index, position, diameter, ground)); }

    static string Scan()
    {
        LaneMarker lane = LaneMarker.Get(0); Vector3 c = lane.LaneCenter;
        var sb = new StringBuilder("활성 이펙트(레인 중심에서 150 넘게 떨어진 것 + 원작 재생기 전부):\n");
        foreach (OriginalVfxPlayer pl in Object.FindObjectsByType<OriginalVfxPlayer>(FindObjectsSortMode.None))
            if (pl.gameObject.activeInHierarchy) sb.AppendLine($"   [원작] {pl.name} 위치 {pl.transform.position:F0} 크기배율 {pl.transform.localScale.x:F1} 재생중 {pl.IsPlaying}");
        foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (!ps.isPlaying && !ps.IsAlive()) continue;
            Vector3 d = ps.transform.position - c; d.y = 0;
            if (d.magnitude > 150f) sb.AppendLine($"   [파티클] {ps.transform.root.name}/{ps.name} 위치 {ps.transform.position:F0} 중심에서 {d.magnitude:F0}");
        }
        sb.AppendLine("활성 렌더러(Particles/Unlit 재질, Map 밖, 중심에서 100 넘게 떨어진 것):");
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
            Material m = r.sharedMaterial; if (m == null || m.shader == null || !m.shader.name.Contains("Particles")) continue;
            Vector3 d = r.bounds.center - c; d.y = 0;
            if (d.magnitude > 100f) sb.AppendLine($"   {r.transform.root.name}/{r.name} 경계중심 {r.bounds.center:F0} 크기 {r.bounds.size:F0} 재질 {m.name}");
        }
        return sb.ToString();
    }

    static string Report()
    {
        SkillVfx.PlayedPrefab -= OnPlayed;
        SkillVfxTable table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        var sb = new StringBuilder("재생 횟수:\n");
        foreach (var kv in played.OrderByDescending(k => k.Value)) sb.AppendLine($"   {table.prefabs[kv.Key].name} × {kv.Value}");
        if (played.Count == 0) sb.AppendLine("   ⚠️ 0 — 한 번도 안 떴다");
        sb.AppendLine("생성 좌표 표(이펙트 · 지름 · 땅 · 좌표 · 가장 가까운 유닛까지 수평거리/높이차):");
        foreach (var g in plays.GroupBy(p => p.index))
        {
            var p = g.First();
            (string name, Vector3 pos) near = placed.OrderBy(u => (new Vector3(u.pos.x - p.pos.x, 0, u.pos.z - p.pos.z)).magnitude).First();
            sb.AppendLine($"   {table.prefabs[p.index].name} ×{g.Count()} · 지름 {p.diameter:F1} · 땅={p.ground} · {p.pos:F0} · {near.name} 까지 {new Vector3(near.pos.x - p.pos.x, 0, near.pos.z - p.pos.z).magnitude:F0} / 높이 {p.pos.y - near.pos.y:F1}");
        }
        return sb.ToString();
    }
}
