using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S2 2.323 「진화체」(j:56160~56222 CR1~CR3, 시작 지급 j:58936). 시작 때 진화 위습 1개를 준다.
/// 진화 포탈에 넣으면 흔함 1기 + 특별함 1기가 「검은 실루엣」으로 서 있고(머리 위 「0 / 152」),
/// 내 레인 몹을 152마리 잡으면 0.02초마다 색이 1%씩 차올라(2초) 진짜 유닛이 된다.
/// 원작 풀은 특별함 32종(j:56110~56150) — 우리는 뽑기표의 특별함 등급 전체에서 뽑는다.
/// 실루엣은 이 PC에서만 보이는 껍데기다(멀티 클라 거울 없음 — 활성화 후 진짜 유닛은 평소대로 보인다).
/// </summary>
public static class S2Evolution
{
    public const int NeededKills = 152;
    const string WispResource = "Wisps/Wisp_진화체";

    class State { public EvolutionBody body; }
    static readonly Dictionary<int, State> states = new Dictionary<int, State>();

    public static WispData Wisp => Resources.Load<WispData>(WispResource);

    /// <summary>
    /// 진화 포탈(원작 rect CH, 일반 위습 포탈 셋 바로 옆)을 맵 재생성 없이 코드로 세운다: 랜덤위습 포탈(유닛랜덤)을 복제해 그 북동쪽 빈 자리(금화 포탈의 x, 유닛랜덤의 z)에 둔다.
    /// 모든 PC가 만든다(트리거 판정은 호스트만 — EvolutionPortal). 이미 있으면 안 만든다.
    /// </summary>
    public static void EnsurePortal()
    {
        if (GameObject.Find("Portal_진화체") != null) return;
        GameObject north = GameObject.Find("Portal_유닛랜덤"), east = GameObject.Find("Portal_금화랜덤");
        if (north == null) { Debug.LogWarning("[진화체] Portal_유닛랜덤을 못 찾아 진화 포탈을 못 세웁니다."); return; }
        GameObject clone = Object.Instantiate(north, north.transform.parent);
        clone.name = "Portal_진화체";
        foreach (UnitPortal old in clone.GetComponents<UnitPortal>()) Object.Destroy(old);
        Vector3 p = north.transform.position;
        if (S2Anchors.TryIsland("OroSeongIsland", out Vector3 ic, out Vector2 isz)) { p = S2Anchors.OnGround(new Vector3(ic.x, ic.y, ic.z - isz.y * 0.28f)); }   // 오로성 섬 가운데 남쪽(실루엣과 안 겹치게)
        else if (east != null) p.x = east.transform.position.x;
        else p.x += north.transform.lossyScale.x * 2f;
        clone.transform.position = p;
        var tint = new MaterialPropertyBlock();
        foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(true)) { r.GetPropertyBlock(tint); tint.SetColor("_BaseColor", new Color(0.75f, 0.45f, 1f, 1f)); r.SetPropertyBlock(tint); }
        clone.AddComponent<EvolutionPortal>();
        GameObject field = Resources.Load<GameObject>("S2/KineticField");   // 원작 kineticfield 바닥(blender) — 있으면 포탈 자리에 깐다
        if (field != null) { GameObject f = Object.Instantiate(field, clone.transform.parent); f.name = "진화체 필드"; f.transform.position = p + Vector3.up * 0.4f; }
        var label = new GameObject("진화체 글자");
        label.transform.SetParent(clone.transform, false);
        label.transform.localPosition = new Vector3(0f, 3f, 0f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = "진화체"; tm.anchor = TextAnchor.MiddleCenter; tm.characterSize = 1.2f; tm.fontSize = 48; tm.color = new Color(0.8f, 0.6f, 1f);
        label.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, clone.transform.localScale.x), 1f / Mathf.Max(0.01f, clone.transform.localScale.y), 1f / Mathf.Max(0.01f, clone.transform.localScale.z));
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 위에서 내려다보는 카메라에 읽히게 바닥에 눕힌다
        Debug.Log($"[진화체] 포탈을 세웠습니다: {p}");
    }

    /// <summary>RewardDistributor.Start: 시작 위습 지급(호스트).</summary>
    public static void GrantStarting(RewardDistributor distributor)
    {
        WispData wisp = Wisp;
        if (wisp == null || distributor == null) return;
        foreach (PlayerContext context in PlayerContext.Occupied)
            distributor.GrantWisps(context, new List<WispReward> { new WispReward { wisp = wisp, count = 1 } });
    }

    /// <summary>EvolutionPortal이 진화 위습을 먹었을 때(호스트).</summary>
    public static bool OnWispUsed(int ownerId, Wisp wisp, GachaTable table, UnitSpawner spawner)
    {
        if (table == null || spawner == null) return false;
        if (states.TryGetValue(ownerId, out State old) && old.body != null)
        {
            S2Notify.Show(ownerId, "이미 진화체가 자라는 중입니다.", 4f);
            return false;
        }

        UnitData common = table.RollFromGrade(UnitGrade.Common);
        UnitData special = table.RollFromGrade(UnitGrade.Special);
        if (special == null || special.prefab == null) return false;

        LaneMarker lane = LaneMarker.Get(ownerId);
        Vector3 at = lane != null ? lane.LaneCenter : wisp.transform.position;
        if (common != null && common.prefab != null)
            spawner.Spawn(common, lane != null ? lane.TakeSpawnPosition(common) : at, ownerId);

        GameObject silhouette = Object.Instantiate(special.prefab, at, Quaternion.identity);
        EvolutionBody body = EvolutionBody.Strip(silhouette);
        body.Init(ownerId, special, spawner);
        body.SpecialUnit = special;
        states[ownerId] = new State { body = body };

        S2Notify.Show(ownerId, $"<color=#9553EC>[진화체]</color> {special.unitName} 을(를) 획득했습니다.", 5f);
        S2Notify.Show(ownerId, $"<color=#FF0000>◈</color> 라인몹을 처치하고 <color=#9553EC>경험치</color>를 획득하면 유닛이 활성화됩니다!", 5f);
        return true;
    }

    /// <summary>RewardDistributor.GrantKillReward: 레인 주인의 라인몹 처치 하나(호스트).</summary>
    public static void OnLaneKill(int ownerId)
    {
        if (states.TryGetValue(ownerId, out State s) && s.body != null) s.body.AddKill();
    }

    public static void Reset()
    {
        states.Clear();
        foreach (EvolutionBody b in clientBodies.Values) if (b != null) Object.Destroy(b.gameObject);
        clientBodies.Clear();
    }

    // ---- 멀티 복제(NetGameState) ----
    /// <summary>호스트: 이 슬롯 진화체의 지금 킬 수(없으면 −1).</summary>
    public static int HostKills(int slot) => states.TryGetValue(slot, out State s) && s.body != null ? s.body.Kills : -1;

    /// <summary>호스트: 이 슬롯 진화체가 깨어날 특별함 유닛의 NetCatalog 번호(없으면 −1).</summary>
    public static int HostUnitIndex(int slot) =>
        states.TryGetValue(slot, out State s) && s.body != null && s.body.SpecialUnit != null && NetLauncher.Catalog != null ? NetLauncher.Catalog.IndexOf(s.body.SpecialUnit) : -1;

    static readonly Dictionary<int, EvolutionBody> clientBodies = new Dictionary<int, EvolutionBody>();

    /// <summary>클라(호스트 아님): 복제된 슬롯별 값으로 같은 실루엣을 세우고 킬 수·색 차오름을 따라간다. 실물 유닛은 호스트가 만들어 거울로 온다.</summary>
    public static void ClientSync(int slot, int kills, int unitIndex)
    {
        clientBodies.TryGetValue(slot, out EvolutionBody body);
        if (unitIndex < 0 || kills < 0)   // 호스트 쪽 진화체가 없어졌다(깨어남 끝)
        {
            if (body != null) { clientBodies.Remove(slot); body.SetKills(S2Evolution.NeededKills); }   // 아직 안 찼으면 지금 채워 사라지게(채우는 중이면 그대로)
            return;
        }
        if (body == null)
        {
            NetCatalog cat = NetLauncher.Catalog;
            if (cat == null || unitIndex >= cat.units.Count || cat.units[unitIndex] == null || cat.units[unitIndex].prefab == null) return;
            LaneMarker lane = LaneMarker.Get(slot);
            Vector3 at = lane != null ? lane.LaneCenter : Vector3.zero;
            GameObject go = Object.Instantiate(cat.units[unitIndex].prefab, at, Quaternion.identity);
            body = EvolutionBody.Strip(go);
            body.Init(slot, cat.units[unitIndex], null);
            clientBodies[slot] = body;
        }
        body.SetKills(kills);
    }

    /// <summary>판이 새로 시작될 때(씬 로드 직전) 이 판 한정 상태를 모두 지운다 — S2 기능 다섯의 static 상태가 새 판에 남아 「이미 사용했습니다」가 뜨지 않게(10-17 파칭코 두 번째 판 버그와 같은 꼴).</summary>
    public static void ResetAllMatchState()
    {
        Reset();
        S2Fortune.ResetMatch();
        S2DebeBack.ResetMatch();
        S2Dango.ResetMatch();
        S2Treasure.ResetMatch();
        OroSeong.ResetMatch();
        S2Objectives.ResetMatch();
        GameModes.ResetMatch();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterReset()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        ResetAllMatchState();
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) ResetAllMatchState();
    }
}

