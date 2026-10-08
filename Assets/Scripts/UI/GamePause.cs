using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일시정지(사장님 10-07 「일시정지 기능」) — 혼자 하기에서만. Time.timeScale 0 + AudioListener.pause. 같이 하기(GameAuthority.Provider가 있는 판)는 이번엔 막는다
/// (원작 워크3 멀티 일시정지 방식은 사장님께 따로 여쭤 둠).
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

    /// <summary>컷인 정지를 켜고 끈다. 같이 하기(네트 판)에선 CutinHoldInMultiplayer가 켜져야 건다.</summary>
    /// <summary>같이 하기에서도 컷인 정지를 허용(사장님 10-08 확정: 누가 얻어도 전원 정지). 구현담당1이 호스트 RPC로 전원이 Play를 부르게 만들 때 켠다 — 그 전엔 false라 MP는 안 멈춘다.</summary>
    public static bool CutinHoldInMultiplayer = false;

    public static void SetCutinHold(bool hold)
    {
        if (hold && !Available && !CutinHoldInMultiplayer) return;
        if (hold == CutinHold) return;
        CutinHold = hold;
        Time.timeScale = Frozen ? 0f : 1f;
    }

    /// <summary>같이 하기(네트 판)면 불가.</summary>
    public static bool Available => GameAuthority.Provider == null;

    public const string MultiplayerMessage = "같이 하기에선 일시정지 할 수 없습니다.";

    /// <summary>켜거나 끈다. 막혔으면 이유를 돌려주고 false.</summary>
    public static bool TryToggle(out string reason)
    {
        reason = null;
        if (!Paused && !Available) { reason = MultiplayerMessage; return false; }
        Set(!Paused);
        return true;
    }

    public static void Set(bool paused)
    {
        if (paused == Paused) return;
        Paused = paused;
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
        Paused = false; CutinHold = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 씬이 바뀌면(처음 화면으로 등) 멈춤이 남지 않게 푼다.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CutinHold = false;
        if (Paused) Set(false);
        else { Time.timeScale = 1f; AudioListener.pause = false; }
    }
}
