using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배경음악(사장님 지시 2026-09-30 「기본적인 배경음악이 깔렸으면」, 10-01 「브금은 처음 게임 시작하고 1라운드까지만」·「1라운드 보스 테마곡」).
/// 곡은 <see cref="Cues"/> 표 — 라운드 구간마다 한 곡. 구간에 들어오면 처음부터 틀고(구간 안에선 되풀이), 구간을 벗어나면 볼륨을 줄여 끈다.
/// 「구간 안」은 그 라운드가 실제로 도는 동안 + 다음 라운드 준비 시간까지(준비 중이면 한 라운드 앞으로 친다) — 2라운드 준비 동안 첫 곡이 이어지던 옛 동작과 같다.
/// 라운드 정보가 없으면(첫 화면·멀티 NetBoot) 0라운드로 친다.
/// 새 판(씬을 다시 불러옴 — 「처음 화면으로」·다시 시작)에선 처음부터 다시 판단한다. 씬과 무관하게 스스로 붙는다(GameVersion과 같은 방식).
/// 라운드는 싱글·호스트는 RoundManager, 멀티 클라는 호스트가 실어 보낸 NetGameState(HUD가 읽는 값과 같다)에서 읽는다.
/// 메뉴의 「소리 끄기」(<see cref="GameSound.Enabled"/>)를 그대로 따른다 — 끄면 멈추고, 켜면 멈춘 자리에서 이어진다.
/// 곡을 추가하려면 Assets/Audio/Music/Resources/Music/에 파일을 넣고 표에 한 줄.
/// </summary>
public class GameBgm : MonoBehaviour
{
    struct Cue
    {
        public string Track; public int FromRound; public int ToRound; public float Volume;
        public Cue(string track, int from, int to, float volume) { Track = track; FromRound = from; ToRound = to; Volume = volume; }
    }

    // 볼륨 계산(사장님 「시끄럽다」 10-01): binks_sake는 평균 −27.6dB라 0.5를 곱하면 ≈ −33.6dB.
    // 보스 테마(Zoltraak 앞 2:15)는 평균 −13.1dB로 14.5dB 크다 → 0.1이면 ≈ −33.1dB로 맞춘 것. 어림값이니 사장님이 들어 보고 조절.
    static readonly Cue[] Cues =
    {
        new Cue("Music/binks_sake", 0, 1, 0.5f),             // 첫 화면 ~ 1라운드(2라운드 시작 때 끔)
        new Cue("Music/boss_r10_zoltraak", 10, 10, 0.1f),   // 첫 보스(10라운드 주영호) — 11라운드 시작 때 끔
        new Cue("Music/boss_r20_journey", 20, 20, 0.11f),   // 20라운드 보스(박은석) — 21라운드 시작 때 끔. 평균 −14.2dB → 0.11이면 ≈ −33.4dB
    };
    const float FadeSeconds = 1.75f;
    const bool BgmOn = true;

    static GameBgm instance;
    AudioSource source;
    readonly AudioClip[] clips = new AudioClip[Cues.Length];
    RoundManager roundManager;
    float nextFind;
    int current = -1;                // 지금 틀고 있는(또는 줄이는 중인) 곡 — -1이면 없음
    bool fading;
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
        for (int i = 0; i < Cues.Length; i++)
        {
            clips[i] = Resources.Load<AudioClip>(Cues[i].Track);
            if (clips[i] == null) Debug.LogWarning($"[소리] 배경음악 Resources/{Cues[i].Track} 를 못 찾았습니다.");
            else Debug.Log($"[소리] 배경음악 {clips[i].name}({clips[i].length:0}초, 볼륨 {Cues[i].Volume:0.00}) — {Cues[i].FromRound}~{Cues[i].ToRound}라운드");
        }
        source = gameObject.AddComponent<AudioSource>();
        source.loop = true;          // 구간이 곡보다 길면 되풀이 — 구간을 벗어날 때 페이드 후 정지
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;         // 효과음이 몰려도 배경음악이 밀려나지 않게
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // 새 판 — 처음부터 다시(첫 씬 로드도 여기 한 번 오지만 아직 아무것도 안 튼 상태라 그대로다)
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetForNewGame();

    void ResetForNewGame()
    {
        if (source == null) return;
        source.Stop();
        source.clip = null;
        current = -1;
        fading = false;
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

    static int CueFor(int round)
    {
        for (int i = 0; i < Cues.Length; i++)
            if (round >= Cues[i].FromRound && round <= Cues[i].ToRound) return i;
        return -1;
    }

    void Update()
    {
        if (source == null) return;

        int effective = 0;
        if (TryReadRound(out int round, out bool preparing))
        {
            // 라운드 번호가 되돌아갔다 = 씬 로드 없이 새 판이 시작됐다 → 처음부터
            if (round < lastRound) ResetForNewGame();
            lastRound = round;
            effective = preparing ? round - 1 : round;   // 다음 라운드 준비 중 = 아직 앞 라운드 구간
        }

        int want = CueFor(effective);
        if (want >= 0 && clips[want] == null) want = -1;
        if (want != current && current >= 0 && !fading) fading = true;   // 구간을 벗어남 → 줄여서 끈다

        if (fading)
        {
            source.volume -= Cues[current].Volume / FadeSeconds * Time.unscaledDeltaTime;
            if (source.volume > 0f) return;
            source.Stop();
            source.clip = null;
            current = -1;
            fading = false;
        }

        if (current < 0 && want >= 0)   // 새 구간 — 처음부터
        {
            current = want;
            source.clip = clips[want];
            source.volume = Cues[want].Volume;
            source.time = 0f;
        }
        if (current < 0) return;

        bool on = GameSound.Enabled;
        if (on && !source.isPlaying) source.UnPause();
        if (on && !source.isPlaying) source.Play();   // 아직 한 번도 안 틀었으면 UnPause로는 안 돈다
        else if (!on && source.isPlaying) source.Pause();
    }

    static string Describe() =>
        instance == null || instance.source == null ? "배경음악 없음"
            : instance.source.clip == null ? $"배경음악 쉬는 중 · 라운드 {instance.lastRound}"
            : $"배경음악 {instance.source.clip.name}: 재생 {instance.source.isPlaying} · 위치 {instance.source.time:0.0}/{instance.source.clip.length:0.0}초 · 볼륨 {instance.source.volume:0.000} · 되풀이 {instance.source.loop} · 줄이는 중 {instance.fading} · 라운드 {instance.lastRound}";
}
