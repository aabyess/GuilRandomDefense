using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// S2 2.323 맵 목표물 둘(j:14582~14584 파괴물 생성, w3b B005·B008): 7시 구석 「샨도라 황금 종」(체력 650만, 모델 D\modelPR (10)) ·
/// 1시 구석 「룸바 해적단의 영혼」(체력 1만, SmallLight + 엘리베이터 장식 3). 부수면 S2Objectives 이벤트를 낸다(보상·표시는 구현담당2 미션판).
/// 맵 재생성 없이 판 시작 때 코드로 세운다 — 원작 좌표(−9152,−10432)·(9280,6528)는 우리 맵이 재배치라 그대로 못 쓰고, **NavMesh 섬 덩어리**를
/// 찾아 레인(플레이어 필드)이 아닌 섬 중 가장 7시(좌하)·1시(우상)에 있는 섬의 그 모서리 안쪽에 놓는다. 모델은 원작 모델이 Resources/S2에 들어오면 교체(지금은 스토리 건물 임시 모델).
/// 호스트만 세운다. 체력은 원작 값 그대로(w3b bhps).
/// </summary>
public class S2MapObjectives : MonoBehaviour
{
    public const float BellHp = 6_500_000f, SoulHp = 10_000f;
    const string TemplateEnemy = "S2/Enemy_Story14_에그헤드";   // 임시: 스토리 건물과 같은 프리팹

    public static S2MapObjectives Instance { get; private set; }
    EnemyDummy bell, soul;
    public Vector3 BellPosition { get; private set; }
    public Vector3 SoulPosition { get; private set; }

    public static void Install()
    {
        if (!GameAuthority.IsServer || Instance != null) return;
        new GameObject("S2MapObjectives").AddComponent<S2MapObjectives>();
    }

    void OnEnable() => Instance = this;
    void OnDisable() { if (Instance == this) Instance = null; }

    System.Collections.IEnumerator Start()
    {
        yield return new WaitForSeconds(3f);   // NavMesh·레인 준비 뒤
        if (!BuildPlan(out Vector3 swSpot, out Vector3 neSpot)) yield break;
        bell = SpawnObjective("샨도라 황금 종", BellHp, 24f, swSpot, S2ObjectiveKind.Bell);
        soul = SpawnObjective("룸바 해적단의 영혼", SoulHp, 12f, neSpot, S2ObjectiveKind.Soul);
        BellPosition = swSpot; SoulPosition = neSpot;
        Debug.Log($"[S2 목표물] 황금 종 {swSpot} · 영혼 {neSpot}");
    }

    /// <summary>「-황금종」 채팅 — 황금 종 체력 %.</summary>
    public static string BellHpText()
    {
        EnemyDummy b = Instance != null ? Instance.bell : null;
        return b == null ? "황금 종이 없습니다(이미 부서졌거나 아직 안 세워짐)." : $"황금 종 체력 {b.HpRatio * 100f:0.#}%";
    }

    // ---- 자리 찾기 ----
    class Cluster { public Bounds bounds; public List<Vector3> verts = new List<Vector3>(); public int id; }

    bool BuildPlan(out Vector3 sw, out Vector3 ne)
    {
        sw = ne = default;
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0) { Debug.LogWarning("[S2 목표물] NavMesh가 비어 있어 못 세웁니다."); return false; }

        // 같은 자리 정점을 하나로(삼각형 사이가 이어지게) → 이어진 삼각형끼리 한 덩어리
        var key = new Dictionary<Vector3Int, int>();
        var pos = new List<Vector3>();
        int Id(Vector3 v) { var k = new Vector3Int(Mathf.RoundToInt(v.x * 2f), Mathf.RoundToInt(v.y * 2f), Mathf.RoundToInt(v.z * 2f)); if (!key.TryGetValue(k, out int i)) { i = pos.Count; key[k] = i; pos.Add(v); } return i; }
        int[] map = new int[tri.vertices.Length];
        for (int i = 0; i < map.Length; i++) map[i] = Id(tri.vertices[i]);
        int[] parent = new int[pos.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int t = 0; t + 2 < tri.indices.Length; t += 3)
        {
            int a = Find(map[tri.indices[t]]), b = Find(map[tri.indices[t + 1]]), c = Find(map[tri.indices[t + 2]]);
            parent[b] = a; parent[Find(c)] = a;
        }
        var clusters = new Dictionary<int, Cluster>();
        for (int i = 0; i < pos.Count; i++)
        {
            int r = Find(i);
            if (!clusters.TryGetValue(r, out Cluster cl)) { cl = new Cluster { id = r, bounds = new Bounds(pos[i], Vector3.zero) }; clusters[r] = cl; }
            cl.verts.Add(pos[i]); cl.bounds.Encapsulate(pos[i]);
        }

