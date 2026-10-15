using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 30R 보스 파칭코 — 규칙·네트워크 뼈대(구현담당1). 화면 연출은 오버레이(구현담당2)가 아래 훅에 붙는다.
///
/// 사양(사장님 10-14): **플레이어마다 따로 뽑는다**. 정지는 전원 같이(GamePause 컷인 정지 길), 각 PC는 자기 슬롯의 plan만 재생,
/// PUSH도 각자 자기 버튼(PachinkoSettings.PushSeconds 안에 한 번 — 남이 눌러도 내 것은 안 멈춘다, 안 누르면 끝에 자동).
/// 결과별 보상: 777 → 랜덤유닛 1기 직접 지급 · 세 같은 숫자(7 제외) → 전설·히든 위습(솔·성탄·뻬꼼 제외) · 리치 꽝 → 희귀함 위습 · 그냥 꽝 → 특별함 위습. 전부 그 사람만.
/// 모든 슬롯의 결과 화면이 끝난 뒤에 정지를 푼다.
///
/// 흐름(서버 = 호스트/솔로): 보스(30R) 처치 → 살아 있는 슬롯마다 plan → 정지 → 슬롯별 RPC_Pachinko(slot, plan) → [슬롯별 PreludeSeconds] → 자기 PUSH 창
///   → 자기 Space(클라는 RPC → 호스트) 또는 시간 끝 → 그 슬롯에 RPC_PachinkoPushed(slot, who) → [슬롯별 ResultSeconds] → 보상 → 전부 끝나면 정지 해제.
///
/// 오버레이 연결(구현담당2): PachinkoController.PlayHandler = plan => …(내 plan 연출 시작, PUSH는 plan.PreludeSeconds() 뒤 PachinkoSettings.PushSeconds 동안),
///   PachinkoController.PushedHandler = who => …(내 줄 가운데 정지 — who ≥ 0 = 내가 눌렀다, -1 = 시간 끝 자동). Space 입력은 이 컨트롤러가 받는다.
///   클릭으로도 누르려면 PachinkoController.RequestPush()를 부른다. 훅이 비어 있으면 로그만 찍고 바로 결과로 간다(임시 경로).
/// 개발 인자: -pachinkoTest jackpot|win|revive|reach|miss|random [-pachinkoTheme tokyo|rezero|fire|slime] [-pachinkoPush success|fail] [-pachinkoSkipTo <초>]  (게임 씬 진입 8초 뒤 모든 슬롯에 그 결과 — 쉼표로 슬롯별 지정 가능 「win,miss」)
/// </summary>
public class PachinkoController : MonoBehaviour
{
    public static Action<PachinkoPlan> PlayHandler;
    public static Action<int> PushedHandler;

    // 테마 투표(10-14, 구현담당2 오버레이가 붙는다). 훅이 비어 있으면 투표를 건너뛰고 바로 시작한다.
    public static Action<float> VoteOpenHandler;       // 투표 화면 열기(초)
    public static Action<int[], int> VoteTallyHandler; // (슬롯별 표 0=무투표 1=東京喰種 2=Re:ゼロ 3=불꽃 4=전생슬, 내 슬롯)
    public static Action<int> VoteCloseHandler;        // 마감 결과 theme 0/1/2 (PachinkoOverlay.Theme 세팅·화면 닫기)
    // 관전 모니터(10-16): 내 것이 아닌 슬롯의 plan·눌림이 도착하면(모든 PC가 전원 plan을 받는다) 모니터가 슬롯별 시계로 재현한다. 내 슬롯도 부른다.
    public static Action<int, PachinkoPlan> MonitorPlanHandler;
    public static Action<int, int> MonitorPushedHandler;   // (slot, who) — who ≥ 0 누름 · -1 시간 끝
    public static Action MonitorClearHandler;
    /// <summary>마감으로 정해진 테마(0=東京喰種 1=Re:ゼロ 2=불꽃 소방대 3=전생슬, -1=아직 안 정함). 투표가 건너뛰어지면 -1.</summary>
    public static int VoteTheme = -1;
    public const float VoteSeconds = 10f, VoteResultHold = 1.3f;
    const int MaxSlots = 16;
    static bool voting;
    static float voteDeadline, voteLaunchAt;
    static bool voteClosed;
    static string[] voteModes;
    static readonly int[] votes = new int[MaxSlots];

