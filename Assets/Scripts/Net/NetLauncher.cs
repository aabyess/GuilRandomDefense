using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetBoot 씬의 접속 창구. [호스트 만들기]/[참가] → 로비 → 호스트 [시작] → 게임 씬.
///
/// ⚠️ 게임 씬(SampleScene)을 직접 Play하면 이 오브젝트가 없다 → 러너 없음 → GameAuthority.Provider null
///    → 지금과 똑같은 싱글. 멀티 설계의 제1 조건이다(gameshot·ClaudeBridge·판 측정 무영향).
///
/// 명령줄(빌드 두 개를 사람 손 없이 붙여 보는 용도):
///   -mpHost | -mpJoin        바로 호스트/참가
///   -mpSession 이름           방 이름(기본 "grd-test")
///   -mpAutoStart N           호스트가 N명이 모이면 스스로 시작
///   -mpShot 초 경로           게임 씬 진입 뒤 그 초에 화면 캡처
///   -mpQuit 초               게임 씬 진입 뒤 그 초에 종료
/// </summary>
public class NetLauncher : MonoBehaviour
{
    const string DefaultSession = "grd-test";

    [SerializeField] NetworkObject playerPrefab;
    [SerializeField] int gameSceneBuildIndex = 1;

    NetworkRunner runner;
    NetSession session;
    string sessionName = DefaultSession;
    string status = "";
    bool starting;
    bool wasRunning;

    int autoStartCount;
    float shotDelay = -1f;
    string shotPath;
    float quitDelay = -1f;

    public static NetLauncher Instance { get; private set; }

    public NetworkObject PlayerPrefab => playerPrefab;

#if UNITY_EDITOR
    /// <summary>NetSetup(에디터 도구) 전용.</summary>
    public void EditorSetup(NetworkObject prefab, int gameSceneIndex)
    {
        playerPrefab = prefab;
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
        ReadCommandLine();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void ReadCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string Next(int i) => i + 1 < args.Length ? args[i + 1] : null;

        bool host = false, join = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-mpHost": host = true; break;
                case "-mpJoin": join = true; break;
                case "-mpSession": sessionName = Next(i) ?? sessionName; break;
                case "-mpAutoStart": int.TryParse(Next(i), out autoStartCount); break;
                case "-mpShot":
                    float.TryParse(Next(i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out shotDelay);
                    shotPath = i + 2 < args.Length ? args[i + 2] : null;
                    break;
                case "-mpQuit":
                    float.TryParse(Next(i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out quitDelay);
                    break;
            }
        }

        if (host) _ = StartRunner(GameMode.Host);
        else if (join) _ = StartRunner(GameMode.Client);
    }

