using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetBoot 씬의 접속 창구(로직). 화면은 NetLobbyUi가 그린다.
///   [방 만들기] → 방 코드 발급 → 대기실(닉네임·슬롯·준비·호스트 난이도) → 전원 준비 → 호스트 [시작] → 게임 씬
///   [참가] ← 방 코드
///
/// ⚠️ 게임 씬(SampleScene)을 직접 Play하면 이 오브젝트가 없다 → 러너 없음 → GameAuthority.Provider null
///    → 지금과 똑같은 싱글. 멀티 설계의 제1 조건이다(gameshot·ClaudeBridge·판 측정 무영향).
///
/// 명령줄(빌드 두 개를 사람 손 없이 붙여 보는 용도):
///   -mpHost | -mpJoin        바로 방 만들기/참가
///   -mpSession 코드           방 코드(호스트는 발급 대신 이 코드로 연다)
///   -mpRegion 지역            Photon 지역(기본 kr) — 방장과 친구가 같아야 서로 보인다
///   -mpNick 이름              닉네임(기억값보다 우선, 기억은 안 바꾼다)
///   -mpReady                 참가하면 바로 [준비]
///   -mpDifficulty 이름        호스트가 대기실에서 고를 난이도(Easy·Normal·Hard·Hell·God·Nightmare)
///   -mpAutoStart N           호스트가 N명이 모이고 전원 준비면 스스로 시작
///   -mpLobbyShot 초 경로      대기실에 들어간 뒤 그 초에 화면 캡처
///   -mpShot 초 경로           게임 씬 진입 뒤 그 초에 화면 캡처
///   -mpQuit 초               게임 씬 진입 뒤 그 초에 종료
///   -mpShotAt 초 경로         실행 뒤 그 초에 캡처(여러 번 줄 수 있다 — 대기실·나간 뒤 화면용)
///   -mpLeave 초              실행 뒤 그 초에 [나가기](호스트면 방 닫기 알림 포함)
///   -mpExitAt 초             실행 뒤 그 초에 종료
///   -mpTestUnits N           (호스트) 게임 씬 진입 뒤 슬롯마다 흔함 유닛 N기를 우리에 세운다 — 거울 확인용
///   -mpDump 초               게임 씬 진입 뒤 그 초에 거울 목록(종류·카탈로그·소유자·좌표·회전)을 로그로 — 여러 번 줄 수 있다
///   -mpTestWisps 초          게임 씬 진입 뒤 그 초에 내 위습 전부를 가장 가까운 유닛 포탈로 보낸다(클라=이동 요청 RPC)
///   -mpTestMoveUnits 초      게임 씬 진입 뒤 그 초에 내 유닛 전부를 레인 가운데로 보낸다(클라=이동 요청 RPC)
///   -mpTestEconomy 초        그 초에 (클라) 내 상점마다 0번 칸 사용 · 지금 되는 조합 전부 · 판매 보상 있는 유닛 하나 판매 — 요청 RPC 확인용
///   -mpTestPhase3 초         그 초에 (클라) 내 유닛 하나 창고 보관→4초 뒤 회수 · 항법 선택 · 복제된 스토리/도박/항법 상태 로그
///   -mpTestCommands 초       그 초부터 2초 간격으로 UnitCommands 공격이동→정지→홀드→적공격→모으기→우리로(클라=명령 요청 RPC)
/// </summary>
public class NetLauncher : MonoBehaviour
{
    // 헷갈리는 글자(0/O, 1/I/L)를 뺀 방 코드 글자.
    const string RoomCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    const int RoomCodeLength = 5;

    // 지역을 고정한다. 안 하면 각자 핑이 가장 좋은 지역으로 붙어서, 방장은 jp·친구는 kr에 들어가
    // 「방을 찾지 못했습니다(GameNotFound)」가 난다(09-26 실측 — 같은 기계 두 판도 갈렸다). 친구들이 한국이라 kr.
    const string DefaultRegion = "kr";

