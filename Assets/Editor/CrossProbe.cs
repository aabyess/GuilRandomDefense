using System.Text;
using UnityEngine;
using UnityEngine.AI;

/// <summary>레인 사이 십자(흙 대지) 뚫림 점검(10-07) — call CrossProbe.Run (편집 모드). 십자 벽 콜라이더·NavMesh 걷기 격자·레인끼리 지상 경로.</summary>
static class CrossProbe
{
    static string Run()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "레인간_세로벽", "레인간_가로벽", "레인간_언덕" })
        {
            GameObject g = GameObject.Find(n);
            if (g == null) { sb.Append($"\n   {n}: ❌ 씬에 없음"); continue; }
            var col = g.GetComponentsInChildren<Collider>(true);
            Bounds b = col.Length > 0 ? col[0].bounds : new Bounds();
            sb.Append($"\n   {n}: 활성 {g.activeInHierarchy} · 콜라이더 {col.Length}개(켜짐 {System.Array.FindAll(col, c => c.enabled).Length}) · 첫 콜라이더 {(col.Length > 0 ? col[0].GetType().Name + " trigger=" + col[0].isTrigger + " " + b.center.ToString("F0") + " 크기 " + b.size.ToString("F0") : "-")}");
        }
        int sea = NavMesh.GetAreaFromName("Sea");
        int groundMask = ~(1 << (sea >= 0 ? sea : 31)) & NavMesh.AllAreas;
        var lanes = MapLayout.Lanes;
        sb.Append($"\n   레인 {lanes.Length}개 · Sea 영역 {sea}");
        for (int i = 0; i < lanes.Length; i++)
            for (int j = i + 1; j < lanes.Length; j++)
            {
                Vector3 a = new Vector3(lanes[i].center.x, MapLayout.IslandTop, lanes[i].center.y);
                Vector3 c = new Vector3(lanes[j].center.x, MapLayout.IslandTop, lanes[j].center.y);
                bool okA = NavMesh.SamplePosition(a, out NavMeshHit ha, 80f, groundMask), okC = NavMesh.SamplePosition(c, out NavMeshHit hc, 80f, groundMask);
                var path = new NavMeshPath();
                bool found = okA && okC && NavMesh.CalculatePath(ha.position, hc.position, groundMask, path);
                sb.Append($"\n   지상 경로 레인{i}→레인{j}: {(found ? path.status.ToString() : "경로 없음")}");
            }
        // 십자 구역 걷기 격자
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        foreach (var l in lanes) { minX = Mathf.Min(minX, l.center.x - l.size.x * 0.5f); maxX = Mathf.Max(maxX, l.center.x + l.size.x * 0.5f); minZ = Mathf.Min(minZ, l.center.y - l.size.y * 0.5f); maxZ = Mathf.Max(maxZ, l.center.y + l.size.y * 0.5f); }
        float vX = (lanes[0].center.x + lanes[0].size.x * 0.5f + lanes[1].center.x - lanes[1].size.x * 0.5f) * 0.5f;
        float hZ = (lanes[0].center.y - lanes[0].size.y * 0.5f - MapLayout.LaneApronDepth + lanes[2].center.y + lanes[2].size.y * 0.5f) * 0.5f;
        int vWalk = 0, vAll = 0, hWalk = 0, hAll = 0, vSeaWalk = 0, hSeaWalk = 0;
        for (float z = minZ; z <= maxZ; z += 20f) { vAll++; Cell(new Vector3(vX, MapLayout.IslandTop, z), groundMask, sea, ref vWalk, ref vSeaWalk); }
        for (float x = minX; x <= maxX; x += 20f) { hAll++; Cell(new Vector3(x, MapLayout.IslandTop, hZ), groundMask, sea, ref hWalk, ref hSeaWalk); }
        sb.Append($"\n   십자 세로 중심선(x {vX:0}) 지상 걷기 {vWalk}/{vAll}칸 · 가로 중심선(z {hZ:0}) 지상 걷기 {hWalk}/{hAll}칸 (Sea 영역으로만 걸을 수 있는 칸: 세로 {vSeaWalk} · 가로 {hSeaWalk})");
        return sb.ToString();
    }

    static void Cell(Vector3 p, int groundMask, int sea, ref int walk, ref int seaWalk)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit h, 3f, groundMask)) walk++;
        else if (sea >= 0 && NavMesh.SamplePosition(p, out NavMeshHit hs, 3f, 1 << sea)) seaWalk++;
    }

    // ───── 플레이 중: 지상 유닛 둘(걸음·배성령 순간이동)을 레인 0에서 십자·옆 레인 쪽으로 보내 본다 — gameshot call:CrossProbe.WalkSetup wait:20 call:CrossProbe.WalkReport
    static UnitIdentity walker, teleporter;
    static Vector3 start;
    static int LaneOf(Vector3 p)
    {
        var lanes = MapLayout.Lanes;
        for (int i = 0; i < lanes.Length; i++)
            if (Mathf.Abs(p.x - lanes[i].center.x) <= lanes[i].size.x * 0.5f + 2f && Mathf.Abs(p.z - lanes[i].center.y) <= lanes[i].size.y * 0.5f + MapLayout.LaneApronDepth + 2f) return i;
        return -1;
    }

    static string WalkSetup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData Load(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
        var lanes = MapLayout.Lanes;
        start = lane.LaneCenter + new Vector3(lanes[0].size.x * 0.4f, 0f, 0f);   // 레인 0 오른쪽 가장자리 근처(옆 레인 1과 세로 십자 쪽)
        walker = spawner.Spawn(Load("흔함_박민수"), start, 0).GetComponent<UnitIdentity>();
        teleporter = spawner.Spawn(Load("초월_배성령_AD"), start + new Vector3(0f, 0f, 60f), 0).GetComponent<UnitIdentity>();
        Vector3 target = new Vector3(lanes[1].center.x, MapLayout.IslandTop, lanes[1].center.y);   // 옆 레인 중심
        walker.GetComponent<UnitMover>().MoveToGroundPoint(target, "십자 건너 옆 레인");
        // 순간이동: 십자 한가운데를 찍는다
        float vX = (lanes[0].center.x + lanes[0].size.x * 0.5f + lanes[1].center.x - lanes[1].size.x * 0.5f) * 0.5f;
        Vector3 cross = new Vector3(vX, MapLayout.IslandTop, teleporter.transform.position.z);
        SkillData tp = null;
        foreach (SkillData sk in teleporter.Data.skills) if (sk != null && sk.levels != null && sk.levels.Count > 0 && sk.levels[0].needsPointClick) tp = sk;
        string tpResult = "순간이동 스킬 없음";
        if (tp != null) { bool ok = teleporter.GetComponent<UnitAttacker>().TryCastActiveAtPoint(tp, cross, out string why); tpResult = ok ? "시전" : "실패: " + why; }
        return $"출발 {start:F0}(레인 {LaneOf(start)}) → 옆 레인 중심 {target:F0} 이동 명령 · 순간이동 십자 중심 {cross:F0} 찍음: {tpResult} · 순간이동 직후 위치 {teleporter.transform.position:F0}(레인 {LaneOf(teleporter.transform.position)})";
    }

    static string WalkReport()
    {
        return $"\n   걷는 유닛: {walker.transform.position:F0} → 레인 {LaneOf(walker.transform.position)} (옆 레인 중심으로 보냈다 — 레인 1이면 뚫림, 0이면 막힘)\n   순간이동 유닛: {teleporter.transform.position:F0} → 레인 {LaneOf(teleporter.transform.position)} (십자 중심을 찍었다 — 레인 1·-1이면 건너감)";
    }

    // 십자 한가운데 NavMesh가 어느 높이·어느 영역에 있는지 + 레인 가장자리에서 거기로 가는 지상 경로(편집 모드).
    static string Top()
    {
        var sb = new StringBuilder();
        var lanes = MapLayout.Lanes;
        float vX = (lanes[0].center.x + lanes[0].size.x * 0.5f + lanes[1].center.x - lanes[1].size.x * 0.5f) * 0.5f;
        Vector3 cross = new Vector3(vX, MapLayout.IslandTop, lanes[0].center.y);
        int sea = NavMesh.GetAreaFromName("Sea");
        int groundMask = ~(1 << (sea >= 0 ? sea : 31)) & NavMesh.AllAreas;
        for (float dy = 0f; dy <= 40f; dy += 4f)
        {
            Vector3 q = cross + Vector3.up * dy;
            bool ok = NavMesh.SamplePosition(q, out NavMeshHit h, 2f, NavMesh.AllAreas);
            if (ok) sb.Append($"\n   십자 중심 y={q.y:0}: NavMesh 있음 (y {h.position.y:0.0}, 영역 {h.mask}) ");
        }
        Vector3 edge = new Vector3(lanes[0].center.x + lanes[0].size.x * 0.5f - 6f, MapLayout.IslandTop, lanes[0].center.y);
        bool eok = NavMesh.SamplePosition(edge, out NavMeshHit eh, 40f, groundMask);
        bool top = NavMesh.SamplePosition(cross + Vector3.up * 6f, out NavMeshHit th, 12f, NavMesh.AllAreas);
        sb.Append($"\n   레인0 오른쪽 가장자리 {edge:F0} NavMesh {eok} · 십자 윗면 NavMesh {top} {(top ? th.position.ToString("F1") : "")}");
        if (eok && top)
        {
            var path = new NavMeshPath();
            bool f = NavMesh.CalculatePath(eh.position, th.position, groundMask, path);
            sb.Append($"\n   가장자리 → 십자 윗면 지상 경로: {(f ? path.status.ToString() : "없음")} (모서리 {path.corners.Length}개)");
        }
        return sb.ToString();
    }

    // 십자 윗면 격자(벽 콜라이더 윗면 y≈14)의 각 점이 레인 0·1·2·3 중심에서 지상으로 닿는가(편집 모드).
    static string Top2()
    {
        var sb = new StringBuilder();
        var lanes = MapLayout.Lanes;
        int sea = NavMesh.GetAreaFromName("Sea");
        int groundMask = ~(1 << (sea >= 0 ? sea : 31)) & NavMesh.AllAreas;
        float vX = (lanes[0].center.x + lanes[0].size.x * 0.5f + lanes[1].center.x - lanes[1].size.x * 0.5f) * 0.5f;
        float hZ = (lanes[0].center.y - lanes[0].size.y * 0.5f - MapLayout.LaneApronDepth + lanes[2].center.y + lanes[2].size.y * 0.5f) * 0.5f;
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        foreach (var l in lanes) { minX = Mathf.Min(minX, l.center.x - l.size.x * 0.5f); maxX = Mathf.Max(maxX, l.center.x + l.size.x * 0.5f); minZ = Mathf.Min(minZ, l.center.y - l.size.y * 0.5f); maxZ = Mathf.Max(maxZ, l.center.y + l.size.y * 0.5f); }
        var starts = new Vector3[lanes.Length];
        for (int i = 0; i < lanes.Length; i++) { NavMesh.SamplePosition(new Vector3(lanes[i].center.x, MapLayout.IslandTop, lanes[i].center.y), out NavMeshHit h, 80f, groundMask); starts[i] = h.position; }
        int topCells = 0; var reach = new int[lanes.Length]; var examples = new System.Collections.Generic.List<string>();
        void Test(Vector3 p)
        {
            if (!NavMesh.SamplePosition(p + Vector3.up * 6f, out NavMeshHit th, 6f, NavMesh.AllAreas) || th.position.y < MapLayout.IslandTop + 2f) return;
            topCells++;
            for (int i = 0; i < lanes.Length; i++)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(starts[i], th.position, groundMask, path) && path.status == NavMeshPathStatus.PathComplete) { reach[i]++; if (examples.Count < 4) examples.Add($"레인{i}→{th.position:F0}"); }
            }
        }
        for (float z = minZ; z <= maxZ; z += 60f) Test(new Vector3(vX, MapLayout.IslandTop, z));
        for (float x = minX; x <= maxX; x += 60f) Test(new Vector3(x, MapLayout.IslandTop, hZ));
        sb.Append($"\n   십자 윗면 NavMesh 칸 {topCells}개 · 레인별 지상으로 닿는 칸 수: {string.Join(" · ", System.Array.ConvertAll(reach, r => r.ToString()))} · 예 {string.Join(", ", examples)}");
        return sb.ToString();
    }

    // ───── 플레이 중: 등급별 지상 유닛을 레인 0 오른쪽 가장자리에 세우고 십자 한가운데(윗면 y14)·옆 레인 중심으로 우클릭 이동 — wait:14 뒤 WalkMany2Report
    static readonly System.Collections.Generic.List<(UnitIdentity unit, Vector3 target, string label)> many = new System.Collections.Generic.List<(UnitIdentity, Vector3, string)>();
    static string WalkMany()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        many.Clear();
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var lanes = MapLayout.Lanes;
        float vX = (lanes[0].center.x + lanes[0].size.x * 0.5f + lanes[1].center.x - lanes[1].size.x * 0.5f) * 0.5f;
        string[] names = { "흔함_박민수", "안흔함_박민수", "희귀함_박민수", "특별함_박민수", "전설적인_박민수", "초월_박민수_AD", "초월_최상호_AD", "불멸_고도현", "영원_서민성" };
        int n = 0;
        foreach (string nm in names)
        {
            UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{nm}.asset");
            if (d == null) { many.Add((null, Vector3.zero, nm + " (에셋 없음)")); continue; }
            Vector3 pos = lane.LaneCenter + new Vector3(lanes[0].size.x * 0.4f, 0f, -300f + n * 70f);
            GameObject go = spawner.Spawn(d, pos, 0);
            UnitIdentity id = go.GetComponent<UnitIdentity>();
            bool toCross = n % 2 == 0;
            Vector3 tgt = toCross ? new Vector3(vX, 14f, pos.z) : new Vector3(lanes[1].center.x, MapLayout.IslandTop, pos.z);
            id.GetComponent<UnitMover>().MoveToGroundPoint(tgt, toCross ? "십자 윗면" : "옆 레인");
            many.Add((id, tgt, nm + (toCross ? " → 십자 한가운데" : " → 옆 레인 중심")));
            n++;
        }
        return $"{n}기 세움, 우클릭 이동 명령";
    }

    static string WalkManyReport()
    {
        var sb = new StringBuilder();
        foreach (var m in many)
        {
            if (m.unit == null) { sb.Append($"\n   {m.label}"); continue; }
            var ag = m.unit.GetComponent<NavMeshAgent>();
            sb.Append($"\n   {m.label}: 위치 {m.unit.transform.position:F0} 레인 {LaneOf(m.unit.transform.position)} · 높이 {m.unit.transform.position.y:0.0} · agent {(ag != null ? ag.enabled + " onMesh " + ag.isOnNavMesh + " 경로 " + ag.pathStatus : "없음")} · 비행 {m.unit.TryGetComponent(out FlyingMover _)}");
        }
        return sb.ToString();
    }

    // 레인 i 중심에서 지상(Sea 제외)으로 **완전 경로(PathComplete)**로 닿는 NavMesh 점들을 전 맵 격자(40)로 찾아, 어느 레인 영역에 속하는지 센다 — 새는 곳이 있으면 다른 레인에 속한 점이 나온다.
    static string Reach()
    {
        var sb = new StringBuilder();
        var lanes = MapLayout.Lanes;
        int sea = NavMesh.GetAreaFromName("Sea");
        int groundMask = ~(1 << (sea >= 0 ? sea : 31)) & NavMesh.AllAreas;
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        foreach (var l in lanes) { minX = Mathf.Min(minX, l.center.x - l.size.x * 0.5f - 100f); maxX = Mathf.Max(maxX, l.center.x + l.size.x * 0.5f + 100f); minZ = Mathf.Min(minZ, l.center.y - l.size.y * 0.5f - MapLayout.LaneApronDepth - 100f); maxZ = Mathf.Max(maxZ, l.center.y + l.size.y * 0.5f + MapLayout.LaneApronDepth + 100f); }
        for (int from = 0; from < lanes.Length; from++)
        {
            NavMesh.SamplePosition(new Vector3(lanes[from].center.x, MapLayout.IslandTop, lanes[from].center.y), out NavMeshHit start, 80f, groundMask);
            var counts = new int[lanes.Length + 1]; int total = 0; var leaks = new System.Collections.Generic.List<string>();
            for (float x = minX; x <= maxX; x += 40f)
                for (float z = minZ; z <= maxZ; z += 40f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, MapLayout.IslandTop, z), out NavMeshHit h, 6f, groundMask)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(start.position, h.position, groundMask, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    total++;
                    int owner = LaneOf(h.position);
                    counts[owner < 0 ? lanes.Length : owner]++;
                    if (owner != from && leaks.Count < 6) leaks.Add($"{h.position:F0}(레인 {owner})");
                }
            sb.Append($"\n   레인{from} 시작 → 완전 경로로 닿는 점 {total}개: " + string.Join(" ", System.Array.ConvertAll(counts, c => c.ToString())) + $" (마지막=레인 밖) · 샌 곳 예: {string.Join(", ", leaks)}");
        }
        return sb.ToString();
    }
}
