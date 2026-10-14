using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 30R 보스 파칭코 — 규칙·네트워크 뼈대(구현담당1). 화면 연출은 오버레이(구현담당2)가 아래 훅에 붙는다.
///
/// 흐름(호스트/솔로가 전부 결정, 클라는 표시만):
///   보스(30R) 처치 순간 한 번 → 계획 뽑기(PachinkoPlan.Roll) → GamePause 정지 → 전원에게 계획 전송(RPC_Pachinko) → 각 PC PlayHandler(plan)
///   → [연출 PreludeSeconds] → PUSH 창(PushSeconds) 아무나 Space = 클라는 RPC로 호스트에 알림 → 호스트가 누른 슬롯을 전원에게(RPC_PachinkoPushed) → 가운데 정지·결과
///   → [ResultSeconds] → 당첨이면 전설·히든 위습(표식: 솔·성탄·뻬꼼 제외, 풀카운트 감점 없음) 전원 지급 → 정지 해제.
///
/// 오버레이 연결(구현담당2): PachinkoController.PlayHandler = plan => …(연출 시작, PUSH는 plan.PreludeSeconds() 뒤에 띄운다),
///   PachinkoController.PushedHandler = slot => …(누른 슬롯, -1 = 시간 끝 자동 → 가운데 정지 연출 시작).
///   PUSH 입력은 이 컨트롤러가 받는다(Space) — 오버레이는 그리기만. 훅이 비어 있으면 로그만 찍고 바로 결과로 간다(임시 경로).
/// 개발 인자: -pachinkoTest win|revive|reach|miss|random  (게임 씬 진입 8초 뒤 바로 발동 — 솔로·호스트)
/// </summary>
public class PachinkoController : MonoBehaviour
{
    public static Action<PachinkoPlan> PlayHandler;
    public static Action<int> PushedHandler;

    public static PachinkoController Instance { get; private set; }
    /// <summary>이 PC에서 파칭코 연출이 진행 중인가(카메라·입력이 비켜야 할 때 본다).</summary>
    public static bool Active => Instance != null && Instance.phase != Phase.Idle;
    static bool triggeredThisRun;

    enum Phase { Idle, Prelude, Push, Result }
    Phase phase;
    PachinkoPlan plan;
    bool authority;            // 이 PC가 호스트/솔로인가
    float openAt, closeAt, endAt;
    bool requestedPush;        // 클라: 이미 눌러서 요청을 보냈는가