    [SerializeField] NetworkObject playerPrefab;
    [SerializeField] NetworkObject gameStatePrefab;
    [SerializeField] NetworkObject entityPrefab;
    [SerializeField] NetCatalog catalog;
    [SerializeField] int gameSceneBuildIndex = 1;

    NetworkRunner runner;
    NetSession session;
    bool starting;
    bool wasRunning;
    bool leavingOnPurpose;
    bool hostClosed;

    // 명령줄
    string cliSession;
    string region = DefaultRegion;
    string cliNick;
    bool cliReady;
    int cliDifficulty = NetGameState.NoDifficulty;
    int autoStartCount;
    float lobbyShotDelay = -1f;
    string lobbyShotPath;
    float shotDelay = -1f;
    string shotPath;
    float quitDelay = -1f;
    float leaveAt = -1f;
    float exitAt = -1f;
    int testUnits;
    readonly System.Collections.Generic.List<float> dumpDelays = new System.Collections.Generic.List<float>();
    float testWispsDelay = -1f;
    float testMoveUnitsDelay = -1f;
    float testCommandsDelay = -1f;
    float testEconomyDelay = -1f;
    float testPhase3Delay = -1f;

    public static NetLauncher Instance { get; private set; }

    public NetworkObject PlayerPrefab => playerPrefab;

    /// <summary>유닛·적·위습을 번호로 가리키는 목록(호스트·클라 같은 빌드면 같은 순서).</summary>
    public static NetCatalog Catalog => Instance != null ? Instance.catalog : null;
    public string RoomCode { get; private set; } = "";
    public string Status { get; private set; } = "";
    public bool IsBusy => starting;
    public bool InRoom => runner != null && runner.IsRunning;
    public bool IsHost => InRoom && runner.IsServer;
    public int GameSceneBuildIndex => gameSceneBuildIndex;

#if UNITY_EDITOR
    /// <summary>NetSetup(에디터 도구) 전용.</summary>
    public void EditorSetup(NetworkObject player, NetworkObject gameState, NetworkObject entity, NetCatalog netCatalog, int gameSceneIndex)
    {
        playerPrefab = player;
        gameStatePrefab = gameState;
        entityPrefab = entity;
        catalog = netCatalog;
        gameSceneBuildIndex = gameSceneIndex;
    }
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 게임에서 빠져나와 NetBoot로 돌아오면 씬에 새 창구가 또 생긴다 — 살아 있는 쪽을 쓴다.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (!TryGetComponent(out NetLobbyUi _)) gameObject.AddComponent<NetLobbyUi>();

        ReadCommandLine();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    // ───────────── 명령줄 ─────────────