/// <summary>검은 실루엣 껍데기: 프리팹에서 게임 컴포넌트를 모두 떼고 모델만 남겨 까맣게 칠한 뒤 「N / 152」를 머리 위에 띄운다.</summary>
public class EvolutionBody : MonoBehaviour
{
    int ownerId, kills;
    public int Kills => kills;
    public UnitData SpecialUnit { get; set; }
    bool filling;
    float fill;
    UnitData unit;
    UnitSpawner spawner;
    TextMesh label;
    S2Silhouette sil;

    public static EvolutionBody Strip(GameObject root)
    {
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null || c is Transform || c is Renderer || c is MeshFilter || c is SkinnedMeshRenderer || c is Animator) continue;
            if (c is Behaviour b) b.enabled = false;
            if (c is Collider col) col.enabled = false;
        }
        foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        root.name = "진화체 실루엣";
        return root.AddComponent<EvolutionBody>();
    }

    public void Init(int owner, UnitData data, UnitSpawner unitSpawner)
    {
        ownerId = owner; unit = data; spawner = unitSpawner;
        sil = new S2Silhouette(GetComponentsInChildren<Renderer>(true));
        Paint(0f);

        var go = new GameObject("진화체 글자");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 40f, 0f);
        label = go.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.characterSize = 3f;
        label.fontSize = 48;
        label.color = new Color32(139, 189, 255, 255);
        RefreshLabel();
    }

    void LateUpdate()
    {
        if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
    }

    void RefreshLabel() { if (label != null) label.text = $"{kills} / {S2Evolution.NeededKills}"; }

    void Paint(float t) => sil.Paint(t);

    public void AddKill() => SetKills(kills + 1);

    /// <summary>킬 수를 정한다(호스트는 +1씩, 클라는 복제값으로). 다 차면 색이 차오른다.</summary>
    public void SetKills(int value)
    {
        if (filling) return;
        kills = Mathf.Max(kills, value);
        RefreshLabel();
        if (kills >= S2Evolution.NeededKills) filling = true;
    }

    void Update()
    {
        if (!filling) return;
        fill += Time.deltaTime / 2f;   // 원작 0.02초마다 1% = 2초
        Paint(Mathf.Clamp01(fill));
        if (fill < 1f) return;
        filling = false;
        Vector3 at = transform.position;
        if (spawner != null) spawner.Spawn(unit, at, ownerId);   // 클라(spawner 없음)는 실루엣만 지운다 — 실물은 호스트가 만든 거울
        if (spawner != null) S2Notify.Show(ownerId, "<color=#04FFC4>[알림]</color> 진화체 경험치 획득 완료! 유닛이 활성화됩니다.", 6f);
        Destroy(gameObject);
    }
}