    static PachinkoController Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PachinkoController");
        Instance = go.AddComponent<PachinkoController>();
        return Instance;
    }

    // ───────── 서버(호스트/솔로) 진입 ─────────

    /// <summary>R30 보스 처치(RewardDistributor.GrantBossReward)에서 부른다. 판당 한 번, 서버만.</summary>
    public static void OnBossKilled(int round)
    {
        if (round != PachinkoSettings.TriggerRound || triggeredThisRun || !GameAuthority.IsServer) return;
        Begin(PachinkoPlan.Roll());
    }

    /// <summary>계획을 정해 시작한다(개발 인자·테스트도 이 길). 서버만 의미 있다.</summary>
    public static void Begin(PachinkoPlan plan)
    {
        if (!GameAuthority.IsServer) return;
        PachinkoController c = Ensure();
        if (c.phase != Phase.Idle) return;
        triggeredThisRun = true;
        c.authority = true;
        c.StartPlay(plan);
        NetGameState state = NetGameState.Instance;
        if (state != null && GamePause.IsNetworked) state.BroadcastPachinko(plan.Pack());
    }

    // ───────── 클라: 계획 수신 · 눌림 수신 ─────────

    public static void ReceivePlan(int packed)
    {
        if (GameAuthority.IsServer) return;   // 방장 PC는 이미 시작했다
        PachinkoController c = Ensure();
        c.authority = false;
        c.StartPlay(PachinkoPlan.Unpack(packed));
    }

    public static void ReceivePushed(int slot)
    {
        if (GameAuthority.IsServer) return;
        if (Instance == null || Instance.phase == Phase.Idle || Instance.phase == Phase.Result) return;
        Instance.EnterResult(slot, serverSide: false);
    }

    /// <summary>호스트가 클라의 PUSH 요청을 받았다(NetCommands).</summary>
    public static void ExecutePush(int slot)
    {
        if (Instance == null || !Instance.authority || Instance.phase != Phase.Push) return;
        Instance.EnterResult(slot, serverSide: true);
    }

    // ───────── 진행 ─────────

    void StartPlay(PachinkoPlan p)
    {
        plan = p;
        requestedPush = false;
        bool overlay = PlayHandler != null;
        float now = Time.unscaledTime;
        openAt = now + (overlay ? p.PreludeSeconds() : PachinkoSettings.NoOverlayDelay);
        closeAt = openAt + (overlay ? PachinkoSettings.PushSeconds : 0f);
        phase = Phase.Prelude;
        if (authority) GamePause.SetPachinkoHold(true);
        Debug.Log($"[파칭코] 시작({(authority ? "호스트" : "클라")}) — {p} · 연출 {(overlay ? "오버레이" : "없음(임시 경로)")} · PUSH 창 {openAt - now:F1}~{closeAt - now:F1}초");
        PlayHandler?.Invoke(p);
    }

    void EnterResult(int slot, bool serverSide)
    {
        phase = Phase.Result;
        endAt = Time.unscaledTime + (PlayHandler != null ? plan.ResultSeconds() : 0.2f);
        Debug.Log($"[파칭코] PUSH — {(slot < 0 ? "시간 끝(자동)" : "슬롯 " + slot)} → 가운데 정지 · 결과 {(plan.win ? "당첨" : "꽝")}{(plan.revive ? "(부활)" : "")}");
        if (serverSide)
        {
            NetGameState state = NetGameState.Instance;
            if (state != null && GamePause.IsNetworked) state.BroadcastPachinkoPushed(slot);
        }
        PushedHandler?.Invoke(slot);
    }

    void Update()
    {
        if (phase == Phase.Idle) return;
        float now = Time.unscaledTime;   // 게임 시간이 서 있으므로 실시간
        if (phase == Phase.Prelude && now >= openAt) phase = Phase.Push;

        if (phase == Phase.Push)
        {
            bool pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && PlayHandler != null;
            if (authority)
            {
                if (pressed) EnterResult(GameAuthority.LocalPlayerId, serverSide: true);
                else if (now >= closeAt) EnterResult(-1, serverSide: true);
            }
            else if (pressed && !requestedPush)
            {
                requestedPush = true;
                NetCommands.RequestPachinkoPush();
            }
            else if (now >= closeAt + 3f) { phase = Phase.Idle; Debug.LogWarning("[파칭코] 호스트 눌림 알림이 안 와 연출을 닫았습니다."); }
        }
        else if (phase == Phase.Result && now >= endAt)
        {
            phase = Phase.Idle;
            if (authority) Finish();
        }
    }

    void Finish()
    {
        if (plan.win) RewardDistributor.Instance?.GrantPachinkoWisps();
        GamePause.SetPachinkoHold(false);
        Debug.Log($"[파칭코] 끝 — {(plan.win ? "당첨: 전설·히든 위습 지급" : "꽝")}, 정지 해제");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; triggeredThisRun = false; PlayHandler = null; PushedHandler = null; }

    // ───────── 개발 인자 ─────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void TestArg()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-pachinkoTest") continue;
            string mode = args[i + 1] == "random" ? null : args[i + 1];
            Ensure().StartCoroutine(Ensure().TestRoutine(mode));
            return;
        }
    }

    System.Collections.IEnumerator TestRoutine(string mode)
    {
        float until = Time.unscaledTime + 8f;
        while (Time.unscaledTime < until || RewardDistributor.Instance == null) yield return null;
        Debug.Log($"[파칭코] 개발 인자로 발동: {mode ?? "random"}");
        Begin(PachinkoPlan.Roll(mode));
    }
}