    public static PachinkoController Instance { get; private set; }
    /// <summary>이 PC에서 파칭코 연출이 진행 중인가(카메라·입력이 비켜야 할 때 본다).</summary>
    public static bool Active => Instance != null && Instance.Running;
    static bool triggeredThisRun;

    enum Phase { Prelude, Push, Result, Done }
    class Run
    {
        public PachinkoPlan plan; public Phase phase;
        public float openAt, closeAt, endAt;
        public bool requested;   // 클라: 눌러서 요청을 보냈다
        public bool pushedOk;    // PUSH를 눌렀다(불꽃 테마 보상 강등 판정)
        public float startAt;    // 이 슬롯이 시작된 실시간(정지 안전장치)
    }

    readonly Dictionary<int, Run> runs = new Dictionary<int, Run>();
    bool authority;
    bool Running { get { foreach (Run r in runs.Values) if (r.phase != Phase.Done) return true; return false; } }

    static PachinkoController Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PachinkoController");
        DontDestroyOnLoad(go);   // 개발 인자(-pachinkoTest)는 첫 씬(NetBoot)에서 만들어져 혼자 하기 씬 전환 때 파괴되던 문제(0.3.18)
        Instance = go.AddComponent<PachinkoController>();
        return Instance;
    }

    // ───────── 서버(호스트/솔로) 진입 ─────────

    /// <summary>R30 보스 처치(RewardDistributor.GrantBossReward)에서 부른다. 판당 한 번, 서버만.</summary>
    public static void OnBossKilled(int round)
    {
        if (round != PachinkoSettings.TriggerRound || triggeredThisRun || !GameAuthority.IsServer) return;
        Begin(null);
    }

    /// <summary>살아 있는 슬롯마다 계획을 뽑아 시작한다. modes = 슬롯 순서대로 결과 지정(개발·테스트, null이면 확률 그대로, 하나면 전원 같은 결과).</summary>
    public static void Begin(string[] modes)
    {
        if (!GameAuthority.IsServer) return;
        PachinkoController c = Ensure();
        if (c.Running || voting) return;
        triggeredThisRun = true;
        bool fixedResult = false;
        if (modes != null) foreach (string m in modes) if (!string.IsNullOrEmpty(m) && m != "random") fixedResult = true;
        if (!fixedResult && VoteOpenHandler != null)
        {
            c.authority = true;
            MonitorClearHandler?.Invoke();
            voting = true; voteClosed = false; voteModes = modes;
            Array.Clear(votes, 0, votes.Length);
            voteDeadline = Time.unscaledTime + VoteSeconds;
            GamePause.SetPachinkoHold(true);
            NetGameState vs = NetGameState.Instance;
            if (vs != null && GamePause.IsNetworked) vs.BroadcastPachinkoVoteOpen(VoteSeconds);
            voteOpenSeen = true;
            Debug.Log($"[파칭코] 테마 투표 시작 {VoteSeconds:F0}초 (t={Time.unscaledTime:F1})");
            VoteOpenHandler(VoteSeconds);
            return;
        }
        c.Launch(modes);
    }

    void Launch(string[] modes)
    {
        PachinkoController c = this;
        c.authority = true;
        c.runs.Clear();
        int index = 0;
        var slots = new List<int>();
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            if (context.IsDead) continue;
            string mode = modes == null || modes.Length == 0 ? null : modes[Mathf.Min(index, modes.Length - 1)];
            if (mode == "random") mode = null;
            c.runs[context.PlayerId] = new Run { plan = PachinkoPlan.Roll(mode) };
            slots.Add(context.PlayerId);
            index++;
        }
        if (slots.Count == 0) { GamePause.SetPachinkoHold(false); return; }
        GamePause.SetPachinkoHold(true);
        MonitorClearHandler?.Invoke();
        NetGameState state = NetGameState.Instance;
        foreach (int slot in slots)
        {
            Run r = c.runs[slot];
            c.Arm(slot, r, local: slot == GameAuthority.LocalPlayerId);
            // 호스트 자기 슬롯도 보낸다(10-16 g1: 예전엔 slot != Local만 보내 클라 관전 모니터에 호스트 줄이 안 떴다). 클라는 자기 슬롯이 아니면 모니터로만 받는다.
            if (state != null && GamePause.IsNetworked) state.BroadcastPachinko(slot, r.plan.Pack());
        }
    }

    // ───────── 테마 투표 ─────────

    /// <summary>오버레이가 내 표를 던졌다(PachinkoOverlay.LocalVoteCast에서 이 함수로). 바꿔 누르기 = 덮어쓰기.</summary>
    public static void CastVote(int theme)
    {
        if (theme < 0 || theme > 3) return;
        if (GameAuthority.IsServer) ReceiveVote(GameAuthority.LocalPlayerId, theme);
        else NetCommands.RequestPachinkoVote(theme);
    }

    /// <summary>호스트: 슬롯의 표 수신(NetCommands). 투표 중일 때만.</summary>
    public static void ReceiveVote(int slot, int theme)
    {
        if (!GameAuthority.IsServer || !voting || voteClosed || slot < 0 || slot >= MaxSlots || theme < 0 || theme > 3) return;
        votes[slot] = theme + 1;
        BroadcastTally();
    }

    static void BroadcastTally()
    {
        int packed = 0;
        for (int i = 0; i < 10; i++) packed |= (votes[i] & 7) << (i * 3);   // 표 값 0~4 = 3비트 × 10슬롯(MaxSlots 4)
        VoteTallyHandler?.Invoke((int[])votes.Clone(), GameAuthority.LocalPlayerId);
        NetGameState s = NetGameState.Instance;
        if (s != null && GamePause.IsNetworked) s.BroadcastPachinkoVoteTally(packed);
    }

    public static void ReceiveVoteOpen(float seconds)
    {
        if (GameAuthority.IsServer) return;
        Ensure().authority = false;
        voteOpenSeen = true;
        Debug.Log($"[파칭코] 투표 열림 수신 {seconds:F0}초 (t={Time.unscaledTime:F1})");
        VoteOpenHandler?.Invoke(seconds);
    }

    public static void ReceiveVoteTally(int packed)
    {
        if (GameAuthority.IsServer) return;
        var by = new int[MaxSlots];
        for (int i = 0; i < 10 && i < MaxSlots; i++) by[i] = (packed >> (i * 3)) & 7;
        VoteTallyHandler?.Invoke(by, GameAuthority.LocalPlayerId);
    }

    public static void ReceiveVoteClose(int theme)
    {
        if (GameAuthority.IsServer) return;
        VoteTheme = theme;
        VoteCloseHandler?.Invoke(theme);
    }

    void CloseVote()
    {
        var count = new int[4];
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            if (context.IsDead || context.PlayerId < 0 || context.PlayerId >= MaxSlots) continue;
            int v = votes[context.PlayerId];
            if (v >= 1 && v <= 4) count[v - 1]++;
        }
        int total = count[0] + count[1] + count[2] + count[3];
        int theme;   // 표 비율 = 확률, 무투표·안 누른 사람은 표에서 빠짐(아무도 안 눌렀으면 넷 균등)
        if (total == 0) theme = UnityEngine.Random.Range(0, 4);
        else
        {
            float r = UnityEngine.Random.value * total;
            theme = 0;
            for (int i = 0; i < 4; i++) { r -= count[i]; if (r <= 0f) { theme = i; break; } }
        }
        int a = count[0], b = count[1];
        voteClosed = true;
        VoteTheme = theme;
        voteLaunchAt = Time.unscaledTime + VoteResultHold;
        Debug.Log($"[파칭코] 투표 마감 — 東京喰種 {a} · Re:ゼロ {b} · 불꽃 {count[2]} · 전생슬 {count[3]} → 테마 {theme} (t={Time.unscaledTime:F1})");
        NetGameState s = NetGameState.Instance;
        if (s != null && GamePause.IsNetworked) s.BroadcastPachinkoVoteClose(theme);
        VoteCloseHandler?.Invoke(theme);
    }

    // ───────── 클라: 내 plan 수신 · 눌림 수신 ─────────

    public static void ReceivePlan(int slot, int packed)
    {
        if (GameAuthority.IsServer) return;
        if (slot != GameAuthority.LocalPlayerId) { PachinkoPlan other = PachinkoPlan.Unpack(packed); Debug.Log($"[파칭코] 관전 plan 수신 슬롯 {slot} — {other} (t={Time.unscaledTime:F1})"); MonitorPlanHandler?.Invoke(slot, other); return; }   // 남의 슬롯 = 관전 모니터용
        PachinkoController c = Ensure();
        c.authority = false;
        var run = new Run { plan = PachinkoPlan.Unpack(packed) };
        c.runs[slot] = run;
        c.Arm(slot, run, local: true);
    }

    /// <summary>호스트가 내 PUSH가 눌렸다(또는 시간 끝)고 알렸다.</summary>
    public static void ReceivePushed(int slot, int who)
    {
        if (GameAuthority.IsServer) return;
        if (slot != GameAuthority.LocalPlayerId) { Debug.Log($"[파칭코] 관전 눌림 수신 슬롯 {slot} — {(who < 0 ? "시간 끝" : "누름")} (t={Time.unscaledTime:F1})"); MonitorPushedHandler?.Invoke(slot, who); return; }
        MonitorPushedHandler?.Invoke(slot, who);
        if (Instance == null || !Instance.runs.TryGetValue(slot, out Run r) || r.phase == Phase.Result || r.phase == Phase.Done) return;
        Instance.EnterResult(slot, r, who, broadcast: false);
    }

    /// <summary>호스트가 클라의 PUSH 요청을 받았다(NetCommands). 그 슬롯의 창이 열려 있을 때만.</summary>
    public static void ExecutePush(int slot)
    {
        if (Instance == null || !Instance.authority || !Instance.runs.TryGetValue(slot, out Run r)) return;
        if (r.phase != Phase.Push) return;
        Instance.EnterResult(slot, r, slot, broadcast: true);
    }

    /// <summary>내 PUSH를 누른다(Space 외에 클릭 등). 창이 열려 있을 때만 의미가 있다.</summary>
    public static void RequestPush()
    {
        if (Instance == null) return;
        int slot = GameAuthority.LocalPlayerId;
        if (!Instance.runs.TryGetValue(slot, out Run r) || r.phase != Phase.Push) return;
        if (Instance.authority) Instance.EnterResult(slot, r, slot, broadcast: true);
        else if (!r.requested) { r.requested = true; NetCommands.RequestPachinkoPush(); }
    }

    // ───────── 진행 ─────────

    void Arm(int slot, Run r, bool local)
    {
        bool overlay = local && PlayHandler != null;
        float now = Time.unscaledTime;
        float skip = PachinkoSettings.VideoTheme(r.plan.theme) != null ? PachinkoSettings.DebugSkipSeconds : 0f;   // -pachinkoSkipTo 시험용
        float prelude = overlay || !local ? Mathf.Max(0.5f, r.plan.PreludeSeconds() - skip) : PachinkoSettings.NoOverlayDelay;
        r.startAt = now;
        r.openAt = now + (local && !overlay ? PachinkoSettings.NoOverlayDelay : prelude);
        r.closeAt = r.openAt + (local && !overlay ? 0f : r.plan.PushSeconds());
        r.phase = Phase.Prelude;
        Debug.Log($"[파칭코] 시작({(authority ? "호스트" : "클라")}) 슬롯 {slot} — {r.plan} · {(local ? (overlay ? "오버레이" : "연출 없음(임시 경로)") : "원격 슬롯")} · PUSH 창 {r.openAt - now:F1}~{r.closeAt - now:F1}초");
        MonitorPlanHandler?.Invoke(slot, r.plan);   // 내 슬롯·호스트가 아는 원격 슬롯 모두(관전 모니터)
        if (local) PlayHandler?.Invoke(r.plan);
        if (local && testAutoPushDelay >= 0f) StartCoroutine(TestAutoPush(testAutoPushDelay + r.plan.PreludeSeconds() - (PachinkoSettings.VideoTheme(r.plan.theme) != null ? PachinkoSettings.DebugSkipSeconds : 0f)));
    }

    // 시험용(-pachinkoAutoPush 초): PUSH 창이 열리고 이 초 뒤에 내 PUSH를 자동으로 누른다(클라면 RPC). 로그 「[파칭코] 시험 자동 PUSH」.
    static float testAutoPushDelay = -1f;
    System.Collections.IEnumerator TestAutoPush(float wait)
    {
        float until = Time.unscaledTime + Mathf.Max(0.1f, wait);
        while (Time.unscaledTime < until) yield return null;
        Debug.Log($"[파칭코] 시험 자동 PUSH — 슬롯 {GameAuthority.LocalPlayerId} (t={Time.unscaledTime:F1})");
        RequestPush();
    }

    // 시험용(-pachinkoVote 테마 초): 투표 창이 열린 뒤 이 초에 이 PC가 그 테마(0~3)에 한 표 던진다.
    static bool voteOpenSeen;
    System.Collections.IEnumerator TestVote(int theme, float delay)
    {
        while (!voteOpenSeen) yield return null;
        float until = Time.unscaledTime + delay;
        while (Time.unscaledTime < until) yield return null;
        Debug.Log($"[파칭코] 시험 투표 — 슬롯 {GameAuthority.LocalPlayerId} → 테마 {theme} (t={Time.unscaledTime:F1})");
        CastVote(theme);
    }

    void EnterResult(int slot, Run r, int who, bool broadcast)
    {
        r.phase = Phase.Result;
        r.pushedOk = who >= 0 && r.plan.pushWin;   // 눌렀어도 pushWin(51%)이 아니면 실패 — 누르지 않으면 무조건 실패
        bool local = slot == GameAuthority.LocalPlayerId;
        r.endAt = Time.unscaledTime + ((local && PlayHandler == null) ? 0.2f : r.plan.ResultSeconds(r.pushedOk));
        MonitorPushedHandler?.Invoke(slot, who);
        Debug.Log($"[파칭코] PUSH 슬롯 {slot} — {(who < 0 ? "시간 끝(자동)" : "누름")} → 가운데 정지 · {r.plan}");
        if (broadcast)
        {
            NetGameState state = NetGameState.Instance;
            if (state != null && GamePause.IsNetworked) state.BroadcastPachinkoPushed(slot, who);   // 호스트 슬롯도(클라 모니터가 호스트 눌림·시간 끝을 본다)
        }
        if (local) PushedHandler?.Invoke(who);
    }

    void Update()
    {
        if (voting && authority)
        {
            if (!voteClosed && Time.unscaledTime >= voteDeadline) CloseVote();
            else if (voteClosed && Time.unscaledTime >= voteLaunchAt) { voting = false; string[] m = voteModes; voteModes = null; Launch(m); }
            return;
        }
        if (runs.Count == 0) return;
        float now = Time.unscaledTime;   // 게임 시간이 서 있으므로 실시간
        int me = GameAuthority.LocalPlayerId;
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && PlayHandler != null;
        bool allDone = true;
        foreach (KeyValuePair<int, Run> kv in new List<KeyValuePair<int, Run>>(runs))
        {
            int slot = kv.Key; Run r = kv.Value;
            if (r.phase == Phase.Done) continue;
            allDone = false;
            // 정지 안전장치: 연출·PUSH·결과를 다 합친 시간 + 여유를 넘기면 그 슬롯을 강제로 끝낸다(끊김·멈춤으로 전원 정지가 길어지지 않게).
            float cap = r.startAt + r.plan.PreludeSeconds() + r.plan.PushSeconds() + r.plan.ResultSeconds(true) + PachinkoSettings.StallSafetySeconds;
            if (r.startAt > 0f && now > cap)
            {
                Debug.LogWarning($"[파칭코] 슬롯 {slot} 안전장치 — 예정 시간을 {PachinkoSettings.StallSafetySeconds:F0}초 넘겨 강제로 끝냅니다.");
                r.phase = Phase.Done;
                if (authority) RewardDistributor.Instance?.GrantPachinkoReward(slot, RewardPlan(r));
                continue;
            }
            if (r.phase == Phase.Prelude && now >= r.openAt) r.phase = Phase.Push;
            if (r.phase == Phase.Push)
            {
                if (slot == me && spacePressed) RequestPush();
                if (authority && r.phase == Phase.Push && now >= r.closeAt) EnterResult(slot, r, -1, broadcast: true);
                else if (!authority && now >= r.closeAt + 3f)
                { r.phase = Phase.Done; Debug.LogWarning("[파칭코] 호스트 눌림 알림이 안 와 연출을 닫았습니다."); }
            }
            else if (r.phase == Phase.Result && now >= r.endAt)
            {
                r.phase = Phase.Done;
                if (authority) RewardDistributor.Instance?.GrantPachinkoReward(slot, RewardPlan(r));
            }
        }
        if (allDone || !Running)
        {
            if (authority) { GamePause.SetPachinkoHold(false); Debug.Log("[파칭코] 전원 끝 — 정지 해제"); }
            runs.Clear();
        }
    }

    /// <summary>
    /// 보상 계산용 plan. 영상형 테마(사장님 10-16): 같은 숫자 셋(크리티컬 = plan.win) + PUSH 성공 → 제일 좋은 보상(777 보상, 랜덤유닛) ·
    /// PUSH 실패 → 당첨 보상(전설·히든 위습). 꽝은 plan 그대로(리치꽝/꽝). 값은 PachinkoSettings.VideoSuccessIsTopReward.
    /// </summary>
    static PachinkoPlan RewardPlan(Run r)
    {
        PachinkoPlan p = r.plan;
        if (PachinkoSettings.VideoTheme(p.theme) != null && p.win)
        {
            if (PachinkoSettings.VideoSuccessIsTopReward) { p.jackpot = r.pushedOk; p.win = true; }
            else if (!r.pushedOk) { p.jackpot = false; p.win = false; p.reach = true; }   // 옛 방식
        }
        return p;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; triggeredThisRun = false; PlayHandler = null; PushedHandler = null; MonitorPlanHandler = null; MonitorPushedHandler = null; MonitorClearHandler = null; VoteOpenHandler = null; VoteTallyHandler = null; VoteCloseHandler = null; voteOpenSeen = false; voting = false; voteClosed = false; voteModes = null; VoteTheme = -1; }

    // ───────── 개발 인자 ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void TestArg()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-pachinkoAutoPush" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ap)) testAutoPushDelay = ap;
            if (args[i] == "-pachinkoVote" && i + 2 < args.Length && int.TryParse(args[i + 1], out int vt) && float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float vd))
                Ensure().StartCoroutine(Ensure().TestVote(vt, vd));
        }
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-pachinkoTest") continue;
            string[] modes = args[i + 1].Split(',');
            Ensure().StartCoroutine(Ensure().TestRoutine(modes));
            return;
        }
    }

    System.Collections.IEnumerator TestRoutine(string[] modes)
    {
        float until = Time.unscaledTime + 8f;
        while (Time.unscaledTime < until || RewardDistributor.Instance == null) yield return null;
        Debug.Log($"[파칭코] 개발 인자로 발동: {string.Join(",", modes)}");
        // -pachinkoTheme tokyo|rezero — 결과를 고정하면 투표를 건너뛰므로 테마도 인자로 고른다(10-16 사장님 리제로 확인).
        string[] args = Environment.GetCommandLineArgs();
        int ski = Array.IndexOf(args, "-pachinkoSkipTo");   // 영상형 테마 시작 위치 점프(시험용)
        if (ski >= 0 && ski + 1 < args.Length && float.TryParse(args[ski + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float skipTo)) PachinkoSettings.DebugSkipSeconds = Mathf.Max(0f, skipTo);
        int pi = Array.IndexOf(args, "-pachinkoPush");   // success|fail — 영상형 테마 PUSH 결과 고정(빌드 확인용)
        if (pi >= 0 && pi + 1 < args.Length) PachinkoPlan.ForcePush = args[pi + 1].ToLowerInvariant().StartsWith("s");
        int ti = Array.IndexOf(args, "-pachinkoTheme");
        if (ti >= 0 && ti + 1 < args.Length) { string tn = args[ti + 1].ToLowerInvariant(); VideoThemeSpec vs = PachinkoSettings.VideoThemeByKey(tn); PachinkoOverlay.Theme = tn.StartsWith("re") ? 1 : vs != null ? vs.theme : 0; }
        Begin(modes);
    }
}
