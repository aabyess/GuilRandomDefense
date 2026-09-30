using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배경음악(사장님 지시 2026-09-30 「기본적인 배경음악이 깔렸으면」, 10-01 「브금은 처음 게임 시작하고 1라운드까지만」).
/// Resources/Sounds/Bgm/ 아래 한 곡을 첫 화면(멀티 NetBoot)부터 틀고, **2라운드가 시작되는 순간** 볼륨을 줄여 끈 뒤 그 판에선 다시 안 튼다(1라운드 동안은 곡이 짧아 되풀이).
/// 새 판(씬을 다시 불러옴 — 「처음 화면으로」·다시 시작)에선 처음부터 다시 튼다. 씬과 무관하게 스스로 붙는다(GameVersion과 같은 방식).
/// 라운드는 싱글·호스트는 RoundManager, 멀티 클라는 호스트가 실어 보낸 NetGameState(HUD가 읽는 값과 같다)에서 읽는다.
/// 메뉴의 「소리 끄기」(<see cref="GameSound.Enabled"/>)를 그대로 따른다 — 끄면 멈추고, 켜면 멈춘 자리에서 이어진다.
/// 곡을 바꾸려면 파일을 넣고 <see cref="Track"/>만 고친다.
/// </summary>
public class GameBgm : MonoBehaviour
{
    const string Track = "Sounds/Bgm/binks_sake";
    // 볼륨 계산(사장님 「시끄럽다」 10-01): 옛 곡 −7.7dB × 0.22 ≈ −20.9dB가 시끄러웠다. 새 30초 곡은 평균 −27.6dB라
    // 0.5를 곱하면 ≈ −33.6dB — 옛 설정보다 12dB쯤 작다. 어림값이니 사장님이 들어 보고 조절.
    const float Volume = 0.5f;
    const float FadeSeconds = 1.75f;
    const int LastRound = 1;         // 이 라운드가 끝나면(다음 라운드 시작) 끈다
    const bool BgmOn = true;

    static GameBgm instance;
    AudioSource source;
    RoundManager roundManager;
    float nextFind;
    bool fading;
    bool finished;                   // 이 판에선 다 끝났다 — 다시 안 켠다
    int lastRound;

    /// <summary>지금 실제로 나고 있나(테스트용).</summary>
    public static bool IsPlaying => instance != null && instance.source != null && instance.source.isPlaying;

    /// <summary>재생 위치(초, 테스트용) — 시간이 흐르는지로 「정말 도는지」를 본다.</summary>
    public static float Position => instance != null && instance.source != null ? instance.source.time : -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!BgmOn || instance != null) return;
        GameObject host = new GameObject("[GameBgm]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GameBgm>();
    }

    void Awake()
    {
        AudioClip clip = Resources.Load<AudioClip>(Track);
        if (clip == null) { Debug.LogWarning($"[소리] 배경음악 Resources/{Track} 를 못 찾았습니다."); return; }
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;          // 새 곡이 30초라 1라운드 동안 되풀이 — 2라운드 시작 때 페이드 후 정지(그 판에선 다시 안 켠다)
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = Volume;
        source.priority = 0;         // 효과음이 몰려도 배경음악이 밀려나지 않게
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[소리] 배경음악 {clip.name}({clip.length:0}초, 볼륨 {Volume:0.00}) — {LastRound}라운드까지");
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // 새 판 — 처음부터 다시(첫 씬 로드도 여기 한 번 오지만 아직 아무것도 안 튼 상태라 그대로다)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetForNewGame();

    void ResetForNewGame()
    {
        if (source == null) return;
        source.Stop();
        source.time = 0f;
        source.volume = Volume;
        fading = false;
        finished = false;
        lastRound = 0;
        roundManager = null;
    }

    /// <summary>지금 라운드와 「준비 시간 중인가」. 라운드 정보가 아직 없으면(첫 화면) false.</summary>
    bool TryReadRound(out int round, out bool preparing)
    {
        round = 0; preparing = false;
        if (!GameAuthority.IsServer && NetGameState.Instance != null)   // 멀티 클라: RoundManager는 멈춰 있다
        {
            round = NetGameState.Instance.Round;
            preparing = NetGameState.Instance.Preparing;
            return round > 0;
        }
        if (roundManager == null && Time.unscaledTime >= nextFind)
        {
            roundManager = FindFirstObjectByType<RoundManager>();
            nextFind = Time.unscaledTime + 0.5f;
        }
        if (roundManager == null) return false;
        round = roundManager.CurrentRound;
        preparing = roundManager.IsWaitingForNextRound;
        return true;
    }

    void Update()
    {
        if (source == null || finished) return;

        if (TryReadRound(out int round, out bool preparing))
        {
            // 라운드 번호가 되돌아갔다 = 씬 로드 없이 새 판이 시작됐다 → 처음부터
            if (round < lastRound) { ResetForNewGame(); }
            lastRound = round;
            if (!fading && round > LastRound && !preparing) fading = true;   // 2라운드가 실제로 시작되는 순간
        }

        if (fading)
        {
            source.volume -= Volume / FadeSeconds * Time.unscaledDeltaTime;
            if (source.volume <= 0f) { source.Stop(); finished = true; fading = false; return; }
        }

        bool want = GameSound.Enabled;
        if (want && !source.isPlaying) source.UnPause();
        if (want && !source.isPlaying) source.Play();   // 아직 한 번도 안 틀었으면 UnPause로는 안 돈다
        else if (!want && source.isPlaying) source.Pause();
    }

    static string Describe() =>
        instance == null || instance.source == null ? "배경음악 없음"
            : $"배경음악 {instance.source.clip.name}: 재생 {instance.source.isPlaying} · 위치 {instance.source.time:0.0}/{instance.source.clip.length:0.0}초 · 볼륨 {instance.source.volume:0.000} · 되풀이 {instance.source.loop} · 줄이는 중 {instance.fading} · 끝남 {instance.finished} · 라운드 {instance.lastRound}";
}
