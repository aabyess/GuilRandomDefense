using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// S2 2.323 맵 목표물 둘(j:14582~14584 파괴물 생성, w3b B005·B008): 7시 구석 「샨도라 황금 종」(체력 650만, 모델 D\modelPR (10)) ·
/// 1시 구석 「룸바 해적단의 영혼」(체력 1만, SmallLight + 엘리베이터 장식 3). 부수면 S2Objectives 이벤트를 낸다(보상·표시는 구현담당2 미션판).
/// 맵 재생성 없이 판 시작 때 코드로 세운다 — 원작 좌표(−9152,−10432)·(9280,6528)는 우리 맵이 재배치라 그대로 못 쓴다. 레인 유닛은 스토리존 포탈로만 다른 섬에
/// 가므로(StoryZonePortal) **스토리존 섬**(NavMesh 덩어리)의 7시(좌하)·1시(우상) 모서리 안쪽에 놓는다(스토리존을 못 찾으면 레인이 아닌 섬 중 가장 구석 섬). 모델은 원작 모델이 Resources/S2에 들어오면 교체(지금은 스토리 건물 임시 모델).
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
        if (!S2Features.Enabled) { return; }
        if (!GameAuthority.IsServer || Instance != null) return;
        new GameObject("S2MapObjectives").AddComponent<S2MapObjectives>();
    }

    void OnEnable() { Instance = this; EnemyDummy.OnAnyEnemyDied += OnEnemyDied; }
    void OnDisable() { if (Instance == this) Instance = null; EnemyDummy.OnAnyEnemyDied -= OnEnemyDied; }

    // 목표물이 죽음 확정된 순간 — 마지막으로 때린 플레이어(모르면 −1)와 함께 이벤트를 낸다.
    void OnEnemyDied(EnemyDummy dead)
    {
        if (!GameAuthority.IsServer || dead == null) return;
        if (dead == bell) { bell = null; S2Objectives.RaiseGoldenBellDestroyed(dead.LastHitPlayer); }
        else if (dead == soul) { soul = null; S2Objectives.RaiseRumbarSoulFreed(dead.LastHitPlayer); }
    }

    System.Collections.IEnumerator Start()
    {
        yield return new WaitForSeconds(3f);   // NavMesh·레인 준비 뒤
        if (!BuildPlan(out Vector3 swSpot, out Vector3 neSpot)) yield break;
        bell = SpawnObjective("샨도라 황금 종", BellHp, 24f, swSpot, "S2/Enemy_S2_GoldenBell");
        soul = SpawnObjective("룸바 해적단의 영혼", SoulHp, 12f, neSpot, "S2/Enemy_S2_RumbaSoul");
        SpawnElevators(neSpot);
        BellPosition = swSpot; SoulPosition = neSpot;
        Debug.Log($"[S2 목표물] 황금 종 {swSpot} · 영혼 {neSpot}");
    }

    /// <summary>「-황금종」 채팅 — 황금 종 체력 %.</summary>
    public static string BellHpText()
    {
        if (!S2Features.Enabled) { return "S2 기능이 꺼져 있습니다."; }
        EnemyDummy b = Instance != null ? Instance.bell : null;
        return b == null ? "황금 종이 없습니다(이미 부서졌거나 아직 안 세워짐)." : $"황금 종 체력 {b.HpRatio * 100f:0.#}%";
    }

    // ---- 자리 찾기 ----
    // NavMesh는 바다까지 한 덩어리라(10-18 실측: 군집 경계가 맵 전체) 섬 모서리를 NavMesh에서 못 읽는다. 스토리존 섬은 MapLayout.StoryZone = 180×150 × WorldScale(750.1×625.1)이고
    // 중심은 StoryManager의 스토리 건물 자리다 — 그 섬의 7시·1시 모서리에서 Inset 안쪽에 놓는다(사거리 있는 유닛이 가장자리에서 못 닿지 않게).
    const float StoryZoneHalfX = 180f * WorldScale.Value * 0.5f, StoryZoneHalfZ = 150f * WorldScale.Value * 0.5f, Inset = 70f;

    bool BuildPlan(out Vector3 sw, out Vector3 ne)
    {
        sw = ne = default;
        StoryManager story = Object.FindFirstObjectByType<StoryManager>();
        if (story == null) { Debug.LogWarning("[S2 목표물] StoryManager가 없어 스토리존 자리를 못 압니다."); return false; }
        Vector3 c = story.SpawnPosition;
        sw = Snap(new Vector3(c.x - StoryZoneHalfX + Inset, c.y, c.z - StoryZoneHalfZ + Inset));
        ne = Snap(new Vector3(c.x + StoryZoneHalfX - Inset, c.y, c.z + StoryZoneHalfZ - Inset));
        return true;
    }

    static Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 40f, NavMesh.AllAreas) ? hit.position : p;

    // 원작 룸바 영혼(9280,6528) 둘레 엘리베이터 장식 3개 — 상대 위치 (−192,0)·(0,−192)·(−64,−128) ÷ 4.167. 모델(ElevatorDisc)이 Resources/S2에 있을 때만.
    static void SpawnElevators(Vector3 soulSpot)
    {
        GameObject prefab = Resources.Load<GameObject>("S2/ElevatorDisc");
        if (prefab == null) return;
        foreach (Vector2 off in new[] { new Vector2(-192f, 0f), new Vector2(0f, -192f), new Vector2(-64f, -128f) })
        {
            GameObject g = Instantiate(prefab, soulSpot + new Vector3(off.x, 0.3f, off.y) / WorldScale.Value, Quaternion.identity);
            g.name = "S2엘리베이터";
        }
    }

    // enemyAsset: 에셋으로 있는 적 데이터(멀티 카탈로그 대상). 없으면 템플릿을 런타임 복제(클라엔 안 보임).
    EnemyDummy SpawnObjective(string label, float hp, float scale, Vector3 at, string enemyAsset)
    {
        EnemyData data = Resources.Load<EnemyData>(enemyAsset);
        if (data == null)
        {
            EnemyData template = Resources.Load<EnemyData>(TemplateEnemy);
            if (template == null || template.prefab == null) { Debug.LogWarning("[S2 목표물] 템플릿 적 자산이 없습니다."); return null; }
            data = Instantiate(template);
            data.name = "S2_" + label; data.enemyName = label; data.hp = hp; data.armor = 0f; data.hpRegenPerSecond = 0f;
            data.isBoss = false; data.goldReward = 0; data.visualScale = scale; data.moveSpeed = 0f;
            Debug.LogWarning($"[S2 목표물] {enemyAsset} 자산이 없어 런타임 복제를 씁니다(멀티 클라에 안 보임).");
        }
        GameObject go = Instantiate(data.prefab, at, Quaternion.identity);
        go.name = "S2목표물_" + label;
        if (!go.TryGetComponent(out EnemyDummy dummy)) { Destroy(go); return null; }
        dummy.Initialize(data);
        dummy.EnableContributionTracking();
        dummy.SetLane(-1);
        if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        return dummy;
    }
}

