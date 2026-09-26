using Fusion;
using UnityEngine;

/// <summary>
/// 판 전체에 하나. 호스트가 러너를 띄우자마자 스폰하고, 씬 전환을 넘어 판 끝까지 산다.
/// 지금(1-a') 싣는 것: 대기실에서 호스트가 고른 난이도 · 판 시작 여부.
/// 1-d에서 라운드·타이머·데스카운트가 여기에 붙는다(설계 §5-2, RoundManager는 안 건드리고 HUD가 여기를 읽는다).
/// </summary>
public class NetGameState : NetworkBehaviour
{
    public const int NoDifficulty = -1;

    public static NetGameState Instance { get; private set; }

    /// <summary>DifficultyMode 값. 아직 안 골랐으면 NoDifficulty.</summary>
    [Networked] public int Difficulty { get; set; }

    [Networked] public NetworkBool Started { get; set; }

    /// <summary>호스트 빌드의 NetCatalog 지문. 클라가 자기 것과 대 본다(다르면 유닛 번호가 어긋난다).</summary>
    [Networked] public int CatalogFingerprint { get; set; }

    // ───── 게임 중(1-d): 호스트가 RoundManager를 읽어 쓴다. 클라 HUD가 이걸 읽는다(RoundManager는 안 건드린다, PM 결정 (b)) ─────
    [Networked] public int Round { get; set; }
    [Networked] public NetworkBool Preparing { get; set; }
    [Networked] public float TimeLeft { get; set; }

    // 스토리 칸·막간 문 표시(2단계 ③) — StoryManager가 호스트에서만 돈다.
    [Networked] public NetworkBool StoryRunning { get; set; }
    [Networked] public NetworkBool StoryWaiting { get; set; }
    [Networked] public NetworkString<_32> StoryLabel { get; set; }
    [Networked] public float StorySeconds { get; set; }
    [Networked] public NetworkString<_16> StoryInterlude { get; set; }

    RoundManager roundManager;
    int notificationsLogged;

    public DifficultyMode? SelectedDifficulty =>
        Difficulty >= 0 && System.Enum.IsDefined(typeof(DifficultyMode), Difficulty) ? (DifficultyMode)Difficulty : (DifficultyMode?)null;

    public override void Spawned()
    {
        Instance = this;
        Runner.MakeDontDestroyOnLoad(gameObject);

        // 호스트: 원격 슬롯 앞으로 온 안내를 그 클라에 넘긴다(자기 것은 자기 화면에 이미 떴다).
        if (HasStateAuthority) PlayerNotification.Shown += RouteNotification;

        if (!HasStateAuthority)
        {
            int mine = NetLauncher.Catalog != null ? NetLauncher.Catalog.Fingerprint : 0;
            Debug.Log($"[MP] 카탈로그 지문: 호스트 {CatalogFingerprint} · 나 {mine}");
            if (mine != CatalogFingerprint && NetLauncher.Instance != null) NetLauncher.Instance.OnBuildMismatch();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        PlayerNotification.Shown -= RouteNotification;
        if (Instance == this) Instance = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Started) return;

        if (roundManager == null) roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager == null) return;

        Round = roundManager.CurrentRound;
        Preparing = roundManager.IsWaitingForNextRound;
        TimeLeft = Preparing ? roundManager.PreRoundTimeLeft : roundManager.RoundTimeLeft;

        StoryManager story = StoryManager.Instance;
        if (story != null)
        {
            StoryRunning = story.HasRunningStory;
            StoryWaiting = story.IsWaiting;
            StoryLabel = Clip(story.StatusLabel, 31);
            StorySeconds = story.SecondsUntilNext;
            StoryInterlude = Clip(story.CurrentInterludeName, 15);
        }
    }

    static string Clip(string text, int max) => string.IsNullOrEmpty(text) ? "" : text.Length > max ? text.Substring(0, max) : text;

    void RouteNotification(int playerId, string message, float duration)
    {
        if (playerId == LocalPlayer.LocalPlayerId) return;
        foreach (NetPlayer player in NetPlayer.All)
        {
            if (player != null && player.Slot == playerId && !player.HasInputAuthority)
            {
                player.RPC_Notify(message, duration);
                if (notificationsLogged++ < 10) Debug.Log($"[MP] 알림 넘김 → 슬롯 {playerId}: {message}");
                return;
            }
        }
    }

    public override void Render()
    {
        if (!HasStateAuthority && Started && StoryManager.Instance != null)
            StoryManager.Instance.ApplyReplicated(StoryRunning, StoryWaiting, StoryLabel.ToString(), StorySeconds, StoryInterlude.ToString());

        // 게임 씬의 DifficultyManager.Awake가 읽는다(호스트·클라 모두). 씬 로드 전에 이미 채워져 있어야 해서
        // 값이 바뀔 때만이 아니라 매 프레임 옮겨 둔다(싼 대입 하나).
        MatchConfig.Difficulty = SelectedDifficulty;
    }

    /// <summary>호스트가 방을 닫기 직전에 모두에게 알린다 — 그냥 끊기면 클라는 「연결 끊김」밖에 모른다.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HostClosing()
    {
        if (NetLauncher.Instance != null) NetLauncher.Instance.OnHostClosing();
    }
}
