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
    [Networked] public byte ExtraTimerKind { get; set; }
    /// <summary>레인당 유닛 수 패배 한계(원작 udg_ModeEnemyInt) — 클라 팀 현황판 제목 「유닛 카운트 = N <- 패배」용. 0 = 모름.</summary>
    [Networked] public int DeathLimit { get; set; }
    /// <summary>제한시간 스토리(원작 와노쿠니) 남은 초, 없으면 -1 · 그 스토리 이름 — HUD 「{이름} 남은 시간:」.</summary>
    [Networked] public float StoryLimitLeft { get; set; }
    [Networked] public NetworkString<_32> StoryLimitName { get; set; }
    [Networked] public float ExtraTimerLeft { get; set; }

    /// <summary>판 도중 원작 Gone으로 나간 슬롯(비트). 재접속한 사람 화면은 그 사람 NetPlayer를 못 봐 좌석을 몰라
    /// 「비어있음」으로 떴다 — 이걸로 앉히고 사망 표식을 건다(PM 09-26 ③).</summary>
    [Networked] public int DepartedMask { get; set; }

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
    [Networked] public int StoryFinished { get; set; }
    /// <summary>풀카운트(원작 멀티보드 3번째 행) — 호스트가 판정한 점수를 클라 점수판에 보인다. 신세계 진입 전엔 숨김.</summary>
    [Networked, Capacity(4)] public NetworkArray<int> FullCounts => default;
    [Networked] public NetworkBool FullCountVisible { get; set; }

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
        if (HasStateAuthority) KillGoldPopup.Shown += RouteKillGold;
        if (HasStateAuthority) GameSound.RemoteRouted += RouteSound;
        if (HasStateAuthority) GameSound.Broadcast += RouteGameSound;
        if (HasStateAuthority) SummonVoice.Broadcast += RouteSummonVoice;
        if (HasStateAuthority) SkillSfx.Broadcast += RouteSkillSfx;
        if (HasStateAuthority) { SkillVfx.Played += RouteVfx; SkillVfx.PlayedPrefab += RoutePrefabVfx; }

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
        KillGoldPopup.Shown -= RouteKillGold;
        GameSound.RemoteRouted -= RouteSound;
        GameSound.Broadcast -= RouteGameSound;
        SummonVoice.Broadcast -= RouteSummonVoice;
        SkillSfx.Broadcast -= RouteSkillSfx;
        SkillVfx.Played -= RouteVfx;
        SkillVfx.PlayedPrefab -= RoutePrefabVfx;
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
        // 보스 제한시간·신세계 대기 타이머(알림 묶음 3). 0 = 없음, 1 = 보스, 2 = 60라운드 신세계 대기.
        if (roundManager.TryGetExtraTimer(out bool newWorldWait, out float extraSeconds))
        {
            ExtraTimerKind = (byte)(newWorldWait ? 2 : 1);
            ExtraTimerLeft = extraSeconds;
        }
        else if (ExtraTimerKind != 0) ExtraTimerKind = 0;
        if (DeathLimit != roundManager.EnemyCountLimit) DeathLimit = roundManager.EnemyCountLimit;
        if (FullCountVisible != FullCountScore.HostVisible) FullCountVisible = FullCountScore.HostVisible;
        for (int i = 0; i < FullCountScore.Players; i++)
            if (FullCounts.Get(i) != FullCountScore.HostGet(i)) FullCounts.Set(i, FullCountScore.HostGet(i));

        StoryManager story = StoryManager.Instance;
        if (story != null)
        {
            StoryRunning = story.HasRunningStory;
            StoryWaiting = story.IsWaiting;
            StoryLabel = Clip(story.StatusLabel, 31);
            StorySeconds = story.SecondsUntilNext;
            StoryLimitLeft = story.SecondsLeftInLimit;
            string limitName = story.SecondsLeftInLimit >= 0f && story.Running != null ? Clip(StoryManager.DisplayName(story.Running), 24) : "";
            if (StoryLimitName.ToString() != limitName) StoryLimitName = limitName;
            StoryInterlude = Clip(story.CurrentInterludeName, 15);
            StoryFinished = story.FinishedCount;
        }
    }

    static string Clip(string text, int max) => string.IsNullOrEmpty(text) ? "" : text.Length > max ? text.Substring(0, max) : text;

    static int soundsLogged;

    // 친구 앞으로 난 소리(원작 GetLocalPlayer()==주인일 때만)는 그 친구 PC에서만 — 방장 PC에선 GameSound.PlayFor가 이미 안 냈다.
    void RouteSound(int playerId, GameSoundId id)
    {
        foreach (NetPlayer player in NetPlayer.All)
            if (player != null && player.Slot == playerId && !player.HasInputAuthority)
            {
                player.RPC_PlaySound((byte)id);
                if (soundsLogged++ < 10) Debug.Log($"[MP] 소리 넘김 → 슬롯 {playerId}: {id}(방장 PC에선 안 냄)");
                return;
            }
    }

    void RouteKillGold(int playerId, Vector3 worldPos, int amount, bool wood)
    {
        if (playerId == LocalPlayer.LocalPlayerId) return;   // 호스트 자신의 것은 KillGoldPopup.Show가 이미 그렸다
        foreach (NetPlayer player in NetPlayer.All)
            if (player != null && player.Slot == playerId && !player.HasInputAuthority)
            {
                player.RPC_KillGold(worldPos, amount, wood);
                return;
            }
    }

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
        {
            StoryManager.Instance.ApplyReplicated(StoryRunning, StoryWaiting, StoryLabel.ToString(), StorySeconds, StoryInterlude.ToString());
            StoryManager.Instance.ApplyReplicatedFinished(StoryFinished);
        }

        // 게임 씬의 DifficultyManager.Awake가 읽는다(호스트·클라 모두). 씬 로드 전에 이미 채워져 있어야 해서
        // 값이 바뀔 때만이 아니라 매 프레임 옮겨 둔다(싼 대입 하나).
        MatchConfig.Difficulty = SelectedDifficulty;

        if (!HasStateAuthority && DepartedMask != 0)
            for (int slot = 0; slot < NetSession.MaxSlots; slot++)
            {
                if ((DepartedMask & (1 << slot)) == 0) continue;
                PlayerContext gone = PlayerContext.Get(slot);
                if (gone == null) continue;
                if (!gone.IsOccupied) gone.SetOccupied(true);
                if (!gone.IsDead) gone.MarkDead();
            }

        // 재접속: 게임 씬이 이 오브젝트보다 먼저 떠서 DifficultyManager.Awake가 난이도를 못 봤으면 지금 건다
        // (안 걸면 클라가 「방장이 모드를 선택하고 있습니다」 창에 멈춘다).
        if (!HasStateAuthority && Started && SelectedDifficulty.HasValue
            && DifficultyManager.Instance != null && !DifficultyManager.Instance.IsModeSelected)
            DifficultyManager.Instance.ApplyReplicatedMode(SelectedDifficulty.Value);
    }

    /// <summary>호스트가 방을 닫기 직전에 모두에게 알린다 — 그냥 끊기면 클라는 「연결 끊김」밖에 모른다.</summary>
    // 스킬 이펙트(적중·마법 적중·방깎·버프) — 호스트에서 한 번 터진 것을 친구 화면에도(09-29 PM). 스턴·이감처럼 붙어 있는 것은
    // NetEntity의 상태 플래그로 따로 간다. 이펙트는 놓쳐도 되는 것이라 비신뢰 채널 — 밀린 패킷을 다시 보내느라 늦게 터지는 것보다 낫다.
    // 상위 등급 획득 음성(09-30) — 원작은 GetLocalPlayer 검사 없이 StartSound라 전원에게 난다. 방장 PC는 SummonVoice가 이미 냈다.
    void RouteSummonVoice(int clipIndex) => RPC_SummonVoice((short)clipIndex);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_SummonVoice(short clipIndex)
    {
        SummonVoice.Play(clipIndex);
    }

    // 스킬 효과음(09-30) — 표 번호(SkillSfxTable.txt의 C줄 순서)·볼륨 배수·자리. 거리 감쇠는 받는 쪽 화면 기준으로 SkillSfx가 센다.
    // 놓쳐도 되는 것이라 이펙트와 같은 비신뢰 채널.
    void RouteSkillSfx(int clip, float volume, Vector3 position) => RPC_SkillSfx((short)clip, (byte)Mathf.RoundToInt(Mathf.Clamp01(volume) * 255f), position);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
    public void RPC_SkillSfx(short clip, byte volume, Vector3 position)
    {
        SkillSfx.Play(clip, volume / 255f, position);
    }

    // 전원 효과음(10-06 — 평타 적중·처치·라운드 시작·보스 등장). 판정(UnitAttacker·EnemyDummy·RoundManager)은 호스트에서만 돌아서
    // 클라엔 그 순간이 없다 — 호스트 GameSound.PlayAll이 연타 제한(평타 초당 12 · 처치 초당 8)을 통과한 것만 여기로 온다.
    // 방장 PC는 이미 냈다. 놓쳐도 되는 것이라 비신뢰 채널(스킬 효과음과 같다).
    void RouteGameSound(GameSoundId id) => RPC_GameSound((byte)id);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
    public void RPC_GameSound(byte id)
    {
        GameSound.PlayBroadcast((GameSoundId)id);
    }

    void RouteVfx(SkillVfx.Kind kind, Vector3 position) => RPC_SkillVfx((byte)kind, position);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
    public void RPC_SkillVfx(byte kind, Vector3 position)
    {
        ReceivedVfx++;
        SkillVfx.Burst((SkillVfx.Kind)kind, position);
    }

    // 스킬별 팩 이펙트(09-30) — 표 번호(SkillVfxTable.prefabs)·위치·지름·땅 여부. 양쪽이 같은 표를 빌드에 싣고 있다.
    void RoutePrefabVfx(int index, Vector3 position, float diameter, bool ground) => RPC_SkillPrefabVfx((short)index, position, diameter, ground);

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.Unreliable)]
    public void RPC_SkillPrefabVfx(short index, Vector3 position, float diameter, NetworkBool ground)
    {
        ReceivedVfx++;
        SkillVfx.PlayPrefab(index, position, diameter, ground, notify: false);
    }

    /// <summary>클라가 받은 한 번짜리 이펙트 수(두 창 확인용 로그).</summary>
    public static int ReceivedVfx;

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HostClosing()
    {
        if (NetLauncher.Instance != null) NetLauncher.Instance.OnHostClosing();
    }
}