        // 플레이어 레인이 든 덩어리·너무 작은 덩어리는 뺀다
        var laneRoots = new HashSet<int>();
        foreach (PlayerContext c in PlayerContext.All)
        {
            LaneMarker lane = c != null ? LaneMarker.Get(c.PlayerId) : null;
            if (lane == null) continue;
            foreach (Cluster cl in clusters.Values) if (cl.bounds.Contains(new Vector3(lane.LaneCenter.x, cl.bounds.center.y, lane.LaneCenter.z))) laneRoots.Add(cl.id);
        }
        var cand = new List<Cluster>();
        foreach (Cluster cl in clusters.Values)
            if (!laneRoots.Contains(cl.id) && cl.bounds.size.x * cl.bounds.size.z > 4000f) cand.Add(cl);
        if (cand.Count == 0) { Debug.LogWarning("[S2 목표물] 레인이 아닌 섬을 못 찾았습니다."); return false; }

        Cluster swC = null, neC = null;
        foreach (Cluster cl in cand)
        {
            float s = cl.bounds.center.x + cl.bounds.center.z;
            if (swC == null || s < swC.bounds.center.x + swC.bounds.center.z) swC = cl;
            if (neC == null || s > neC.bounds.center.x + neC.bounds.center.z) neC = cl;
        }
        sw = CornerSpot(swC, -1f);
        ne = CornerSpot(neC, +1f);
        return true;
    }

    // 덩어리에서 방향(−1=7시 · +1=1시) 끝에 가장 가까운 정점을 안쪽(중심 쪽)으로 조금 당기고 NavMesh에 붙인다.
    static Vector3 CornerSpot(Cluster cl, float dir)
    {
        Vector3 best = cl.verts[0]; float bestScore = float.NegativeInfinity;
        foreach (Vector3 v in cl.verts) { float s = dir * (v.x + v.z); if (s > bestScore) { bestScore = s; best = v; } }
        Vector3 toCenter = cl.bounds.center - best; toCenter.y = 0f;
        float inset = Mathf.Min(40f, toCenter.magnitude * 0.5f);
        Vector3 p = best + toCenter.normalized * inset;
        return NavMesh.SamplePosition(p, out NavMeshHit hit, 60f, NavMesh.AllAreas) ? hit.position : best;
    }

    EnemyDummy SpawnObjective(string label, float hp, float scale, Vector3 at, S2ObjectiveKind kind)
    {
        EnemyData template = Resources.Load<EnemyData>(TemplateEnemy);
        if (template == null || template.prefab == null) { Debug.LogWarning("[S2 목표물] 템플릿 적 자산이 없습니다."); return null; }
        EnemyData data = Instantiate(template);
        data.name = "S2_" + label; data.enemyName = label; data.hp = hp; data.armor = 0f; data.hpRegenPerSecond = 0f;
        data.isBoss = false; data.goldReward = 0; data.visualScale = scale; data.moveSpeed = 0f;
        GameObject go = Instantiate(template.prefab, at, Quaternion.identity);
        go.name = "S2목표물_" + label;
        if (!go.TryGetComponent(out EnemyDummy dummy)) { Destroy(go); return null; }
        dummy.Initialize(data);
        dummy.EnableContributionTracking();
        dummy.SetLane(-1);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        go.AddComponent<S2ObjectiveWatch>().Init(dummy, kind);
        return dummy;
    }
}

public enum S2ObjectiveKind { Bell, Soul }

/// <summary>목표물 체력이 0이 돼 파괴되면(EnemyDummy는 죽을 때 GameObject를 지운다) 마지막으로 때린 플레이어와 함께 S2Objectives 이벤트를 낸다.</summary>
public class S2ObjectiveWatch : MonoBehaviour
{
    EnemyDummy dummy; S2ObjectiveKind kind; int lastHit = -1;
    public void Init(EnemyDummy d, S2ObjectiveKind k) { dummy = d; kind = k; }
    void Update() { if (dummy != null) lastHit = dummy.LastHitPlayer; }
    void OnDestroy()
    {
        if (dummy != null || !GameAuthority.IsServer || !gameObject.scene.isLoaded) return;   // 씬이 내려가며 지워진 건 파괴가 아니다
        if (kind == S2ObjectiveKind.Bell) S2Objectives.RaiseGoldenBellDestroyed(lastHit);
        else S2Objectives.RaiseRumbarSoulFreed(lastHit);
    }
}