    async Task StartRunner(GameMode mode)
    {
        if (starting || runner != null) return;
        starting = true;
        status = mode == GameMode.Host ? "방을 만드는 중…" : "참가하는 중…";

        GameObject go = new GameObject("NetworkRunner");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        session = go.AddComponent<NetSession>();
        session.SetPlayerPrefab(playerPrefab);
        NetworkSceneManagerDefault sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

        // 좌석은 NetPlayer가 생길 때 채워진다. 러너를 띄우는 순간부터 「네트 판」이다.
        MatchConfig.Reset();
        MatchConfig.Active = true;

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = NetSession.MaxSlots,
            SceneManager = sceneManager,
        });

        starting = false;

        if (!result.Ok)
        {
            status = $"실패: {result.ShutdownReason} {result.ErrorMessage}";
            Debug.LogWarning($"[MP] StartGame 실패({mode}, 방 {sessionName}): {result.ShutdownReason} {result.ErrorMessage}");
            Cleanup();
            return;
        }

        GameAuthority.Provider = new FusionAuthorityProvider(runner);
        wasRunning = true;
        status = mode == GameMode.Host ? "방을 열었습니다. 친구를 기다리는 중" : "참가했습니다. 호스트의 시작을 기다리는 중";
        Debug.Log($"[MP] StartGame 성공: {mode}, 방 {sessionName}, IsServer={runner.IsServer}, 지역 {runner.SessionInfo.Region}");
    }

    void Update()
    {
        // 호스트가 나가거나 연결이 끊기면 러너가 스스로 멈춘다 — 싱글 상태로 되돌리고 NetBoot로.
        if (wasRunning && (runner == null || !runner.IsRunning))
        {
            Debug.Log("[MP] 러너가 멈췄습니다 — 싱글 상태로 되돌리고 NetBoot로 돌아갑니다.");
            Cleanup();
            status = "연결이 끊겼습니다.";
            if (SceneManager.GetActiveScene().buildIndex != 0) SceneManager.LoadScene(0);
            return;
        }

        if (autoStartCount > 0 && CanStartMatch && session.PlayerCount >= autoStartCount)
        {
            // 방금 들어온 접속자의 NetPlayer 복제가 클라에 닿을 틈을 준다(DontDestroyOnLoad라 늦어도 살아남지만,
            // 좌석이 씬 로드 전에 채워져 있어야 PlayerContext.Awake가 본다).
            autoStartCount = 0;
            StartCoroutine(StartMatchAfter(1f));
        }
    }

    bool CanStartMatch => runner != null && runner.IsRunning && runner.IsServer && session != null && !session.MatchStarted;

    IEnumerator StartMatchAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        StartMatch();
    }

    void StartMatch()
    {
        if (!CanStartMatch) return;

        session.MatchStarted = true;
        runner.SessionInfo.IsOpen = false;
        runner.SessionInfo.IsVisible = false;

        Debug.Log($"[MP] 판 시작: 좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(s => s))}}} → 씬 {gameSceneBuildIndex}");
        runner.LoadScene(SceneRef.FromIndex(gameSceneBuildIndex), LoadSceneMode.Single);
    }

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
        Debug.Log($"[MP] 게임 씬 진입: IsServer={GameAuthority.IsServer}, LocalPlayerId={LocalPlayer.LocalPlayerId}, " +
                  $"좌석 {{{string.Join(",", MatchConfig.OccupiedSlots.OrderBy(s => s))}}}, PlayerContext {contexts}");

        if (shotDelay >= 0f && !string.IsNullOrEmpty(shotPath)) StartCoroutine(ShotAfter(shotDelay, shotPath));
        if (quitDelay >= 0f) StartCoroutine(QuitAfter(quitDelay));
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
        Cleanup();
        Application.Quit();
    }

    void OnGUI()
    {
        // 게임 씬에선 창구를 안 그린다 — 게임 HUD와 겹친다.
        if (SceneManager.GetActiveScene().buildIndex == gameSceneBuildIndex) return;

        GUILayout.BeginArea(new Rect(20, 20, 360, 400), GUI.skin.box);
        GUILayout.Label("길랜디 멀티 (시제품)");

        bool idle = runner == null && !starting;
        GUI.enabled = idle;
        GUILayout.Label("방 이름");
        sessionName = GUILayout.TextField(sessionName);
        if (GUILayout.Button("호스트 만들기")) _ = StartRunner(GameMode.Host);
        if (GUILayout.Button("참가")) _ = StartRunner(GameMode.Client);
        GUI.enabled = true;

        GUILayout.Space(8);
        GUILayout.Label(status);

        if (runner != null && runner.IsRunning)
        {
            GUILayout.Label("좌석: " + string.Join(", ", NetPlayer.All.OrderBy(p => p.Slot)
                .Select(p => $"{p.Slot}{(p.HasInputAuthority ? "(나)" : "")}")));

            GUI.enabled = CanStartMatch;
            if (runner.IsServer && GUILayout.Button("시작")) StartMatch();
            GUI.enabled = true;

            if (GUILayout.Button("나가기")) { Cleanup(); status = "나왔습니다."; }
        }

        GUILayout.EndArea();
    }
}
