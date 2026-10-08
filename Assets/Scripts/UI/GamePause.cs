using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일시정지(사장님 10-07 「일시정지 기능」) — Time.timeScale 0 + AudioListener.pause. 같이 하기(10-08): 누가 P를 누르든 요청 RPC → 호스트가 NetGameState.Paused를 세움 → 전원 적용
/// (규칙 상수 PausesPerPlayer·AnyoneCanResume은 사장님 확정 10-08: 누구나 멈춤·누구나 풀기·횟수 무제한). 호스트가 멈춰도 Fusion 틱·RPC·접속은 살아 있다(두 창 실측).
/// 컷인 정지(CutinHold)는 호스트가 정해 NetGameState.CutinHold로 전원에게(클라는 ApplyNetworkedCutin).
///
/// 멈춘 동안 막는 것: 뽑기·상점(GameHud.UseShop)·조합(CombineSystem.TryCombine — 채팅 조합 포함)·스킬 시전(UnitAttacker.TryCastActive*)·명령 카드 버튼(GameHud.OnUnitCommandSlotClicked). 카메라·채팅 입력·메뉴는 그대로.
/// timeScale을 안 따르는 시간(Time.unscaledTime 등 — 알림 표시·툴팁·화면 흔들림·BGM 페이드·UI 던져 두기 타이머)은 전수 훑었다: 전부 화면·소리 연출이고 게임 상태(타이머·쿨다운·충전식 돈도박·스토리 대기·적 이동)는 Time.time/deltaTime이라 같이 멈춘다.
/// 키: P · Pause/Break (Esc는 레시피 서랍·채팅·선택 취소가 이미 쓴다 — 안 쓴다). GameHud가 키·메뉴 [일시정지] 단추·가운데 문구를 맡는다.
/// </summary>
public static class GamePause
{
    /// <summary>사용자가 건 일시정지(P·메뉴). 정지 이유는 둘 — 이것과 <see cref="CutinHold"/> — 이고 둘 다 없을 때만 timeScale 1.</summary>
    public static bool Paused { get; private set; }

    /// <summary>컷인(CutinOverlay)이 거는 게임 시간 정지(사장님 10-08 「컷인 나올 때 맵 전체 일시정지」). 혼자 하기에서만 걸린다 — 소리는 안 멈춘다(획득 음성 유지).</summary>
    public static bool CutinHold { get; private set; }

    /// <summary>게임 시간이 서 있는가(사용자 일시정지 또는 컷인). 뽑기·조합·스킬을 막는 기준.</summary>
    public static bool Frozen => Paused || CutinHold;

    /// <summary>컷인 정지를 켜고 끈다(CutinOverlay가 부른다). 같이 하기에선 호스트만 정하고(사장님 10-08 확정: 누가 얻어도 전원 정지) 클라는 NetGameState.CutinHold를 ApplyNetworkedCutin으로 받는다 — 클라의 CutinOverlay가 부른 건 무시.</summary>
    public static void SetCutinHold(bool hold)
    {
        if (IsNetworked && !GameAuthority.IsServer) return;
        ApplyCutinHold(hold);
    }

    /// <summary>같이 하기 클라: 호스트가 정한 컷인 정지를 적용한다(매 프레임 불려도 바뀔 때만 일한다).</summary>
    public static void ApplyNetworkedCutin(bool hold) => ApplyCutinHold(hold);

    static void ApplyCutinHold(bool hold)
    {
        if (hold == CutinHold) return;
        CutinHold = hold;
        Time.timeScale = Frozen ? 0f : 1f;
    }

    // ── 같이 하기 일시정지 규칙(사장님 답 대기 — PM 추천 기본값, 바꿀 땐 이 상수만) ──
    /// <summary>한 사람이 한 판에 멈출 수 있는 횟수. 0 = 무제한.</summary>
    public const int PausesPerPlayer = 0;
    /// <summary>true면 누구나 풀 수 있고, false면 멈춘 사람(과 호스트)만 푼다.</summary>
    public const bool AnyoneCanResume = true;

    /// <summary>누가 멈췄나(슬롯, 같이 하기만 — 싱글은 −1). 가운데 문구용.</summary>
    public static int PausedBy { get; private set; } = -1;

    /// <summary>같이 하기든 혼자 하기든 멈출 수 있다(10-08 같이 하기 일시정지).</summary>
    public static bool Available => true;

    public static bool IsNetworked => GameAuthority.Provider != null;

    /// <summary>켜거나 끈다. 같이 하기에선 호스트에 요청만 보낸다(결과는 NetGameState.Paused로 돌아와 전원에게 적용). 막혔으면 이유를 돌려주고 false.</summary>
    public static bool TryToggle(out string reason)
    {
        reason = null;
        if (IsNetworked) { NetCommands.RequestPause(!Paused); return true; }
        Set(!Paused);
        return true;
    }

    /// <summary>같이 하기: 호스트가 정한 값(NetGameState.Paused/PausedBy)을 이 PC에 적용한다 — 매 프레임 불려도 바뀔 때만 일한다.</summary>
    public static void ApplyNetworked(bool paused, int by)
    {
        PausedBy = paused ? by : -1;
        Set(paused);
    }

    public static void Set(bool paused)
    {
        if (paused == Paused) return;
        Paused = paused;
        if (!paused) PausedBy = -1;
        Time.timeScale = Frozen ? 0f : 1f;   // 컷인 정지가 남아 있으면 사용자 정지를 풀어도 0 유지
        AudioListener.pause = paused;
        Debug.Log($"[일시정지] {(paused ? "멈춤" : "재개")}");
    }

    /// <summary>막힌 동작이 부른다 — 멈춘 중이면 알림을 띄우고 true.</summary>
    public static bool Blocks(int playerId = -1)
    {
        if (!Frozen) return false;
        PlayerNotification.Show(playerId >= 0 ? playerId : LocalPlayer.LocalPlayerId, Paused ? "일시정지 중입니다 — P로 계속" : "획득 연출 중입니다", 2f);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Install()
    {
        Paused = false; CutinHold = false; PausedBy = -1;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 바뀌면(처음 화면으로 등) 멈춤이 남지 않게 푼다.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CutinHold = false; PausedBy = -1;
        if (Paused) Set(false);
        else { Time.timeScale = 1f; AudioListener.pause = false; }
    }
}
