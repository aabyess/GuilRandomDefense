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

    public static void Reset() => states.Clear();

    /// <summary>판이 새로 시작될 때(씬 로드 직전) 이 판 한정 상태를 모두 지운다 — S2 기능 다섯의 static 상태가 새 판에 남아 「이미 사용했습니다」가 뜨지 않게(10-17 파칭코 두 번째 판 버그와 같은 꼴).</summary>
    public static void ResetAllMatchState()
    {
        Reset();
        S2Fortune.ResetMatch();
        S2DebeBack.ResetMatch();
        S2Dango.ResetMatch();
        S2Treasure.ResetMatch();
        OroSeong.ResetMatch();
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

    public void AddKill()
    {
        if (filling) return;
        kills++;
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
        spawner.Spawn(unit, at, ownerId);
        S2Notify.Show(ownerId, "<color=#04FFC4>[알림]</color> 진화체 경험치 획득 완료! 유닛이 활성화됩니다.", 6f);
        Destroy(gameObject);
    }
}
