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
///   -mpSolo                  바로 「혼자 하기」
///   -mpSaveDir 폴더           세이브 폴더(같은 PC 두 창이 같은 player_0.json을 안 쓰게) — 테스트용
///   -mpTestFinishRun 초      (호스트) 그 초에 살아 있는 전원 +5점 뒤 FinishRun(클리어 아님) — 판 끝 세이브 경로 확인용
///   -mpSession 코드           방 코드(호스트는 발급 대신 이 코드로 연다)
///   -mpRegion 지역            Photon 지역(기본 kr) — 방장과 친구가 같아야 서로 보인다
///   -mpNick 이름              닉네임(기억값보다 우선, 기억은 안 바꾼다)
///   -mpReady                 참가하면 바로 [준비]
///   -mpDifficulty 이름        호스트가 대기실에서 고를 난이도(Easy·Normal·Hard·Hell·God·Nightmare)
///   -mpAutoStart N           호스트가 N명이 모이고 전원 준비면 스스로 시작
///   -mpTestSlot N 초         (대기실 자리 이동 확인, 사장님 10-06) 내 NetPlayer가 생긴 뒤 그 초에 「N번 자리(1~4, 줄 번호)로 옮겨 달라」 요청 — 여러 번 가능. 로그 접두 「[자리]」
///   -mpTestStartAt 초        (호스트) 내 NetPlayer가 생긴 뒤 그 초에 [시작](전원 준비 여부 무시) — 자리 이동 시험용
///                            예(두 창): 호스트 `-mpHost -mpSaveDir a -mpTestSlot 3 4 -mpTestStartAt 14` · 친구 `-mpJoin <코드> -mpSaveDir b -mpTestSlot 2 4`
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
///   -mpTestTraitUnits        (호스트) 슬롯마다 즉시형 특성(대상 지정 아님, 비용 1 이하)을 가진 유닛 1기
///   -mpTestTrait 초          (클라) 그 초에 특성 버튼 요청 — 복제된 특성 포인트·해금 전후 로그
///   -mpTestChat 초 코드      그 초에 채팅 한 줄(게임 씬, 여러 번 가능)
///   -mpLobbyChat 초 문장     방에 들어간 뒤 그 초에 대기실 채팅 한 줄
///   -mpTestGambleLabels 초   그 초에 (호스트·클라 각자) 내 도박소 칸 글자 전부 로그 — 재고 「남은/최대 · N초」 복제 확인
///   -mpCamWisp 초            그 초에 카메라를 위습 쪽으로(주인 색 캡처용)
///   -mpToken 문자열          접속 식별값(재접속 때 같은 사람 알아보기)을 이 값으로 — 한 기계 두 창 테스트용(기본은 설치마다 GUID)
///   -mpDropAt 초             (클라) 그 초(실행 뒤 절대 시간)에 [나가기] 예고 없이 러너를 끊는다 — 망 끊김 흉내
///   -mpRejoinAfter 초        (클라) 끊겨서 첫 화면으로 돌아온 뒤 그 초에 [다시 참가]를 누른다
///   -mpTestDupes N           (호스트) 게임 씬 진입 뒤 슬롯마다 같은 흔함 N기 두 종류를 우리에 — 이름표 「×N」 확인용
///   -mpTestSameType 초 경로   그 초에 내 유닛 하나로 「같은 종류 전부 선택」(더블클릭·Ctrl+클릭과 같은 함수)을 부르고 캡처
///   -mpTestCoin 초 N          그 초부터 내 도박소 0번 칸(10엔 도박)을 N번 누른다(클라=요청 RPC) — coinsound 확인
///   -mpSoloMenuMainAt 초 경로 (혼자 하기) 그 초에 「메뉴」 첫 화면([계속하기]·[소리 끄기]·[처음 화면으로])을 열고 캡처
///   -mpSoloShopSoundAt 초     (혼자 하기) 돈을 채워 500엔 도박(해금·재고 7로)을 7번 · 특성 포인트 구매 · 졸업 뒤 목재 구입 — 결과마다 소리 여부
///   -mpSoloCoinAt 초          (혼자 하기) 그 초에 10엔 도박 → 소리 끔 → 10엔 도박 → 소리 켬(끈 동안 안 나는지)
///   -mpTestNotices 초 경로    (호스트) 알림 묶음 확인 — 보스 타이머 칸(캡처)·조합 부족·유닛도박 공지·승리 문구, 알림을 로그로
///   -mpTestGap 초             (호스트) GAP 09-27 다섯 항목 확인 — 데스 경고·위습 페널티·패배 세이브(슬롯 1)·창고·판매 위습
///   -mpTestItems 초           (호스트) 그 초에 모든 슬롯에 I011 위습꾸러미·I00S 고대의배를 1개씩 준다 — 친구 화면 아이템 칸 복제 확인용
///   -mpTestUseItem 초         (클라) 그 초에 내 아이템 칸 내용을 로그로 → 위습꾸러미 사용 요청(RPC) → 3초 뒤 다시 로그
///   -mpTestSecondSave 초      (호스트) 41R 2차 세이브 보상(RewardDistributor.GrantSecondSaveRewards)을 그 초에 한 번 부르고 슬롯별 위습 수·호스트가 든 슬롯별 세이브값을 로그로 —
///                             클라 세이브(player_0.json: 클리어 30·포인트 1500)는 -mpSaveDir 폴더에 미리 넣어 둔다
///   -mpTestSelect 초 폴더     (양쪽, 클라 확인용) 슬롯 1(친구) 유닛 최대 4기와 레인 1 적 둘을 **거울 ID 순**으로 골라 캡처 —
///                             방장·친구가 같은 개체를 고르므로 파일 이름(id)으로 나란히 비교한다. 소환 없이 있는 것만
///   -mpTestPortraits 초 폴더  (호스트) 흔함·특별함·재규어·적·매머드·보스를 차례로 골라 초상화 캡처 + 초상 켬/끔 FPS
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
    bool sawMatchStarted;
    bool startedAsHost;   // 러너가 멈춘 뒤엔 runner.IsServer를 못 믿는다 — 띄울 때 모드를 기억

    const string ConnectionTokenPrefsKey = "GuilRandomDefense.ConnectionToken";
    string cliToken;
    float dropAt = -1f;
    float rejoinAfter = -1f;
    float rejoinAtTime = -1f;
    float testMenuDelay = -1f;
    int testDupes;
    float testGapDelay = -1f;
    float testSecondSaveDelay = -1f;
    float testItemsDelay = -1f;
    float testUseItemDelay = -1f;
    float testNoticesDelay = -1f;
    string testNoticesShot;
    int noticesLogged;
    float testCoinDelay = -1f;
    int testCoinCount;
    float soloCoinAt = -1f;
    float soloShopSoundAt = -1f;
    float soloMenuMainAt = -1f;
    string soloMenuMainShot;
    float testSameTypeDelay = -1f;
    string testSameTypeShot;
    float soloMenuAt = -1f;
    string soloMenuShot;
    float soloHomeAt = -1f;
    static bool soloTestsScheduled;   // 혼자 하기 → 처음 화면 → 다시 혼자 하기에서 새 창구가 같은 명령줄을 또 읽는다 — 테스트 예약은 한 번만
    string testMenuShot;

    /// <summary>판 도중 예고 없이 끊겼을 때 기억해 둔 방 코드 — 첫 화면의 [다시 참가]가 쓴다(성공하면 비운다).</summary>
    public string RejoinCode { get; private set; } = "";

    // 명령줄
    string cliSession;
    string region = DefaultRegion;
    string cliNick;
    bool cliReady;
    int cliDifficulty = NetGameState.NoDifficulty;
    int autoStartCount;
    // 자리 이동 시험(-mpTestSlot/-mpTestStartAt)
    readonly System.Collections.Generic.List<(float delay, int slot)> testSlotRequests = new System.Collections.Generic.List<(float, int)>();
    float testStartAtDelay = -1f;
    float seatTestBase = -1f;
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
    float testMainDelay = -1f;   // 10-06 0.3.12 후보 멀티 점검(NetMainTest)
    string testMainShot;
    float testCommandsDelay = -1f;
    float testEconomyDelay = -1f;
    float testFinishRunDelay = -1f;
    float camWispDelay = -1f;
    float testPortraitsDelay = -1f;
    string testPortraitsDir;
    float testSelectDelay = -1f;
    float test0929Delay = -1f; string test0929Dir;
    float testVfxDelay = -1f; string testVfxDir;
    string testSelectDir;
    readonly System.Collections.Generic.List<(float, string)> shotAts = new System.Collections.Generic.List<(float, string)>();
    float testPhase3Delay = -1f;
    bool testTraitUnits;
    float testTraitDelay = -1f;
    readonly System.Collections.Generic.List<(float, string)> chatTests = new System.Collections.Generic.List<(float, string)>();
    readonly System.Collections.Generic.List<(float, string)> lobbyChatTests = new System.Collections.Generic.List<(float, string)>();
    readonly System.Collections.Generic.List<float> gambleLabelDelays = new System.Collections.Generic.List<float>();

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

        bool host = false, join = false, solo = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-mpHost": host = true; break;
                case "-mpSolo": solo = true; break;
                case "-mpSaveDir": PersistentSave.SaveRootOverride = Arg(i + 1); break;
                case "-mpTestFinishRun": testFinishRunDelay = Seconds(i + 1); break;
                case "-mpCamWisp": camWispDelay = Seconds(i + 1); break;
                case "-mpToken": cliToken = Arg(i + 1); break;
                case "-mpTestGap": testGapDelay = Seconds(i + 1); break;
                case "-mpTestSecondSave": testSecondSaveDelay = Seconds(i + 1); break;
                case "-mpTestItems": testItemsDelay = Seconds(i + 1); break;
                case "-mpTestUseItem": testUseItemDelay = Seconds(i + 1); break;
                case "-mpTestNotices": testNoticesDelay = Seconds(i + 1); testNoticesShot = Arg(i + 2); break;
                case "-mpTestCoin": testCoinDelay = Seconds(i + 1); int.TryParse(Arg(i + 2), out testCoinCount); break;
                case "-mpSoloCoinAt": soloCoinAt = Seconds(i + 1); break;
                case "-mpSoloShopSoundAt": soloShopSoundAt = Seconds(i + 1); break;
                case "-mpSoloMenuMainAt": soloMenuMainAt = Seconds(i + 1); soloMenuMainShot = Arg(i + 2); break;
                case "-mpTestDupes": int.TryParse(Arg(i + 1), out testDupes); break;
                case "-mpTestSameType": testSameTypeDelay = Seconds(i + 1); testSameTypeShot = Arg(i + 2); break;
                case "-mpSoloMenuAt": soloMenuAt = Seconds(i + 1); soloMenuShot = Arg(i + 2); break;
                case "-mpSoloHomeAt": soloHomeAt = Seconds(i + 1); break;
                case "-mpTestMenu": testMenuDelay = Seconds(i + 1); testMenuShot = Arg(i + 2); break;
                case "-mpDropAt": dropAt = Seconds(i + 1); break;
                case "-mpRejoinAfter": rejoinAfter = Seconds(i + 1); break;
                case "-mpTestVfx": testVfxDelay = Seconds(i + 1); testVfxDir = Arg(i + 2); break;
                case "-mpTest0929": test0929Delay = Seconds(i + 1); test0929Dir = Arg(i + 2); break;
                case "-mpTestSelect": testSelectDelay = Seconds(i + 1); testSelectDir = Arg(i + 2); break;
                case "-mpTestPortraits": testPortraitsDelay = Seconds(i + 1); testPortraitsDir = Arg(i + 2); break;
                case "-mpJoin": join = true; break;
                case "-mpSession": cliSession = Arg(i + 1); break;
                case "-mpRegion": region = Arg(i + 1) ?? region; break;
                case "-mpNick": cliNick = Arg(i + 1); break;
                case "-mpReady": cliReady = true; break;
                case "-mpDifficulty":
                    if (Enum.TryParse(Arg(i + 1), true, out DifficultyMode mode)) cliDifficulty = (int)mode;
                    break;
                case "-mpAutoStart": int.TryParse(Arg(i + 1), out autoStartCount); break;
                case "-mpTestSlot": { int.TryParse(Arg(i + 1), out int wanted); float d = Seconds(i + 2); if (wanted >= 1 && wanted <= NetSession.MaxSlots && d >= 0f) testSlotRequests.Add((d, wanted - 1)); break; }
                case "-mpTestStartAt": testStartAtDelay = Seconds(i + 1); break;
                case "-mpLobbyShot": lobbyShotDelay = Seconds(i + 1); lobbyShotPath = Arg(i + 2); break;
                case "-mpShot": shotDelay = Seconds(i + 1); shotPath = Arg(i + 2); break;
                case "-mpQuit": quitDelay = Seconds(i + 1); break;
                case "-mpShotAt": shotAts.Add((Seconds(i + 1), Arg(i + 2))); StartCoroutine(ShotAfter(Seconds(i + 1), Arg(i + 2))); break;
                case "-mpLeave": leaveAt = Seconds(i + 1); break;
                case "-mpExitAt": exitAt = Seconds(i + 1); break;
                case "-mpTestUnits": int.TryParse(Arg(i + 1), out testUnits); break;
                case "-mpDump": dumpDelays.Add(Seconds(i + 1)); break;
                case "-mpTestWisps": testWispsDelay = Seconds(i + 1); break;
                case "-mpTestMoveUnits": testMoveUnitsDelay = Seconds(i + 1); break;
                case "-mpTestMain": testMainDelay = Seconds(i + 1); testMainShot = Arg(i + 2); break;
                case "-mpTestCommands": testCommandsDelay = Seconds(i + 1); break;
                case "-mpTestEconomy": testEconomyDelay = Seconds(i + 1); break;
                case "-mpTestPhase3": testPhase3Delay = Seconds(i + 1); break;
                case "-mpTestTraitUnits": testTraitUnits = true; break;
                case "-mpTestTrait": testTraitDelay = Seconds(i + 1); break;
                case "-mpTestChat": chatTests.Add((Seconds(i + 1), Arg(i + 2))); break;
                case "-mpLobbyChat": lobbyChatTests.Add((Seconds(i + 1), Arg(i + 2))); break;
                case "-mpTestGambleLabels": gambleLabelDelays.Add(Seconds(i + 1)); break;
            }
        }

        if (solo) { PlaySolo(); return; }
        if (host) CreateRoom();
        else if (join) JoinRoom(cliSession);
    }

    // ───────────── 방 만들기 / 참가 / 나가기 ─────────────

    /// <summary>
    /// 「혼자 하기」 — 이 창구(와 대기실 UI)를 통째로 없애고 게임 씬만 연다. 러너·Provider·MatchConfig가 하나도 안 남아서
    /// 게임은 SampleScene을 직접 연 것과 한 줄도 다르지 않다(Fusion을 전혀 안 거친다). PM 조건(09-26).
    /// </summary>
    public void PlaySolo()
    {
        if (runner != null || starting) return;
        Debug.Log("[MP] 혼자 하기 — 창구를 없애고 게임 씬을 연다(네트 없음)");
        int scene = gameSceneBuildIndex;
        GameAuthority.Provider = null;
        MatchConfig.Reset();
        LocalPlayer.LocalPlayerId = 0;
        NetLoadingHook.Show("혼자 하기");
        // 테스트 전용: 이 창구는 곧 사라지니 남은 -mpShotAt 캡처는 씬을 넘어 사는 작은 오브젝트에 넘긴다(로딩 화면 확인용).
        if (!soloTestsScheduled && (shotAts.Count > 0 || soloMenuAt >= 0f || soloHomeAt >= 0f || exitAt >= 0f || soloCoinAt >= 0f || soloMenuMainAt >= 0f || soloShopSoundAt >= 0f))
        {
            soloTestsScheduled = true;
            var survivor = new GameObject("[MP] 캡처(혼자 하기)").AddComponent<NetShotHelper>();
            DontDestroyOnLoad(survivor.gameObject);
            survivor.Schedule(shotAts);
            survivor.ScheduleMenu(soloMenuAt, soloMenuShot, soloHomeAt, exitAt);
            survivor.ScheduleCoin(soloCoinAt);
            survivor.ScheduleShopSounds(soloShopSoundAt);
            survivor.ScheduleMenuMain(soloMenuMainAt, soloMenuMainShot);
        }
        Destroy(gameObject);
        SceneManager.LoadScene(scene);
    }

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

    async Task StartRunner(GameMode mode, string code, bool isRetry = false)
    {
        if ((starting && !isRetry) || runner != null) return;
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
        // 권한 판정도 러너를 띄우는 순간부터 — 판 도중 재접속하면 StartGame이 끝나기 전에 게임 씬이 뜰 수 있고,
        // Provider가 비어 있으면 GameAuthority.IsServer가 true(싱글)라 클라가 호스트 일(위습 뿌리기 등)을 해 버린다.
        GameAuthority.Provider = new FusionAuthorityProvider(runner);
        sawMatchStarted = false;
        startedAsHost = mode == GameMode.Host;

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = code,
            PlayerCount = NetSession.MaxSlots,
            SceneManager = sceneManager,
            // 방 목록에 안 띄운다 — 코드를 아는 친구만 들어온다.
            IsVisible = false,
            CustomPhotonAppSettings = RegionSettings(),
            // 같은 사람 알아보기(재접속) — 호스트가 PlayerJoined에서 읽는다. 닉네임은 겹칠 수 있어 안 쓴다.
            ConnectionToken = System.Text.Encoding.UTF8.GetBytes(ConnectionToken()),
        });

        starting = false;

        if (!result.Ok)
        {
            // 한 번만 자동으로 다시(PM 09-26) — 이 맥에서도 하루 세 번, 첫 접속이 Photon에 안 닿고 바로 다시 하면 붙었다.
            // 방장: 접속 자체 실패(ExceptionOnConnect·시간 초과) / 친구: 방이 없음(방장이 재시도 중일 수 있다).
            bool connectFailure = result.ShutdownReason == ShutdownReason.ConnectionTimeout
                || result.ShutdownReason == ShutdownReason.PhotonCloudTimeout
                || (result.ErrorMessage ?? "").Contains("ExceptionOnConnect");
            bool retryable = connectFailure || (mode == GameMode.Client && result.ShutdownReason == ShutdownReason.GameNotFound);
            if (!isRetry && retryable)
            {
                Debug.LogWarning($"[MP] 진단: 연결 재시도 — {mode}, 방 {code}, 첫 시도 {result.ShutdownReason} {result.ErrorMessage}");
                Cleanup();
                starting = true;   // 기다리는 2초 동안 버튼이 다시 안 눌리게
                Status = "연결을 다시 시도합니다…";
                await Task.Delay(2000);
                starting = false;
                if (this == null) return;   // 그 사이 창구가 사라졌으면(혼자 하기 등) 그만
                await StartRunner(mode, code, isRetry: true);
                return;
            }
            if (isRetry) Debug.LogWarning($"[MP] 진단: 재시도도 실패 — {mode}, 방 {code}: {result.ShutdownReason} {result.ErrorMessage}");

            Status = mode == GameMode.Client
                ? $"{code} 방을 찾지 못했습니다. 코드를 확인하세요. ({result.ShutdownReason})"
                : $"방을 만들지 못했습니다. ({result.ShutdownReason} {result.ErrorMessage})";
            Debug.LogWarning($"[MP] StartGame 실패({mode}, 방 {code}): {result.ShutdownReason} {result.ErrorMessage}");
            Cleanup();
            return;
        }

        wasRunning = true;
        if (mode == GameMode.Client) RejoinCode = "";

        if (runner.IsServer && gameStatePrefab != null)
        {
            int difficulty = cliDifficulty;   // 난이도는 게임 안에서 방장이 정한다(사장님 10-03) — 명령줄 테스트 값만 미리 건다
            runner.Spawn(gameStatePrefab, Vector3.zero, Quaternion.identity, null,
                (r, spawned) =>
                {
                    NetGameState state = spawned.GetComponent<NetGameState>();
                    state.Difficulty = difficulty;
                    state.Started = false;
                    state.CatalogFingerprint = catalog != null ? catalog.Fingerprint : 0;
                });
        }

        if (runner.IsServer && entityPrefab != null) StartCoroutine(PrewarmEntityPrefab());

        Status = mode == GameMode.Host ? "방을 열었습니다. 친구에게 방 코드를 알려 주세요." : "들어왔습니다. 준비를 누르고 호스트를 기다리세요.";
        Debug.Log($"[MP] StartGame 성공: {mode}, 방 {code}, IsServer={runner.IsServer}, 지역 {runner.SessionInfo.Region}{(isRetry ? " (재시도로 성공)" : "")}");

        if (lobbyShotDelay >= 0f && !string.IsNullOrEmpty(lobbyShotPath)) StartCoroutine(ShotAfter(lobbyShotDelay, lobbyShotPath));
        foreach (var (delay, text) in lobbyChatTests) if (delay >= 0f && !string.IsNullOrEmpty(text)) StartCoroutine(TestChatAfter(delay, text));
    }

    string ConnectionToken()
    {
        if (!string.IsNullOrWhiteSpace(cliToken)) return cliToken.Trim();
        try
        {
            string token = PlayerPrefs.GetString(ConnectionTokenPrefsKey, "");
            if (string.IsNullOrEmpty(token))
            {
                token = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(ConnectionTokenPrefsKey, token);
                PlayerPrefs.Save();
            }
            return token;
        }
        catch { return Guid.NewGuid().ToString("N"); }   // 못 저장하면 이번 실행만의 값(재접속은 같은 실행 안에서만)
    }

    /// <summary>첫 화면 [다시 참가].</summary>
    public void Rejoin()
    {
        if (string.IsNullOrEmpty(RejoinCode) || IsBusy || InRoom) return;
        Debug.Log($"[MP] 다시 참가: 방 {RejoinCode}");
        JoinRoom(RejoinCode);
    }

    Fusion.Photon.Realtime.FusionAppSettings RegionSettings()
    {
        var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
        settings.FixedRegion = region;
        return settings;
    }

    // NetEntity 프리팹은 처음 한 번 지연 로드된다 — 게임 첫 틱에 거울 수십 개가 한꺼번에 「Failed to load prefab synchronously」로
    // 한 번씩 실패하고 다음 틱에 다시 섰다(09-26 회귀에서 15건). 대기실에서 빈 거울 하나를 미리 세워 로드를 끝내 둔다 —
    // 실물이 없는 거울이라 NetEntity가 다음 틱에 스스로 거둔다.
    IEnumerator PrewarmEntityPrefab()
    {
        for (int attempt = 0; attempt < 20 && runner != null && runner.IsRunning; attempt++)
        {
            bool ok;
            try { ok = runner.Spawn(entityPrefab, Vector3.zero, Quaternion.identity) != null; }
            catch (Exception) { ok = false; }
            if (ok) { Debug.Log($"[MP] 거울 프리팹 미리 로드 완료({attempt + 1}번째)"); yield break; }
            yield return new WaitForSecondsRealtime(0.25f);
        }
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
        RejoinCode = "";

        // 판 도중 친구의 [나가기] — 호스트가 끊김(60초 유예)으로 보지 않고 바로 원작대로 정리하게 먼저 알린다.
        if (!IsHost && sawMatchStarted && NetPlayer.Local != null)
        {
            NetPlayer.Local.RPC_LeavingOnPurpose();
            yield return new WaitForSecondsRealtime(0.4f);
        }

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

        if (NetGameState.Instance != null && NetGameState.Instance.Started) sawMatchStarted = true;

        if (dropAt >= 0f && Time.realtimeSinceStartup >= dropAt && runner != null && runner.IsRunning)
        {
            dropAt = -1f;
            Debug.Log("[MP] -mpDropAt: [나가기] 예고 없이 러너를 끊습니다(망 끊김 흉내).");
            _ = runner.Shutdown();
        }

        // 호스트가 나가거나 연결이 끊기면 러너가 스스로 멈춘다 — 싱글 상태로 되돌리고 NetBoot로.
        if (wasRunning && !leavingOnPurpose && (runner == null || !runner.IsRunning))
        {
            // 판 도중 친구 쪽 끊김이면 방 코드를 기억해 [다시 참가]를 띄운다(방장은 60초 동안 자리를 붙잡고 있다).
            bool canRejoin = !startedAsHost && sawMatchStarted && !hostClosed && !string.IsNullOrEmpty(RoomCode);
            if (canRejoin) RejoinCode = RoomCode;
            string message = hostClosed ? "호스트가 방을 닫았습니다."
                : canRejoin ? $"호스트와 연결이 끊겼습니다. {NetSession.GraceSeconds:F0}초 안에 [다시 참가]를 누르면 이어서 할 수 있습니다."
                : "호스트와 연결이 끊겼습니다.";
            Debug.Log($"[MP] 러너가 멈췄습니다 — {message} 싱글 상태로 되돌리고 NetBoot로 돌아갑니다.");
            Cleanup();
            Status = message;
            ReturnToBoot();
            if (canRejoin && rejoinAfter >= 0f) { rejoinAtTime = Time.realtimeSinceStartup + rejoinAfter; rejoinAfter = -1f; }   // 테스트 자동 재참가는 한 번만
            return;
        }

        if (rejoinAtTime >= 0f && Time.realtimeSinceStartup >= rejoinAtTime)
        {
            rejoinAtTime = -1f;
            Debug.Log("[MP] -mpRejoinAfter: [다시 참가]");
            Rejoin();
        }

        if (cliReady && NetPlayer.Local != null && !NetPlayer.Local.IsHost && !NetPlayer.Local.Ready)
        {
            cliReady = false;
            SetReady(true);
        }

        TickSeatTests();

        if (autoStartCount > 0 && CanStartMatch && NetPlayer.All.Count >= autoStartCount)
        {
            // 좌석·닉네임 복제가 클라에 닿을 틈을 준다(좌석이 씬 로드 전에 채워져 있어야 PlayerContext.Awake가 본다).
            autoStartCount = 0;
            StartCoroutine(StartMatchAfter(1f));
        }
    }

    // 자리 이동 시험(-mpTestSlot/-mpTestStartAt) — 내 NetPlayer가 생긴 뒤 시간으로 잰다. 요청 1.5초 뒤에 결과(내 슬롯·좌석 집합)를 로그로.
    void TickSeatTests()
    {
        if (testSlotRequests.Count == 0 && testStartAtDelay < 0f) return;
        if (NetPlayer.Local == null) return;
        bool started = NetGameState.Instance != null && NetGameState.Instance.Started;   // 시작 뒤에도 요청은 보낸다(시나리오 ④: 호스트가 거절해야 한다)
        if (seatTestBase < 0f) { seatTestBase = Time.unscaledTime; Debug.Log($"[자리] 시험 시작 — 내 슬롯 {NetPlayer.Local.Slot} · 호스트 {NetPlayer.Local.IsHost} · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(x => x))}}}"); }
        float elapsed = Time.unscaledTime - seatTestBase;
        for (int i = testSlotRequests.Count - 1; i >= 0; i--)
        {
            if (elapsed < testSlotRequests[i].delay) continue;
            (float delay, int slot) request = testSlotRequests[i];
            testSlotRequests.RemoveAt(i);
            StartCoroutine(SeatRequestRoutine(request.slot));
        }
        if (testStartAtDelay >= 0f && elapsed >= testStartAtDelay && IsHost && !started)
        {
            testStartAtDelay = -1f;
            Debug.Log($"[자리] 시작 요청 — 내 슬롯 {NetPlayer.Local.Slot} · 접속 {NetPlayer.All.Count}명 · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(x => x))}}}");
            StartMatch();
        }
    }

    IEnumerator SeatRequestRoutine(int slot)
    {
        int before = NetPlayer.Local != null ? NetPlayer.Local.Slot : -1;
        Debug.Log($"[자리] 요청 {slot + 1}번 (지금 {before + 1}번)");
        RequestSlot(slot);
        yield return new WaitForSecondsRealtime(1.5f);
        if (NetPlayer.Local == null) yield break;
        Debug.Log($"[자리] 요청 {slot + 1} → 결과 슬롯 {NetPlayer.Local.Slot + 1}(번호 {NetPlayer.Local.Slot}) · {(NetPlayer.Local.Slot == slot ? "성공" : "실패/대기")} · LocalPlayerId {LocalPlayer.LocalPlayerId} · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(x => x))}}} · 전체 {string.Join(" ", NetPlayer.All.OrderBy(x => x.Slot).Select(x => $"{x.Slot}:{x.DisplayName}{(x.IsHost ? "(방장)" : "")}"))}");
    }

    // 게임 씬 진입 직후 한 번: 자리 시험 중이면 내 번호와 앉은 번호를 로그로(Player.log에서 「[자리] 게임 씬」 grep).
    void LogSeatAtGameScene()
    {
        if (seatTestBase < 0f) return;
        string seated = string.Join(",", PlayerContext.Occupied.Select(c => c.PlayerId).OrderBy(x => x));
        Debug.Log($"[자리] 게임 씬 진입: LocalPlayerId {LocalPlayer.LocalPlayerId} · PlayerContext 앉은 번호 {{{seated}}} · 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(x => x))}}} · 서버 {GameAuthority.IsServer}");
    }

    // ───────────── 대기실 조작 ─────────────

    public void SetReady(bool ready)
    {
        if (NetPlayer.Local != null) NetPlayer.Local.RPC_SetReady(ready);
    }

    /// <summary>대기실에서 빈 자리로 옮기기(사장님 10-06) — 호스트가 판정한다. 시작 뒤엔 호스트가 거절.</summary>
    public void RequestSlot(int slot)
    {
        if (NetPlayer.Local != null) NetPlayer.Local.RPC_RequestSlot(slot);
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
        && NetGameState.Instance != null
        && AllReady;

    /// <summary>시작 버튼이 왜 꺼져 있는지 — 버튼 아래에 그대로 보여 준다.</summary>
    public string StartBlockedReason
    {
        get
        {
            if (!IsHost) return "";
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
        NetLoadingHook.Show("방장 시작");
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
        EnemyDummy.ResetStoryHpReduction(); // 새 판 — R01G 퇴장 누적은 판마다 0부터
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
        if (testMainDelay >= 0f) { var mt = gameObject.AddComponent<NetMainTest>(); mt.startAt = testMainDelay; mt.shotPrefix = testMainShot; }
        if (testFinishRunDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestFinishRunAfter(testFinishRunDelay));
        LogSeatAtGameScene();
        if (camWispDelay >= 0f) StartCoroutine(CamWispAfter(camWispDelay));
        if (GameAuthority.IsServer && (testNoticesDelay >= 0f || testGapDelay >= 0f))
            PlayerNotification.Shown += (slot, msg, dur) => { if (noticesLogged++ < 80) Debug.Log($"[알림로그] → 슬롯 {slot}({dur:0}초): {msg}"); };
        if (testNoticesDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestNoticesAfter(testNoticesDelay, testNoticesShot));
        if (testGapDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestGapAfter(testGapDelay));
        if (testSecondSaveDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestSecondSaveAfter(testSecondSaveDelay));
        if (testItemsDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestItemsAfter(testItemsDelay));
        if (testUseItemDelay >= 0f && !GameAuthority.IsServer) StartCoroutine(TestUseItemAfter(testUseItemDelay));
        if (testCoinDelay >= 0f) StartCoroutine(TestCoinAfter(testCoinDelay, Mathf.Max(1, testCoinCount)));
        if (testDupes > 0 && GameAuthority.IsServer) SpawnDuplicateUnits(testDupes);
        if (testSameTypeDelay >= 0f) StartCoroutine(TestSameTypeAfter(testSameTypeDelay, testSameTypeShot));
        if (testMenuDelay >= 0f) StartCoroutine(TestMenuAfter(testMenuDelay, testMenuShot));
        if (testSelectDelay >= 0f) StartCoroutine(TestSelectAfter(testSelectDelay, testSelectDir));
        if (test0929Delay >= 0f) StartCoroutine(Test0929After(test0929Delay, test0929Dir));
        if (testVfxDelay >= 0f) StartCoroutine(TestVfxAfter(testVfxDelay, testVfxDir));
        if (testPortraitsDelay >= 0f && GameAuthority.IsServer) StartCoroutine(TestPortraitsAfter(testPortraitsDelay, testPortraitsDir));
        if (testPhase3Delay >= 0f) StartCoroutine(TestPhase3After(testPhase3Delay));
        if (testTraitUnits && GameAuthority.IsServer) SpawnTraitTestUnits();
        if (testTraitDelay >= 0f) StartCoroutine(TestTraitAfter(testTraitDelay));
        foreach (var (delay, text) in chatTests) if (delay >= 0f && !string.IsNullOrEmpty(text)) StartCoroutine(TestChatAfter(delay, text));
        foreach (float delay in gambleLabelDelays) if (delay >= 0f) StartCoroutine(GambleLabelsAfter(delay));
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

    IEnumerator TestCoinAfter(float seconds, int count)
    {
        yield return new WaitForSecondsRealtime(seconds);
        GamblingShop shop = FindObjectsByType<GamblingShop>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == LocalPlayer.LocalPlayerId);
        if (shop == null) { Debug.LogWarning("[MP] 동전 테스트: 내 도박소 없음"); yield break; }
        for (int i = 0; i < count; i++)
        {
            bool sent = NetCommands.RequestShopUse(shop, 0, default);
            Debug.Log($"[MP] 동전 테스트 {i + 1}/{count}: 슬롯 {LocalPlayer.LocalPlayerId} 「{shop.GetSlotView(0).label.Replace('\n', ' ')}」 → 보냄 {sent}");
            yield return new WaitForSecondsRealtime(0.8f);
        }
    }

    // 알림 묶음 확인(호스트). 알림 자체는 위 [알림로그] 구독이 전부 남긴다.
    IEnumerator TestNoticesAfter(float seconds, string shot)
    {
        yield return new WaitForSecondsRealtime(seconds);
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        RoundManager round = FindFirstObjectByType<RoundManager>();
        // 3. 보스 제한시간 칸 — 제한 끝 시각만 넣어 칸을 띄우고 찍는다(방장·친구 둘 다 -mpShotAt으로).
        typeof(RoundManager).GetField("bossLimitEndsAt", Any).SetValue(round, Time.time + 75.3f);
        Debug.Log("[알림테스트] 3. 보스 제한시간 칸 켬(75.3초)");
        yield return new WaitForSecondsRealtime(1.5f);
        yield return new WaitForEndOfFrame();
        if (!string.IsNullOrEmpty(shot)) ScreenCapture.CaptureScreenshot(shot);

        // 5. 조합 부족 — 지금 못 만드는 식 하나의 부족 목록.
        CombineSystem combine = FindFirstObjectByType<CombineSystem>();
        if (combine != null)
            for (int i = 0; combine.RecipeAt(i) != null; i++)
            {
                CombineRecipe recipe = combine.RecipeAt(i);
                if (combine.CanCombineNow(recipe)) continue;
                var lines = combine.DescribeShortage(recipe);
                Debug.Log($"[알림테스트] 5. 조합식 {i}({(recipe.result != null ? recipe.result.unitName : "?")}) 부족: {string.Join(" / ", lines)}");
                break;
            }

        // 6. 유닛도박 공지 — 중급도박을 자원 채워 실패·당첨이 둘 다 나올 때까지(최대 12번).
        GamblingShop shop = FindObjectsByType<GamblingShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 0);
        var field = typeof(GamblingShop).GetField("unitOptions", Any);
        var options = shop != null && field != null ? field.GetValue(shop) as System.Collections.Generic.List<GamblingOptionData> : null;
        GamblingOptionData mid = options?.FirstOrDefault(o => o != null && o.optionName == "중급도박");
        PlayerContext me = PlayerContext.Get(0);
        if (mid != null && me != null)
        {
            for (int i = 0; i < 12; i++)
            {
                me.ResourceWallet.Add(mid.costResourceType, mid.cost);
                if (mid.goldCost > 0) me.GoldWallet.Add(mid.goldCost);
                me.GamblingProgress?.ApplyReplicated(mid, 0, true, Mathf.Max(1, mid.stockMax), 0f, 0);
                bool ok = shop.TryRoll(mid, out string reason);
                if (!ok) { Debug.Log($"[알림테스트] 6. 중급도박 안 됨: {reason}"); break; }
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }
        else Debug.LogWarning("[알림테스트] 6. 중급도박 없음");

        // 2. 승리 문구.
        typeof(RoundManager).GetMethod("AnnounceClear", Any).Invoke(round, null);
        yield return new WaitForSecondsRealtime(0.5f);

        // 7. 제한시간 스토리(원작 와노쿠니 = Story13) — 바로 세워 경고 문구와 타이머 칸을 본다.
        StoryManager stories = StoryManager.Instance;
        StoryData limited = null;
        for (int i = 0; stories != null && stories.StoryAt(i) != null; i++) if (stories.StoryAt(i).timeLimitSeconds > 0f) { limited = stories.StoryAt(i); break; }
        if (limited != null)
        {
            typeof(StoryManager).GetMethod("Spawn", Any).Invoke(stories, new object[] { limited });
            Debug.Log($"[알림테스트] 7. 제한시간 스토리 {limited.storyName} 세움 — 남은 {stories.SecondsLeftInLimit:F0}초");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        yield return new WaitForEndOfFrame();
        if (!string.IsNullOrEmpty(shot)) ScreenCapture.CaptureScreenshot(shot.Replace(".png", "_story.png"));

        // 8. 스토리 완료 공지(첫 스토리 보상) · 1단계/3단계 크립(처치자 슬롯 1).
        RewardDistributor rewards = RewardDistributor.Instance;
        if (stories != null && stories.StoryAt(0) != null) rewards.GrantStoryReward(stories.StoryAt(0));
        foreach (EnemyData creep in catalog.enemies.Where(e => e != null && !string.IsNullOrEmpty(e.killAnnounceLabel)))
            rewards.GrantKillReward(creep, -1, round.CurrentRound, 1);
        // 11. 보스 처치(레인 0, 10라운드 보상).
        EnemyData boss10 = catalog.enemies.FirstOrDefault(e => e != null && e.isBoss && e.name.Contains("R10"));
        if (boss10 != null) rewards.GrantKillReward(boss10, 0, 10, 0);
        // 9. 퇴치 퀘스트 성공(슬롯 0).
        PirateQuestManager pirates = FindFirstObjectByType<PirateQuestManager>();
        var quest = ScriptableObject.CreateInstance<PirateQuestData>();
        quest.questName = "신림패거리"; quest.successGold = 1500;
        quest.successResources = new System.Collections.Generic.List<EnemyResourceReward> { new EnemyResourceReward { type = ResourceType.Wood, amount = 1 } };
        if (pirates != null) typeof(PirateQuestManager).GetMethod("HandleSuccess", Any).Invoke(pirates, new object[] { quest, 0 });
        // 10. 세이브 포인트(슬롯 1, 친구 화면).
        PlayerContext.Get(1)?.PersistentSave?.AddSessionPoints(2);
        // 12. 팁 — 네 번 불러 네 번째에 한 줄.
        var tip = typeof(RoundManager).GetMethod("MaybeShowTip", Any);
        for (int i = 0; i < 4; i++) tip.Invoke(round, null);
        // 13. 판매: 흔함 누적(3번) · 안흔함 두 번.
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        GameHud hudRef = FindFirstObjectByType<GameHud>();
        LaneMarker lane0 = LaneMarker.Get(0);
        UnitData commonSell = catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.sellRewardEveryNSells > 0);
        UnitData uncommonSell = catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.sellRewardWisp != null && u.sellRewardWispChance < 1f);
        if (spawner != null && hudRef != null && lane0 != null)
        {
            for (int i = 0; commonSell != null && i < 3; i++)
                hudRef.ExecuteSellOn(spawner.Spawn(commonSell, lane0.TakeSpawnPosition(commonSell), 0).GetComponent<Selectable>());
            for (int i = 0; uncommonSell != null && i < 2; i++)
                hudRef.ExecuteSellOn(spawner.Spawn(uncommonSell, lane0.TakeSpawnPosition(uncommonSell), 0).GetComponent<Selectable>());
        }
        Debug.Log($"[알림테스트] 13. 판매 — 흔함 {(commonSell != null ? commonSell.unitName : "없음")}×3 · 안흔함 {(uncommonSell != null ? uncommonSell.unitName : "없음")}×2");
        Debug.Log("[알림테스트] 끝");
    }

    // GAP 09-27 1·2·5·7·8 확인(호스트). 사사로운 필드·메서드는 리플렉션으로 — 테스트 전용, 게임 코드는 안 바꾼다.
    IEnumerator TestItemsAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        ItemData bundle = Catalog != null ? Catalog.items.Find(i => i != null && i.useKind == ItemUseKind.WispBundle) : null;
        ItemData ship = Catalog != null ? Catalog.items.Find(i => i != null && i.useKind == ItemUseKind.AncientShip) : null;
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            if (c.ItemInventory == null) continue;
            if (bundle != null) c.ItemInventory.Add(bundle);
            if (ship != null) c.ItemInventory.Add(ship);
            Debug.Log($"[아이템테스트] 호스트: 슬롯 {c.PlayerId}에게 위습꾸러미·고대의배 지급 — 보유 {c.ItemInventory.Items.Count}종");
        }
    }

    IEnumerator TestUseItemAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        PlayerContext me = PlayerContext.Get(LocalPlayer.LocalPlayerId);
        string Held() => me != null && me.ItemInventory != null ? string.Join(",", System.Linq.Enumerable.Select(me.ItemInventory.Items, i => i != null ? i.itemName : "-")) : "(없음)";
        Debug.Log($"[아이템테스트] 클라: 내 아이템 칸 = [{Held()}]");
        NetCommands.RequestUseItem(ItemUseKind.WispBundle);
        yield return new WaitForSecondsRealtime(3f);
        Debug.Log($"[아이템테스트] 클라: 사용 요청 3초 뒤 아이템 칸 = [{Held()}]");
    }

    IEnumerator TestSecondSaveAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        string Wisps(int slot)
        {
            int n = 0;
            foreach (Wisp w in Wisp.Active)
                if (w != null && !w.IsConsumed && w.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == slot) n++;
            return n.ToString();
        }
        foreach (PlayerContext c in PlayerContext.Occupied)
        {
            var d = c.PersistentSave != null ? c.PersistentSave.Data : null;
            Debug.Log($"[2차세이브테스트] 전 — 슬롯 {c.PlayerId}: 호스트가 든 세이브 클리어 {(d != null ? d.cumulativeClearCount : -1)}회·포인트 {(d != null ? d.cumulativePlayPoint : -1)} · 위습 {Wisps(c.PlayerId)}");
        }
        RewardDistributor.Instance?.GrantSecondSaveRewards();
        yield return new WaitForSecondsRealtime(1f);
        foreach (PlayerContext c in PlayerContext.Occupied) Debug.Log($"[2차세이브테스트] 후 — 슬롯 {c.PlayerId}: 위습 {Wisps(c.PlayerId)}");
    }

    IEnumerator TestGapAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        RoundManager round = FindFirstObjectByType<RoundManager>();
        PlayerContext me = PlayerContext.Get(0), friend = PlayerContext.Get(1);
        if (round == null || me == null || friend == null) { Debug.LogWarning("[GAP테스트] 준비물 없음"); yield break; }
        int shown = 0;
        System.Action<int, string, float> spy = (slot, msg, dur) => { if (shown++ < 40) Debug.Log($"[GAP테스트] 알림 → 슬롯 {slot}({dur:0}초): {msg}"); };
        PlayerNotification.Shown += spy;

        // 2. 데스 경고: 한계를 레인 0 적 수 근처로 잠깐 낮춘다.
        var threshold = typeof(RoundManager).GetField("enemyCountThreshold", Any);
        int original = (int)threshold.GetValue(round);
        int lane0 = EnemyDummy.CountInLane(0);
        Debug.Log($"[GAP테스트] 2. 데스 경고 — 레인0 적 {lane0}, 레인1 적 {EnemyDummy.CountInLane(1)}, 원래 한계 {original}");
        threshold.SetValue(round, lane0 + 4);   // 한계−8 이상 · 한계 미만 → 「위험」
        yield return new WaitForSecondsRealtime(1.5f);
        threshold.SetValue(round, Mathf.Max(1, lane0));   // 한계 이상 → 「N회 남음」
        yield return new WaitForSecondsRealtime(1.5f);
        threshold.SetValue(round, original);
        yield return new WaitForSecondsRealtime(0.5f);

        // 8. 위습 페널티: += 누적 · 차단 라운드 문구 · 해적단 실패 문구.
        round.BlockRoundRewardWisp(0, 2);
        round.BlockRoundRewardWisp(0, 2);
        Debug.Log($"[GAP테스트] 8. 차단 2+2 → 남은 {round.WispBlockRoundsRemaining(0)}(4여야 함)");
        var grant = typeof(RoundManager).GetMethod("GrantFlatRoundReward", Any);
        grant.Invoke(round, null);
        Debug.Log($"[GAP테스트] 8. 라운드 위습 한 번 → 슬롯0 남은 차단 {round.WispBlockRoundsRemaining(0)}(3이어야 함)");
        PirateQuestManager pirates = FindFirstObjectByType<PirateQuestManager>();
        var quest = ScriptableObject.CreateInstance<PirateQuestData>();
        quest.questName = "테스트 해적단";
        quest.failWispBlockRounds = 2;
        if (pirates != null) typeof(PirateQuestManager).GetMethod("HandleFailure", Any).Invoke(pirates, new object[] { quest, 1 });
        Debug.Log($"[GAP테스트] 8. 해적단 실패(슬롯 1) → 슬롯1 남은 차단 {round.WispBlockRoundsRemaining(1)}(2여야 함)");
        yield return new WaitForSecondsRealtime(0.5f);

        // 1. 패배 세이브 + 8의 「죽은 사람 제외」: 슬롯 1을 패배시키고 라운드 위습을 한 번 더.
        int points = friend.PersistentSave != null ? friend.PersistentSave.SessionPoints : -1;
        typeof(RoundManager).GetMethod("HandlePlayerDefeated", Any).Invoke(round, new object[] { 1, friend, "제한시간안에 보스를 잡지 못해 패배하였습니다.", 10f });
        Debug.Log($"[GAP테스트] 1. 슬롯 1 패배(세션 포인트 {points}) → 사망 {friend.IsDead}");
        int friendWisps = Wisp.Active.Count(w => w != null && w.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 1);
        int blockedBefore = round.WispBlockRoundsRemaining(1);
        grant.Invoke(round, null);
        int friendWispsAfter = Wisp.Active.Count(w => w != null && w.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 1);
        Debug.Log($"[GAP테스트] 8. 죽은 슬롯1 라운드 위습 {friendWisps}→{friendWispsAfter}(같아야 함) · 차단 {blockedBefore}→{round.WispBlockRoundsRemaining(1)}(같아야 함)");
        yield return new WaitForSecondsRealtime(0.5f);

        // 7. 창고: 희귀함 초과 거절 · C/V로 옮기면 목록에서 빠짐.
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        Warehouse house = me.Warehouse;
        LaneMarker lane = LaneMarker.Get(0);
        UnitData legend = catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.grade == UnitGrade.Legendary);
        UnitData uncommon = catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.grade == UnitGrade.Uncommon);
        if (spawner != null && house != null && lane != null && legend != null && uncommon != null)
        {
            GameObject big = spawner.Spawn(legend, lane.TakeSpawnPosition(legend), 0);
            Debug.Log($"[GAP테스트] 7. 전설 {legend.unitName} 창고 → {house.Store(big)}(false여야 함)");
            GameObject a = spawner.Spawn(uncommon, lane.TakeSpawnPosition(uncommon), 0);
            GameObject b = spawner.Spawn(uncommon, lane.TakeSpawnPosition(uncommon), 0);
            Debug.Log($"[GAP테스트] 7. 안흔함 {uncommon.unitName} 창고 → {house.Store(a)} · 들고 있음 {house.Contains(a)}");
            UnitCommands.SendToPen(new System.Collections.Generic.List<Selectable> { a.GetComponent<Selectable>() });
            Debug.Log($"[GAP테스트] 7. C(우리로) 뒤 창고에 남음 {house.Contains(a)}(false여야 함)");
            house.Store(a);
            UnitCommands.Gather(new System.Collections.Generic.List<Selectable> { b.GetComponent<Selectable>() });   // 필드의 b 자리로 같은 이름 전부
            Debug.Log($"[GAP테스트] 7. V(필드로 모으기) 뒤 창고에 남음 {house.Contains(a)}(false여야 함)");
        }
        else Debug.LogWarning("[GAP테스트] 7. 준비물 없음");
        yield return new WaitForSecondsRealtime(0.5f);

        // 5. 판매 위습 종류: 판매 위습 확률 1인 유닛을 팔아 새로 생긴 위습 이름을 본다 · 씬 unionWisp 현재값.
        UnitData seller = catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.sellRewardWisp != null && u.sellRewardWispChance >= 1f && u.grade != UnitGrade.Common);
        GameHud hud = FindFirstObjectByType<GameHud>();
        if (seller != null && spawner != null && hud != null)
        {
            var before = new System.Collections.Generic.HashSet<Wisp>(Wisp.Active);
            GameObject s = spawner.Spawn(seller, lane.TakeSpawnPosition(seller), 0);
            hud.ExecuteSellOn(s.GetComponent<Selectable>());
            yield return null;
            var fresh = Wisp.Active.Where(w => w != null && !before.Contains(w)).Select(w => w.Data != null ? w.Data.name : "?");
            Debug.Log($"[GAP테스트] 5. {seller.unitName}({seller.grade}) 판매 → 에셋 {seller.sellRewardWisp.name} · 새 위습 [{string.Join(", ", fresh)}]");
        }
        var union = typeof(RewardDistributor).GetField("unionWisp", Any)?.GetValue(RewardDistributor.Instance) as WispData;
        Debug.Log($"[GAP테스트] 5. 씬 RewardDistributor.unionWisp = {(union != null ? union.name : "없음")}(맵 재생성 전이면 Wisp_흔함)");

        PlayerNotification.Shown -= spy;
        Debug.Log("[GAP테스트] 끝");
    }

    void SpawnDuplicateUnits(int copies)
    {
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        var commons = catalog != null ? catalog.units.Where(u => u != null && u.prefab != null && u.grade == UnitGrade.Common).ToList() : null;
        if (spawner == null || commons == null || commons.Count < 2) return;
        int spawned = 0;
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            LaneMarker lane = LaneMarker.Get(context.PlayerId);
            for (int kind = 0; kind < 2; kind++)
            {
                UnitData data = commons[(context.PlayerId * 2 + kind) % commons.Count];
                for (int i = 0; i < copies; i++)
                {
                    Vector3 position = lane != null ? lane.TakeSpawnPosition(data) : context.transform.position;
                    if (spawner.Spawn(data, position, context.PlayerId) != null) spawned++;
                }
            }
        }
        Debug.Log($"[MP] -mpTestDupes: 같은 유닛 {copies}기씩 두 종류, 모두 {spawned}기");
    }

    // 클라 확인(PM 09-26): 겉모습(거울)에도 UnitIdentity·OwnedByPlayer·Selectable이 있어 같은 종류 선택이 되는가.
    // 마우스를 흉내 내지 않고 SelectionManager의 같은 함수(더블클릭·Ctrl+클릭이 부르는 SelectSameTypeOnScreen)를 직접 부른다.
    IEnumerator TestSameTypeAfter(float seconds, string path)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SelectionManager selection = FindFirstObjectByType<SelectionManager>();
        var mine = UnitIdentity.Active.Where(u => u != null && u.Data != null && u.OwnerId == LocalPlayer.LocalPlayerId).ToList();
        var seedIdentity = mine.GroupBy(u => u.Data).OrderByDescending(g => g.Count()).Select(g => g.First()).FirstOrDefault();
        if (selection == null || seedIdentity == null || !seedIdentity.TryGetComponent(out Selectable seed))
        {
            Debug.LogWarning($"[MP] 같은 종류 선택 테스트: 준비물 없음(선택기 {selection != null}, 내 유닛 {mine.Count})");
            yield break;
        }
        int sameOwned = mine.Count(u => u.Data == seedIdentity.Data);
        var method = typeof(SelectionManager).GetMethod("SelectSameTypeOnScreen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null) { Debug.LogWarning("[MP] 같은 종류 선택 테스트: SelectSameTypeOnScreen을 못 찾음"); yield break; }
        method.Invoke(selection, new object[] { seed, seedIdentity.Data });
        Debug.Log($"[MP] 같은 종류 선택 테스트(IsServer={GameAuthority.IsServer}, 슬롯 {LocalPlayer.LocalPlayerId}): {seedIdentity.Data.unitName} — " +
                  $"내 것 {sameOwned}기 중 {selection.Selected.Count}기 선택{(seed.name.Contains("거울") ? " (거울)" : "")}");
        yield return new WaitForSecondsRealtime(1f);
        yield return new WaitForEndOfFrame();
        if (!string.IsNullOrEmpty(path)) ScreenCapture.CaptureScreenshot(path);
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

        // 조합: 내 겉모습 유닛마다 그 유닛으로 시작하는 식 중 지금 되는 것을 요청(실제 [조합] 버튼과 같은 조건). 마지막에 일부러
        // 틀린 요청(그 유닛으로 시작하지 않는 식) 하나를 보내 호스트 검증이 거절하는지도 본다.
        CombineSystem system = FindFirstObjectByType<CombineSystem>();
        int combos = 0;
        Selectable anyUnit = null;
        if (system != null)
        {
            foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
            {
                if (combos >= 3) break;
                if (e == null || e.Object == null || !e.Object.IsValid) continue;   // 앞 조합에 재료로 쓰여 사라진 거울
                if (e.EntityKind != NetEntityKind.Unit || e.Owner != me || e.Visual == null) continue;
                if (!e.Visual.TryGetComponent(out UnitIdentity uid) || uid.Data == null || !e.Visual.TryGetComponent(out Selectable sel)) continue;
                anyUnit = sel;
                foreach (CombineRecipe recipe in system.GetRecipesStartingWith(uid.Data))
                {
                    if (!system.CanCombineNow(recipe)) continue;
                    NetCommands.RequestCombine(system, recipe, sel);
                    Debug.Log($"[MP] 테스트 조합: {uid.Data.unitName}로 조합식 {system.IndexOfRecipe(recipe)} 요청");
                    combos++;
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                }
            }
            anyUnit = null;   // 앞 조합에 재료로 쓰였을 수 있다 — 지금 살아 있는 내 유닛을 새로 고른다
            foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
                if (e != null && e.Object != null && e.Object.IsValid && e.EntityKind == NetEntityKind.Unit && e.Owner == me
                    && e.Visual != null && e.Visual.TryGetComponent(out anyUnit)) break;
            if (anyUnit != null && anyUnit.TryGetComponent(out UnitIdentity anyId))
            {
                var ownList = system.GetRecipesStartingWith(anyId.Data);
                for (int i = 0; system.RecipeAt(i) != null; i++)
                {
                    if (ownList != null && ownList.Contains(system.RecipeAt(i))) continue;
                    NetCommands.RequestCombine(system, system.RecipeAt(i), anyUnit);
                    Debug.Log($"[MP] 테스트 조합(틀린 요청): {anyId.Data.unitName}로 조합식 {i} — 거절되어야 함");
                    break;
                }
                yield return new WaitForSecondsRealtime(1f);
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

    void SpawnTraitTestUnits()
    {
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        UnitData withTrait = catalog != null ? catalog.units.FirstOrDefault(u => u != null && u.prefab != null && u.trait != null
            && !u.trait.targetsOtherUnit && !u.trait.isTransformType && u.trait.costTraitPoints <= 1) : null;
        if (spawner == null || withTrait == null) { Debug.LogWarning("[MP] -mpTestTraitUnits: 조건에 맞는 유닛이 없습니다."); return; }
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            LaneMarker lane = LaneMarker.Get(context.PlayerId);
            spawner.Spawn(withTrait, lane != null ? lane.TakeSpawnPosition(withTrait) : context.transform.position, context.PlayerId);
        }
        Debug.Log($"[MP] -mpTestTraitUnits: {withTrait.unitName}(특성 {withTrait.trait.name}, 비용 {withTrait.trait.costTraitPoints}) 슬롯마다 1기");
    }

    IEnumerator TestTraitAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        UnitUpgrades upgrades = PlayerContext.Local != null ? PlayerContext.Local.UnitUpgrades : null;
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e.EntityKind != NetEntityKind.Unit || e.Owner != LocalPlayer.LocalPlayerId || e.Visual == null) continue;
            if (!e.Visual.TryGetComponent(out UnitIdentity id) || id.Data == null || id.Data.trait == null || id.Data.trait.targetsOtherUnit) continue;
            UnitTraitData trait = id.Data.trait;
            Debug.Log($"[MP] 테스트 특성: {id.Data.unitName} {trait.name} — 요청 전 포인트 {upgrades?.TraitPoints} 해금 {upgrades?.IsUnlocked(trait)}");
            NetCommands.RequestHudUnitAction(NetHudAction.Trait, e.Visual.GetComponent<Selectable>(), 0);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[MP] 테스트 특성: 요청 뒤 포인트 {upgrades?.TraitPoints} 해금 {upgrades?.IsUnlocked(trait)}");
            yield break;
        }
        Debug.Log($"[MP] 테스트 특성: 특성 있는 내 유닛이 없습니다(포인트 {upgrades?.TraitPoints}).");
    }

    // 테스트 전용(-mpTestChat 초 코드): 채팅 코드를 요청으로 보낸다.
    IEnumerator TestChatAfter(float seconds, string code)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (GameAuthority.IsServer) PlayerChat.HandleOnAuthority(LocalPlayer.LocalPlayerId, NetPlayer.Local != null ? NetPlayer.Local.DisplayName : "나", code, out _);
        else NetCommands.RequestChat(code);
        Debug.Log($"[MP] 테스트 채팅 「{code}」 보냄");
    }

    IEnumerator GambleLabelsAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        foreach (GamblingShop shop in FindObjectsByType<GamblingShop>(FindObjectsSortMode.None))
        {
            if (!shop.TryGetComponent(out OwnedByPlayer owner) || !PlayerContext.GetOccupied(owner.OwnerId)) continue;
            if (!GameAuthority.IsServer && owner.OwnerId != LocalPlayer.LocalPlayerId) continue;   // 호스트는 앉은 슬롯 전부, 클라는 내 것
            var labels = new System.Collections.Generic.List<string>();
            for (int i = 0; i < shop.SlotCount; i++)
            {
                LaneShopSlotView view = shop.GetSlotView(i);
                if (!string.IsNullOrEmpty(view.label)) labels.Add($"[{i}]{view.label.Replace("\n", " / ")}{(view.available ? "" : "(흐림)")}");
            }
            Debug.Log($"[MP] 도박소 칸({(GameAuthority.IsServer ? "호스트" : "클라")} 슬롯 {owner.OwnerId}): " + string.Join(" | ", labels));
        }
    }

    // 테스트 전용: 판 끝 세이브(RoundManager.FinishPersistentSave와 같은 호출)를 지금 돌린다.
    IEnumerator TestFinishRunAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            if (context.IsDead || context.PersistentSave == null) continue;
            context.PersistentSave.AddSessionPoints(5);
            context.PersistentSave.FinishRun(false);
            Debug.Log($"[MP] 테스트 판 끝: 슬롯 {context.PlayerId} +5점 FinishRun");
        }
    }

    IEnumerator CamWispAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
        foreach (NetEntity e in FindObjectsByType<NetEntity>(FindObjectsSortMode.None))
        {
            if (e == null || e.Object == null || !e.Object.IsValid || e.EntityKind != NetEntityKind.Wisp) continue;
            if (cam != null) cam.MoveTo(e.transform.position);
            Debug.Log($"[MP] 카메라 → 위습 {e.transform.position}");
            yield break;
        }
    }

    // 테스트 전용: 초상화(PortraitStage) 확인 — 유닛은 선택, 적은 살펴보기로 정보칸에 띄우고 캡처한다.
    IEnumerator TestPortraitsAfter(float seconds, string dir)
    {
        yield return new WaitForSecondsRealtime(seconds);
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        SelectionManager selection = FindFirstObjectByType<SelectionManager>();
        LaneMarker lane = LaneMarker.Get(0);
        if (spawner == null || selection == null || lane == null || catalog == null) { Debug.LogWarning("[MP] 초상 테스트: 준비물 없음"); yield break; }

        var targets = new System.Collections.Generic.List<(string label, GameObject go, bool isEnemy)>();
        UnitData Find(System.Func<UnitData, bool> pick) => catalog.units.FirstOrDefault(u => u != null && u.prefab != null && pick(u));
        void AddUnit(string label, UnitData data)
        {
            if (data == null) { Debug.LogWarning($"[MP] 초상 테스트: {label} 유닛 없음"); return; }
            GameObject go = spawner.Spawn(data, lane.TakeSpawnPosition(data), 0);
            if (go != null) targets.Add(($"{label}_{data.name}", go, false));
        }
        AddUnit("1흔함", Find(u => u.grade == UnitGrade.Common));
        AddUnit("2특별함", Find(u => u.name.Contains("특별함_노건완")) ?? Find(u => u.grade == UnitGrade.Special));
        AddUnit("3재규어", Find(u => u.name.Contains("안흔함_강재규")));

        void AddEnemy(string label, EnemyData data, float dx)
        {
            if (data == null || data.prefab == null) { Debug.LogWarning($"[MP] 초상 테스트: {label} 적 없음"); return; }
            GameObject go = Instantiate(data.prefab, lane.LaneCenter + new Vector3(dx, 0f, 0f), Quaternion.identity);
            if (go.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
            if (go.TryGetComponent(out EnemyDummy dummy)) dummy.Initialize(data);
            targets.Add(($"{label}_{data.name}", go, true));
        }
        AddEnemy("4적", catalog.enemies.FirstOrDefault(e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R01")), -60f);
        AddEnemy("5매머드", catalog.enemies.FirstOrDefault(e => e != null && e.name.Contains("양문호") && !e.isBoss), 0f);
        AddEnemy("6보스", catalog.enemies.FirstOrDefault(e => e != null && e.isBoss && e.prefab != null && e.name.Contains("R10")), 60f);

        yield return new WaitForSecondsRealtime(1f);
        foreach (var (label, go, isEnemy) in targets)
        {
            if (go == null) continue;
            if (isEnemy) { selection.ClearSelection(); InspectTarget.Set(go); }
            else { InspectTarget.Clear(); if (go.TryGetComponent(out Selectable s)) selection.SelectOnly(s); }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            string path = System.IO.Path.Combine(dir, $"portrait_{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[MP] 초상 캡처: {label} → {path}");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // 프레임 영향: 초상 켬(유닛 하나 선택) 8초 평균 vs 끔(선택 없음) 8초 평균
        InspectTarget.Clear();
        GameObject any = targets.Count > 0 ? targets[0].go : null;
        if (any != null && any.TryGetComponent(out Selectable sel)) selection.SelectOnly(sel);
        float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount;
        yield return new WaitForSecondsRealtime(8f);
        float onFps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0);
        selection.ClearSelection();
        t0 = Time.realtimeSinceStartup; f0 = Time.frameCount;
        yield return new WaitForSecondsRealtime(8f);
        float offFps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0);
        Debug.Log($"[MP] 초상 FPS: 켬 {onFps:F1} · 끔 {offFps:F1}");
    }

    // 친구 화면 초상 확인(PM 09-26): 클라는 겉모습(거울)을, 호스트는 실물을 고른다 — 사람이 클릭한 것과 같은 SelectOnly·InspectTarget 길.
    IEnumerator TestSelectAfter(float seconds, string dir)
    {
        yield return new WaitForSecondsRealtime(seconds);
        SelectionManager selection = FindFirstObjectByType<SelectionManager>();
        if (selection == null) { Debug.LogWarning("[MP] 선택 테스트: SelectionManager 없음"); yield break; }

        var targets = new System.Collections.Generic.List<(string label, GameObject go, bool isEnemy)>();
        var entities = FindObjectsByType<NetEntity>(FindObjectsSortMode.None).OrderBy(e => e.Object.Id.Raw).ToList();
        int units = 0, enemies = 0;
        foreach (NetEntity e in entities)
        {
            if (e.Owner != 1) continue;
            GameObject body = GameAuthority.IsServer ? e.Real : e.Visual;
            if (body == null) continue;
            if (e.EntityKind == NetEntityKind.Unit && units < 4) { units++; targets.Add(($"id{e.Object.Id.Raw}_{body.name}", body, false)); }
            else if (e.EntityKind == NetEntityKind.Enemy && enemies < 2) { enemies++; targets.Add(($"id{e.Object.Id.Raw}_{body.name}", body, true)); }
        }
        targets = targets.OrderBy(t => t.isEnemy ? 0 : 1).ToList();   // 적 먼저 — 유닛은 우리에 있어 안 죽지만 적은 곧 죽는다
        Debug.Log($"[MP] 선택 테스트: 슬롯 {LocalPlayer.LocalPlayerId}, 대상 {targets.Count}개(IsServer={GameAuthority.IsServer})");

        foreach (var (label, go, isEnemy) in targets)
        {
            if (go == null) continue;
            if (isEnemy) { selection.ClearSelection(); InspectTarget.Set(go); }
            else
            {
                InspectTarget.Clear();
                if (go.TryGetComponent(out Selectable s)) selection.SelectOnly(s);
                // 방장은 남의 유닛을 못 고른다(정보칸 빈칸) — 같은 개체의 값을 로그로 나란히 남긴다: 방장=실물 UnitAttacker, 친구=거울이 받은 값.
                NetEntity m = go.GetComponentInParent<NetEntity>();
                if (go.TryGetComponent(out UnitAttacker a))
                    Debug.Log($"[MP] 선택 값 {label}: 공격력 {a.AttackDamage:F0} · 사거리 {a.AttackRange:F1} · 공격속도 {(a.AttackInterval > 0f ? 1f / a.AttackInterval : 0f):F2}/s (실물)");
                else if (m != null)
                    Debug.Log($"[MP] 선택 값 {label}: 공격력 {m.AttackDamage:F0} · 사거리 {m.AttackRange:F1} · 공격속도 {(m.AttackInterval > 0f ? 1f / m.AttackInterval : 0f):F2}/s (거울)");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            string safe = string.Concat(label.Split(System.IO.Path.GetInvalidFileNameChars()));
            string path = System.IO.Path.Combine(dir, $"select_{safe}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[MP] 선택 캡처: {label} → {path}");
            yield return new WaitForSecondsRealtime(0.5f);
        }
        InspectTarget.Clear();
        selection.ClearSelection();
    }

    // 게임 중 [메뉴] → 나가기 확인 창을 띄우고 찍는다(누르지는 않는다 — 나가기는 -mpLeave로 따로).
    IEnumerator TestMenuAfter(float seconds, string path)
    {
        yield return new WaitForSecondsRealtime(seconds);
        GameHud hud = FindFirstObjectByType<GameHud>();
        if (hud != null) hud.ShowGameMenuConfirm();
        yield return new WaitForSecondsRealtime(0.5f);
        yield return new WaitForEndOfFrame();
        if (!string.IsNullOrEmpty(path)) ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[MP] 메뉴 테스트: 확인 창 열고 캡처 → {path}");
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

    // 테스트 전용(-mpTest0929 <초> <폴더>): 09-29 배포판 피드백 넷을 두 창에서 확인한다.
    //   ① 시작 특별함 1기(내 것 이름·표시 이름) ② 친구가 「흔함」 강화 2번 → 친구 흔함 유닛 공격력(거울 값) 전후
    //   ③ 방장이 보스 한 마리를 친구 레인에 세움 → 양쪽 스케일 ④ 친구 A 공격 대기 → 커서 상태(캡처엔 커서가 안 찍혀 로그로).
    IEnumerator Test0929After(float seconds, string dir)
    {
        yield return new WaitForSecondsRealtime(seconds);
        int me = LocalPlayer.LocalPlayerId;
        string side = GameAuthority.IsServer ? "host" : "client";
        RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
        SelectionManager selection = FindFirstObjectByType<SelectionManager>();
        GameObject Body(NetEntity e) => GameAuthority.IsServer ? e.Real : e.Visual;
        System.Collections.Generic.List<NetEntity> Live() => FindObjectsByType<NetEntity>(FindObjectsSortMode.None)
            .Where(e => e != null && e.Object != null && e.Object.IsValid).OrderBy(e => e.Object.Id.Raw).ToList();
        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            string path = System.IO.Path.Combine(dir, $"{side}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[0929] 캡처 {path}");
        }

        // ① 시작 특별함
        NetEntity mySpecial = null;
        foreach (NetEntity e in Live())
        {
            if (e.EntityKind != NetEntityKind.Unit || Body(e) == null || !Body(e).TryGetComponent(out UnitIdentity id) || id.Data == null) continue;
            if (id.Data.grade != UnitGrade.Special) continue;
            Debug.Log($"[0929] ① 특별함 p{e.Owner}: {id.Data.unitName} → 표시 「{id.Data.DisplayName}」 ({side})");
            if (e.Owner == me && mySpecial == null) mySpecial = e;
        }
        if (mySpecial != null)
        {
            if (cam != null) cam.MoveTo(mySpecial.transform.position);
            if (selection != null && Body(mySpecial).TryGetComponent(out Selectable ss)) selection.SelectOnly(ss);
            yield return Shot("1_special");
        }
        else Debug.LogWarning($"[0929] ① 내(p{me}) 특별함이 없다 ({side})");

        // ② 흔함 강화 — 친구 흔함 유닛 하나를 정해 양쪽이 같은 개체 값을 찍는다
        NetEntity common = Live().FirstOrDefault(e => e.EntityKind == NetEntityKind.Unit && e.Owner == 1 && Body(e) != null
            && Body(e).TryGetComponent(out UnitIdentity cid) && cid.Data != null && cid.Data.grade == UnitGrade.Common);
        float Dmg(NetEntity e) => e == null ? -1f : (GameAuthority.IsServer && e.Real != null && e.Real.TryGetComponent(out UnitAttacker ra) ? ra.AttackDamage : e.AttackDamage);
        Debug.Log($"[0929] ② 전: 친구 흔함 {(common != null ? common.Object.Id.Raw.ToString() : "없음")} 공격력 {Dmg(common):F0} ({side})");
        if (!GameAuthority.IsServer && me == 1)
        {
            bool bought = false;
            foreach (UnitUpgradeShop shop in FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None))
            {
                if (!shop.TryGetComponent(out OwnedByPlayer so) || so.OwnerId != me) continue;
                for (int s = 0; s < shop.SlotCount; s++)
                {
                    LaneShopSlotView v = shop.GetSlotView(s);
                    if (string.IsNullOrEmpty(v.label) || !v.label.Contains("흔함")) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        bool sent = NetCommands.RequestShopUse(shop, s, default);
                        Debug.Log($"[0929] ② 친구 강화 요청 {k + 1}: {shop.name} {s}번 「{v.label.Replace('\n', ' ')}」 → 보냄 {sent}");
                        yield return new WaitForSecondsRealtime(1f);
                    }
                    Debug.Log($"[0929] ② 강화 후 라벨: 「{shop.GetSlotView(s).label.Replace('\n', ' ')}」");
                    bought = true;
                    break;
                }
                if (bought) break;
            }
            if (!bought) Debug.LogWarning("[0929] ② 친구 「흔함」 강화 칸을 못 찾았다");
        }
        else yield return new WaitForSecondsRealtime(2f);
        yield return new WaitForSecondsRealtime(1.5f);
        Debug.Log($"[0929] ② 후: 친구 흔함 공격력 {Dmg(common):F0} ({side})");
        if (common != null && me == 1 && selection != null && Body(common).TryGetComponent(out Selectable cs))
        {
            if (cam != null) cam.MoveTo(common.transform.position);
            selection.SelectOnly(cs);
            yield return Shot("2_common_info");
        }

        // ③ 보스 스케일 — 방장이 친구 레인(1)에 보스를 세운다(SpawnSideBoss도 SpawnEnemyInternal을 지나 1.6배가 걸린다)
        if (GameAuthority.IsServer && catalog != null)
        {
            EnemyData boss = catalog.enemies.FirstOrDefault(d => d != null && d.isBoss && d.prefab != null);
            WaveSpawner spawner = FindFirstObjectByType<WaveSpawner>();
            GameObject b = spawner != null && boss != null ? spawner.SpawnSideBoss(boss, 1) : null;
            Debug.Log($"[0929] ③ 방장 보스 소환 {(boss != null ? boss.enemyName : "없음")} → {(b != null ? b.transform.localScale.ToString("F2") : "실패")} (프리팹 {(boss != null ? boss.prefab.transform.localScale.ToString("F2") : "-")})");
        }
        yield return new WaitForSecondsRealtime(3f);
        NetEntity bossEntity = null;
        foreach (NetEntity e in Live())
        {
            if (e.EntityKind != NetEntityKind.Enemy || Body(e) == null || !Body(e).TryGetComponent(out EnemyDummy ed) || ed.Data == null) continue;
            Renderer r = Body(e).GetComponentInChildren<Renderer>();
            string h = r != null ? r.bounds.size.y.ToString("F2") : "-";
            if (ed.Data.isBoss) { bossEntity = e; Debug.Log($"[0929] ③ 보스 {ed.Data.enemyName}: 루트 {e.transform.localScale:F2} · 월드 {Body(e).transform.lossyScale:F2} · 렌더 키 {h} · 프리팹 {ed.Data.prefab.transform.localScale:F2} ({side})"); }
        }
        if (bossEntity != null) { if (cam != null) cam.MoveTo(bossEntity.transform.position); yield return Shot("3_boss"); }
        else Debug.LogWarning($"[0929] ③ 보스가 안 보인다 ({side})");

        // ④ A 공격 대기 커서(친구만)
        if (!GameAuthority.IsServer && selection != null && common != null && Body(common) != null && Body(common).TryGetComponent(out Selectable ac))
        {
            selection.SelectOnly(ac);
            selection.BeginAttackTargeting();
            yield return null; yield return null;
            var f = typeof(SelectionManager).GetField("attackCursorShown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Debug.Log($"[0929] ④ 친구 A 대기: IsAttackTargeting {selection.IsAttackTargeting} · 빨간 칼 커서 {(f != null ? f.GetValue(selection) : "필드없음")}");
            yield return Shot("4_attack_wait");
        }
        Debug.Log($"[0929] 끝 ({side})");
    }

    // 테스트 전용(-mpTestVfx <초> <폴더>): 스킬 이펙트 복제(09-29). 방장이 친구 레인(1)의 적 하나에 차례로
    //   ① 흔함 시전자로 스킬 피해·스턴(등급 게이트 — 양쪽 다 안 떠야 함) ② 특별함 이상(게이트 없음)으로 스킬 피해·마법 피해·방깎·스턴·이감을 건다.
    // 양쪽이 같은 적의 스턴·이감 이펙트 유무와 받은 한 번짜리 이펙트 수를 찍고, 친구는 그 적을 보고 캡처한다.
    IEnumerator TestVfxAfter(float seconds, string dir)
    {
        yield return new WaitForSecondsRealtime(seconds);
        string side = GameAuthority.IsServer ? "host" : "client";
        NetEntity PickEnemy() => FindObjectsByType<NetEntity>(FindObjectsSortMode.None)
            .Where(e => e != null && e.Object != null && e.Object.IsValid && e.EntityKind == NetEntityKind.Enemy)
            .OrderBy(e => e.Object.Id.Raw).FirstOrDefault(e => (GameAuthority.IsServer ? e.Real : e.Visual) != null
                && (GameAuthority.IsServer ? e.Real : e.Visual).TryGetComponent(out EnemyDummy d) && d.LaneIndex == 1);
        NetEntity target = PickEnemy();
        if (target == null) { Debug.LogWarning($"[VFX테스트] 친구 레인 적이 없다 ({side})"); yield break; }
        uint id = target.Object.Id.Raw;
        RtsCameraController cam = FindFirstObjectByType<RtsCameraController>();
        if (cam != null) cam.MoveTo(target.transform.position);
        // 붙은 이펙트는 1.2.2부터 대상 자식이 아니라 SkillVfx 루트 밑 러너다 — 대상을 따라가는 러너와 그 입자를 센다(방장=Real·친구=Visual 같은 방식).
        string Runners()
        {
            GameObject body = GameAuthority.IsServer ? target.Real : target.Visual;
            (int runners, int playing, int particles) = SkillVfx.AttachedTo(body != null ? body.transform : null);
            return $"러너 {runners} · 재생 겹 {playing} · 입자 {particles}";
        }
        string State() => $"StunVfx {target.StunVfx} · SlowVfx {target.SlowVfx} · 클라가 받은 이펙트 {NetGameState.ReceivedVfx} · {Runners()}";

        if (GameAuthority.IsServer && target.Real.TryGetComponent(out EnemyDummy real))
        {
            UnitData common = catalog != null ? catalog.units.FirstOrDefault(u => u != null && u.grade == UnitGrade.Common) : null;
            bool before = SkillVfx.BeginCast(common);
            real.TakeDamage(1f, DamageType.AD, AttackType.Normal, 0, 0f, true);
            real.AddFreeze();
            SkillVfx.EndCast(before);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[VFX테스트] ① 흔함 시전(게이트) 적 {id}: 스턴 붙음 {real.HasStunVfx} · {Runners()} (False·러너 0·입자 0이어야)");
            yield return new WaitForSecondsRealtime(1f);
            real.RemoveFreeze();
            yield return new WaitForSecondsRealtime(0.5f);

            real.TakeDamage(1f, DamageType.AD, AttackType.Normal, 0, 0f, true);
            yield return new WaitForSecondsRealtime(0.3f);
            real.TakeDamage(1f, DamageType.AP, AttackType.Spells, 0, 0f, true);
            real.AddArmorShred(1f);
            real.AddFreeze();
            real.AddSlow(0.5f);
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[VFX테스트] ② 특별함 이상 시전 적 {id}: 스턴 {real.HasStunVfx} · 이감 {real.HasSlowVfx} · {Runners()} (둘 다 True·러너 2·입자 >0)");
            yield return new WaitForSecondsRealtime(3f);
            real.RemoveFreeze();
            real.RemoveSlow(0.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log($"[VFX테스트] ③ 해제 적 {id}: 스턴 {real.HasStunVfx} · 이감 {real.HasSlowVfx} · {Runners()} (둘 다 False·러너 0·입자 0)");
        }
        else
        {
            // 방장 순서에 맞춰 찍는다: ① 게이트 1초 뒤 · ② 1초 뒤(붙어 있어야) · ③ 해제 1.5초 뒤
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[VFX테스트] ① 게이트 중 친구 적 {id}: {State()} (Stun False·받은 0·러너 0·입자 0이어야)");
            yield return new WaitForSecondsRealtime(2.8f);
            Debug.Log($"[VFX테스트] ② 친구 적 {id}: {State()} (Stun·Slow True, 받은 ≥3·러너 2·입자 >0)");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "client_vfx_stun_slow.png"));
            yield return new WaitForSecondsRealtime(4.5f);
            Debug.Log($"[VFX테스트] ③ 해제 뒤 친구 적 {id}: {State()} (Stun·Slow False·러너 0·입자 0)");
        }
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