    void ReadCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string Arg(int i) => i < args.Length ? args[i] : null;
        float Seconds(int i) => float.TryParse(Arg(i), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : -1f;

        bool host = false, join = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-mpHost": host = true; break;
                case "-mpJoin": join = true; break;
                case "-mpSession": cliSession = Arg(i + 1); break;
                case "-mpRegion": region = Arg(i + 1) ?? region; break;
                case "-mpNick": cliNick = Arg(i + 1); break;
                case "-mpReady": cliReady = true; break;
                case "-mpDifficulty":
                    if (Enum.TryParse(Arg(i + 1), true, out DifficultyMode mode)) cliDifficulty = (int)mode;
                    break;
                case "-mpAutoStart": int.TryParse(Arg(i + 1), out autoStartCount); break;
                case "-mpLobbyShot": lobbyShotDelay = Seconds(i + 1); lobbyShotPath = Arg(i + 2); break;
                case "-mpShot": shotDelay = Seconds(i + 1); shotPath = Arg(i + 2); break;
                case "-mpQuit": quitDelay = Seconds(i + 1); break;
                case "-mpShotAt": StartCoroutine(ShotAfter(Seconds(i + 1), Arg(i + 2))); break;
                case "-mpLeave": leaveAt = Seconds(i + 1); break;
                case "-mpExitAt": exitAt = Seconds(i + 1); break;
                case "-mpTestUnits": int.TryParse(Arg(i + 1), out testUnits); break;
                case "-mpDump": dumpDelays.Add(Seconds(i + 1)); break;
                case "-mpTestWisps": testWispsDelay = Seconds(i + 1); break;
                case "-mpTestMoveUnits": testMoveUnitsDelay = Seconds(i + 1); break;
                case "-mpTestCommands": testCommandsDelay = Seconds(i + 1); break;
                case "-mpTestEconomy": testEconomyDelay = Seconds(i + 1); break;
                case "-mpTestPhase3": testPhase3Delay = Seconds(i + 1); break;
            }
        }

        if (host) CreateRoom();
        else if (join) JoinRoom(cliSession);
    }

    // ───────────── 방 만들기 / 참가 / 나가기 ─────────────

    public void CreateRoom()
    {
        string code = !string.IsNullOrWhiteSpace(cliSession) ? cliSession.Trim().ToUpperInvariant() : NewRoomCode();
        _ = StartRunner(GameMode.Host, code);
    }

    public void JoinRoom(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            Status = "방 코드를 입력하세요.";
            return;
        }
        _ = StartRunner(GameMode.Client, code);
    }

    static string NewRoomCode()
    {
        var random = new System.Random();
        char[] code = new char[RoomCodeLength];
        for (int i = 0; i < code.Length; i++) code[i] = RoomCodeAlphabet[random.Next(RoomCodeAlphabet.Length)];
        return new string(code);
    }

    async Task StartRunner(GameMode mode, string code)
    {
        if (starting || runner != null) return;
        starting = true;
        hostClosed = false;
        leavingOnPurpose = false;
        RoomCode = code;
        Status = mode == GameMode.Host ? "방을 만드는 중…" : $"{code} 방에 들어가는 중…";

        GameObject go = new GameObject("NetworkRunner");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        session = go.AddComponent<NetSession>();
        session.SetPlayerPrefab(playerPrefab);
        go.AddComponent<NetMirrorHost>().Setup(entityPrefab, catalog);
        go.AddComponent<NetDiagnostics>().Attach(runner);
        NetworkSceneManagerDefault sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

        // 좌석은 NetPlayer가 생길 때 채워진다. 러너를 띄우는 순간부터 「네트 판」이다.
        MatchConfig.Reset();
        MatchConfig.Active = true;

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = code,
            PlayerCount = NetSession.MaxSlots,
            SceneManager = sceneManager,
            // 방 목록에 안 띄운다 — 코드를 아는 친구만 들어온다.
            IsVisible = false,
            CustomPhotonAppSettings = RegionSettings(),
        });

        starting = false;

        if (!result.Ok)
        {
            Status = mode == GameMode.Client
                ? $"{code} 방을 찾지 못했습니다. 코드를 확인하세요. ({result.ShutdownReason})"
                : $"방을 만들지 못했습니다. ({result.ShutdownReason} {result.ErrorMessage})";
            Debug.LogWarning($"[MP] StartGame 실패({mode}, 방 {code}): {result.ShutdownReason} {result.ErrorMessage}");
            Cleanup();
            return;
        }

        GameAuthority.Provider = new FusionAuthorityProvider(runner);
        wasRunning = true;

        if (runner.IsServer && gameStatePrefab != null)
        {
            int difficulty = cliDifficulty != NetGameState.NoDifficulty ? cliDifficulty : SavedDifficulty();
            runner.Spawn(gameStatePrefab, Vector3.zero, Quaternion.identity, null,
                (r, spawned) =>
                {
                    NetGameState state = spawned.GetComponent<NetGameState>();
                    state.Difficulty = difficulty;
                    state.Started = false;
                    state.CatalogFingerprint = catalog != null ? catalog.Fingerprint : 0;
                });
        }

        Status = mode == GameMode.Host ? "방을 열었습니다. 친구에게 방 코드를 알려 주세요." : "들어왔습니다. 준비를 누르고 호스트를 기다리세요.";
        Debug.Log($"[MP] StartGame 성공: {mode}, 방 {code}, IsServer={runner.IsServer}, 지역 {runner.SessionInfo.Region}");

        if (lobbyShotDelay >= 0f && !string.IsNullOrEmpty(lobbyShotPath)) StartCoroutine(ShotAfter(lobbyShotDelay, lobbyShotPath));
    }

    Fusion.Photon.Realtime.FusionAppSettings RegionSettings()
    {
        var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
        settings.FixedRegion = region;
        return settings;
    }

    // 싱글에서 마지막으로 고른 난이도(DifficultyManager가 기억해 둔 값)를 대기실 기본값으로 쓴다.
    static int SavedDifficulty()
    {
        try
        {
            if (!PlayerPrefs.HasKey(DifficultyManager.SavedModeKey)) return NetGameState.NoDifficulty;
            int saved = PlayerPrefs.GetInt(DifficultyManager.SavedModeKey);
            return Enum.IsDefined(typeof(DifficultyMode), saved) ? saved : NetGameState.NoDifficulty;
        }
        catch { return NetGameState.NoDifficulty; }
    }

    public void Leave()
    {
        if (runner == null) return;
        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
    {
        leavingOnPurpose = true;

        // 호스트가 나가면 방이 없어진다 — 그냥 끊으면 친구들은 「연결 끊김」만 본다. 먼저 알리고 잠깐 기다린다.
        if (IsHost && NetGameState.Instance != null)
        {
            NetGameState.Instance.RPC_HostClosing();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Cleanup();
        Status = "방에서 나왔습니다.";
        ReturnToBoot();
    }

    /// <summary>NetGameState.RPC_HostClosing이 부른다(클라에서).</summary>
    /// <summary>NetGameState가 부른다(클라): 호스트와 빌드가 달라 유닛 번호가 어긋나면 들어가지 않는다.</summary>
    public void OnBuildMismatch()
    {
        Debug.LogWarning("[MP] 호스트와 빌드가 다릅니다(카탈로그 지문 불일치) — 방에서 나옵니다.");
        leavingOnPurpose = true;
        Cleanup();
        Status = "방장과 게임 버전이 다릅니다. 같은 빌드로 다시 시도하세요.";
        ReturnToBoot();
    }

    public void OnHostClosing()
    {
        if (IsHost) return;
        hostClosed = true;
        Debug.Log("[MP] 호스트가 방을 닫는다고 알려 왔습니다.");
    }

    void Update()
    {
        if (leaveAt >= 0f && Time.realtimeSinceStartup >= leaveAt)
        {
            leaveAt = -1f;
            Debug.Log("[MP] -mpLeave 시간이 되어 나갑니다.");
            Leave();
        }

        if (exitAt >= 0f && Time.realtimeSinceStartup >= exitAt)
        {
            exitAt = -1f;
            Debug.Log("[MP] -mpExitAt 시간이 되어 종료합니다.");
            leavingOnPurpose = true;
            Cleanup();
            Application.Quit();
            return;
        }

        // 호스트가 나가거나 연결이 끊기면 러너가 스스로 멈춘다 — 싱글 상태로 되돌리고 NetBoot로.
        if (wasRunning && !leavingOnPurpose && (runner == null || !runner.IsRunning))
        {
            string message = hostClosed ? "호스트가 방을 닫았습니다." : "호스트와 연결이 끊겼습니다.";
            Debug.Log($"[MP] 러너가 멈췄습니다 — {message} 싱글 상태로 되돌리고 NetBoot로 돌아갑니다.");
            Cleanup();
            Status = message;
            ReturnToBoot();
            return;
        }

        if (cliReady && NetPlayer.Local != null && !NetPlayer.Local.IsHost && !NetPlayer.Local.Ready)
        {
            cliReady = false;
            SetReady(true);
        }

        if (autoStartCount > 0 && CanStartMatch && NetPlayer.All.Count >= autoStartCount)
        {
            // 좌석·닉네임 복제가 클라에 닿을 틈을 준다(좌석이 씬 로드 전에 채워져 있어야 PlayerContext.Awake가 본다).
            autoStartCount = 0;
            StartCoroutine(StartMatchAfter(1f));
        }
    }

    // ───────────── 대기실 조작 ─────────────

    public void SetReady(bool ready)
    {
        if (NetPlayer.Local != null) NetPlayer.Local.RPC_SetReady(ready);
    }

    public void SetNickname(string nickname)
    {
        NetPlayer.SaveNickname(nickname);
        if (NetPlayer.Local != null) NetPlayer.Local.RPC_SetNickname(nickname);
    }

    /// <summary>시작 전에 쓸 닉네임 — 명령줄이 있으면 그것, 없으면 기억값.</summary>
    public string InitialNickname => !string.IsNullOrWhiteSpace(cliNick) ? cliNick : NetPlayer.LoadNickname();

    public void SetDifficulty(DifficultyMode mode)
    {
        if (!IsHost || NetGameState.Instance == null || NetGameState.Instance.Started) return;
        NetGameState.Instance.Difficulty = (int)mode;
        try
        {
            // 싱글과 같은 기억값을 쓴다 — 다음에 방을 열 때 기본값이 된다.
            PlayerPrefs.SetInt(DifficultyManager.SavedModeKey, (int)mode);
            PlayerPrefs.Save();
        }
        catch { }
    }

    public bool AllReady => NetPlayer.All.Count > 0 && NetPlayer.All.All(p => p.IsReadyForStart);

    public bool CanStartMatch =>
        IsHost && session != null && !session.MatchStarted
        && NetGameState.Instance != null && NetGameState.Instance.SelectedDifficulty.HasValue
        && AllReady;

    /// <summary>시작 버튼이 왜 꺼져 있는지 — 버튼 아래에 그대로 보여 준다.</summary>
    public string StartBlockedReason
    {
        get
        {
            if (!IsHost) return "";
            if (NetGameState.Instance == null || !NetGameState.Instance.SelectedDifficulty.HasValue) return "난이도를 고르세요.";
            int notReady = NetPlayer.All.Count(p => !p.IsReadyForStart);
            if (notReady > 0) return $"{notReady}명이 아직 준비하지 않았습니다.";
            return "";
        }
    }

    IEnumerator StartMatchAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        StartMatch();
    }

    public void StartMatch()
    {
        if (!CanStartMatch) return;

        session.MatchStarted = true;
        NetGameState.Instance.Started = true;
        // 게임 씬 로드 전에 호스트 쪽 값도 확정해 둔다(Render가 다음 프레임에 옮기기 전에 씬이 먼저 뜰 수 있다).
        MatchConfig.Difficulty = NetGameState.Instance.SelectedDifficulty;
        runner.SessionInfo.IsOpen = false;

        Debug.Log($"[MP] 판 시작: 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(s => s))}}}, 난이도 {MatchConfig.Difficulty} → 씬 {gameSceneBuildIndex}");
        runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex), LoadSceneMode.Single);
    }

    // ───────────── 정리 ─────────────

    void Cleanup()
    {
        GameAuthority.Provider = null;
        LocalPlayer.LocalPlayerId = 0;
        MatchConfig.Reset();
        wasRunning = false;

        if (runner != null)
        {
            if (runner.IsRunning) _ = runner.Shutdown();
            Destroy(runner.gameObject);
        }
        runner = null;
        session = null;
        RoomCode = "";
    }

    void ReturnToBoot()
    {
        if (SceneManager.GetActiveScene().buildIndex != 0) SceneManager.LoadScene(0);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != gameSceneBuildIndex || runner == null) return;
        StartCoroutine(AfterGameSceneLoaded());
    }

    IEnumerator AfterGameSceneLoaded()
    {
        // Start들이 한 번 돈 뒤에 좌석 상태를 찍는다(RewardDistributor.Start가 위습을 뿌린 뒤).
        yield return null;

        string contexts = string.Join(" ", PlayerContext.All.OrderBy(c => c.PlayerId)
            .Select(c => $"{c.PlayerId}:{(c.IsOccupied ? "앉음" : "빔")}"));
        string difficulty = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected
            ? DifficultyManager.Instance.Current.ToString() : "미선택";
        Debug.Log($"[MP] 게임 씬 진입: IsServer={GameAuthority.IsServer}, LocalPlayerId={LocalPlayer.LocalPlayerId}, " +
                  $"좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(s => s))}}}, 난이도 {difficulty}, PlayerContext {contexts}");

        if (shotDelay >= 0f && !string.IsNullOrEmpty(shotPath)) StartCoroutine(ShotAfter(shotDelay, shotPath));
        if (quitDelay >= 0f) StartCoroutine(QuitAfter(quitDelay));
        if (testUnits > 0 && GameAuthority.IsServer) SpawnTestUnits(testUnits);
        foreach (float delay in dumpDelays) if (delay >= 0f) StartCoroutine(DumpAfter(delay));
        if (testWispsDelay >= 0f) StartCoroutine(TestMoveAfter(testWispsDelay, NetEntityKind.Wisp));
        if (testMoveUnitsDelay >= 0f) StartCoroutine(TestMoveAfter(testMoveUnitsDelay, NetEntityKind.Unit));
        if (testCommandsDelay >= 0f) StartCoroutine(TestCommandsAfter(testCommandsDelay));
        if (testEconomyDelay >= 0f) StartCoroutine(TestEconomyAfter(testEconomyDelay));
        if (testPhase3Delay >= 0f) StartCoroutine(TestPhase3After(testPhase3Delay));
    }

    // 테스트 전용(-mpTestUnits): 흔함 유닛을 슬롯마다 N기, 실제 소환 경로(UnitSpawner.Spawn → 우리 칸)로 세운다.
    void SpawnTestUnits(int perSlot)
    {
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        var commons = catalog != null ? catalog.units.Where(u => u != null && u.prefab != null && u.grade == UnitGrade.Common).ToList() : null;
        if (spawner == null || commons == null || commons.Count == 0)
        {
            Debug.LogWarning("[MP] -mpTestUnits: UnitSpawner나 흔함 유닛을 못 찾았습니다.");
            return;
        }

        int spawned = 0;
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            LaneMarker lane = LaneMarker.Get(context.PlayerId);
            for (int i = 0; i < perSlot; i++)
            {
                UnitData data = commons[(context.PlayerId * perSlot + i) % commons.Count];
                Vector3 position = lane != null ? lane.TakeSpawnPosition(data) : context.transform.position;
                if (spawner.Spawn(data, position, context.PlayerId) != null) spawned++;
            }
        }
        Debug.Log($"[MP] -mpTestUnits: 유닛 {spawned}기 소환");
    }

    // 테스트 전용: 사람이 우클릭하는 대신, 내 위습/유닛을 목적지로 보낸다. 클라는 우클릭과 같은 요청 RPC를 탄다.
    IEnumerator TestMoveAfter(float seconds, NetEntityKind kind)
    {
        yield return new WaitForSecondsRealtime(seconds);

        UnitPortal[] portals = FindObjectsByType<UnitPortal>(FindObjectsSortMode.None);
        LaneMarker lane = LaneMarker.Get(LocalPlayer.LocalPlayerId);
        int sent = 0;
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e.EntityKind != kind || e.Owner != LocalPlayer.LocalPlayerId) continue;
            Transform body = GameAuthority.IsServer ? (e.Real != null ? e.Real.transform : null) : (e.Visual != null ? e.Visual.transform : null);
            if (body == null) continue;

            Vector3 target;
            if (kind == NetEntityKind.Wisp)
            {
                UnitPortal nearest = portals.OrderBy(pt => (pt.transform.position - body.position).sqrMagnitude).FirstOrDefault();
                if (nearest == null) continue;
                target = nearest.transform.position;
            }
            else
            {
                if (lane == null) continue;
                target = lane.LaneCenter;
            }

            if (GameAuthority.IsServer)
            {
                if (body.TryGetComponent(out UnitMover mover)) { mover.MoveToGroundPoint(target, "테스트"); sent++; }
            }
            else
            {
                NetCommands.RequestMove(body, target);
                sent++;
            }
        }
        Debug.Log($"[MP] 테스트 이동({kind}): {sent}개 보냄 ({(GameAuthority.IsServer ? "직접" : "요청 RPC")})");
    }

    // 테스트 전용: 사람이 단축키를 누르는 대신 UnitCommands를 직접 부른다 — 클라에선 그 안의 // MP: 분기가 요청 RPC를 보낸다.
    IEnumerator TestCommandsAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        var mine = new System.Collections.Generic.List<Selectable>();
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e.EntityKind != NetEntityKind.Unit || e.Owner != LocalPlayer.LocalPlayerId) continue;
            GameObject body = GameAuthority.IsServer ? e.Real : e.Visual;
            if (body != null && body.TryGetComponent(out Selectable s)) mine.Add(s);
        }

        LaneMarker lane = LaneMarker.Get(LocalPlayer.LocalPlayerId);
        Vector3 center = lane != null ? lane.LaneCenter : Vector3.zero;
        Debug.Log($"[MP] 테스트 명령: 내 유닛 {mine.Count}기");

        Debug.Log($"[MP] 테스트 명령 공격이동 → {UnitCommands.AttackMove(mine, center)}");
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log($"[MP] 테스트 명령 정지 → {UnitCommands.Stop(mine)}");
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log($"[MP] 테스트 명령 홀드 → {UnitCommands.Hold(mine)}");
        yield return new WaitForSecondsRealtime(2f);
        EnemyDummy nearest = null;
        float best = float.MaxValue;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy.LaneIndex != LocalPlayer.LocalPlayerId) continue;
            float d = (enemy.transform.position - center).sqrMagnitude;
            if (d < best) { best = d; nearest = enemy; }
        }
        Debug.Log($"[MP] 테스트 명령 적공격({(nearest != null ? nearest.name : "없음")}) → {UnitCommands.AttackTarget(mine, nearest)}");
        yield return new WaitForSecondsRealtime(3f);
        Debug.Log($"[MP] 테스트 명령 모으기 → {UnitCommands.Gather(mine)}");
        yield return new WaitForSecondsRealtime(2f);
        Debug.Log($"[MP] 테스트 명령 우리로 → {UnitCommands.SendToPen(mine)}");
    }

    IEnumerator TestEconomyAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        int me = LocalPlayer.LocalPlayerId;

        int shopsTried = 0;
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(behaviour is ILaneShop shop)) continue;
            if (!behaviour.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId != me) continue;
            LaneShopSlotView view = shop.GetSlotView(0);
            if (string.IsNullOrEmpty(view.label) || view.targetKind != LaneShopTargetKind.None) continue;
            bool sent = NetCommands.RequestShopUse(shop, 0, default);
            Debug.Log($"[MP] 테스트 상점: {behaviour.name}({behaviour.GetType().Name}) 0번 「{view.label}」 사용가능 {view.available} → 보냄 {sent}");
            shopsTried++;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Debug.Log($"[MP] 테스트 상점: {shopsTried}곳");

        CombineSystem system = FindFirstObjectByType<CombineSystem>();
        int combos = 0;
        if (system != null)
        {
            for (int i = 0; system.RecipeAt(i) != null; i++)
            {
                CombineRecipe recipe = system.RecipeAt(i);
                if (!system.CanCombineNow(recipe)) continue;
                NetCommands.RequestCombine(system, recipe, null);
                Debug.Log($"[MP] 테스트 조합: 조합식 {i} 요청");
                combos++;
                yield return new WaitForSecondsRealtime(1f);
                if (combos >= 3) break;
            }
        }
        Debug.Log($"[MP] 테스트 조합: {combos}건 요청");

        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e.EntityKind != NetEntityKind.Unit || e.Owner != me || e.Visual == null) continue;
            if (!e.Visual.TryGetComponent(out UnitIdentity id) || id.Data == null) continue;
            UnitData d = id.Data;
            bool sellable = d.sellRewardWisp != null || d.sellRewardTraitPoints > 0 || d.sellRewardWood > 0 || d.sellTriggersItemGamblePool != null || d.sellRewardEveryNSells > 0;
            if (!sellable || !e.Visual.TryGetComponent(out Selectable s)) continue;
            NetCommands.RequestHudUnitAction(NetHudAction.Sell, s, 0);
            Debug.Log($"[MP] 테스트 판매: {d.unitName} 요청");
            break;
        }
    }

    IEnumerator TestPhase3After(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        int me = LocalPlayer.LocalPlayerId;

        Selectable unit = null;
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
            if (e.EntityKind == NetEntityKind.Unit && e.Owner == me && e.Visual != null && e.Visual.TryGetComponent(out unit)) break;

        if (unit != null)
        {
            Vector3 before = unit.transform.position;
            Debug.Log($"[MP] 테스트 창고 보관 요청 → {NetCommands.RequestWarehouse(unit)} (위치 {before.x:F0},{before.z:F0})");
            yield return new WaitForSecondsRealtime(4f);
            Vector3 stored = unit.transform.position;
            Debug.Log($"[MP] 테스트 창고: 보관 뒤 위치 {stored.x:F0},{stored.z:F0} · 회수 요청 → {NetCommands.RequestWarehouse(unit)}");
            yield return new WaitForSecondsRealtime(4f);
            Vector3 back = unit.transform.position;
            Debug.Log($"[MP] 테스트 창고: 회수 뒤 위치 {back.x:F0},{back.z:F0}");
        }

        NetCommands.RequestNavigation(NavigationChoice.Hegemon);
        yield return new WaitForSecondsRealtime(2f);

        PlayerContext context = PlayerContext.Local;
        StoryManager story = StoryManager.Instance;
        string gamble = "";
        NetCatalog catalog = Catalog;
        if (catalog != null && context != null && context.GamblingProgress != null)
            foreach (GamblingOptionData option in catalog.gamblingOptions)
                gamble += $"{option.name}:{(context.GamblingProgress.IsUnlocked(option) ? "해금" : "잠김")}/{context.GamblingProgress.UsesSoFar(option)}회 ";
        Debug.Log($"[MP] 테스트 상태(클라): 항법 {context?.NavigationState?.Choice} · 스토리 진행 {story?.HasRunningStory} 대기 {story?.IsWaiting} 「{story?.StatusLabel}」 {story?.SecondsUntilNext:F0}초 · 도박 {gamble}");
    }

    IEnumerator DumpAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        var entities = FindObjectsByType<NetEntity>(FindObjectsSortMode.None)
            .Where(e => e.Object != null && e.Object.IsValid)
            .OrderBy(e => e.Object.Id.Raw)
            .ToList();
        var lines = entities.Select(e =>
        {
            Vector3 p = e.transform.position;
            string visual = GameAuthority.IsServer ? "실물" : (e.Visual != null ? "겉모습" : "겉모습없음");
            return $"{e.Object.Id.Raw}:{e.EntityKind}#{e.CatalogIndex}/p{e.Owner} ({p.x:F1},{p.z:F1}) r{e.transform.eulerAngles.y:F0} {visual}";
        });
        Debug.Log($"[MP] 거울 목록({(GameAuthority.IsServer ? "호스트" : "클라")}, {entities.Count}개): " + string.Join(" | ", lines));
    }

    IEnumerator ShotAfter(float seconds, string path)
    {
        yield return new WaitForSecondsRealtime(seconds);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[MP] 캡처: {path}");
    }

    IEnumerator QuitAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Debug.Log("[MP] -mpQuit 시간이 되어 종료합니다.");
        leavingOnPurpose = true;
        Cleanup();
        Application.Quit();
    }
}
