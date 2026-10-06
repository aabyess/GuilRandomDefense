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
/// 표는 **위에서부터** 맞는 첫 줄을 쓴다 — 보스 곡을 라운드 브금(넓은 구간)보다 위에 둔다.
/// Resume 줄(라운드 브금, 10-01 사장님 「②」)은 구간을 벗어날 때 끄지 않고 멈춘 자리를 기억했다가, 돌아오면 그 자리에서 잇는다(보스전 동안 멈춤).
/// </summary>
public class GameBgm : MonoBehaviour
{
    struct Cue
    {
        public string Track; public int FromRound; public int ToRound; public float Volume; public bool Resume; public bool Once; public bool Hold;
        public Cue(string track, int from, int to, float volume, bool resume = false, bool once = false, bool hold = false) { Track = track; FromRound = from; ToRound = to; Volume = volume; Resume = resume; Once = once; Hold = hold; }
    }

    // Once 줄(첫 곡·보스 곡, 10-02 사장님)은 한 번만 끝까지 틀고, 끝나면 그 줄을 건너뛴다 → 구간이 남았으면 라운드 브금(Resume)이 멈춘 자리에서 이어진다.
    // Hold 줄(보스 곡, 10-02 사장님 「3~4분이라 보스 라운드 끝나도 계속」)은 구간을 벗어나도 끝까지 틀고 끝난 뒤에 라운드 브금이 이어진다.
    // 볼륨 계산(사장님 「시끄럽다」 10-01): binks_sake는 평균 −27.6dB라 0.5를 곱하면 ≈ −33.6dB.
    // 보스 테마(Zoltraak 앞 2:15)는 평균 −13.1dB로 14.5dB 크다 → 0.1이면 ≈ −33.1dB로 맞춘 것. 어림값이니 사장님이 들어 보고 조절.
    static readonly Cue[] Cues =
    {
        // 첫 화면 곡(사장님 10-06 「게임 시작 전까지 브금」) — NetBoot(첫 화면·대기실) 동안만 되풀이, 게임 씬으로 넘어가면 끈다.
        //   Alexander Nakarada 「Tavern Tales」 CC BY 4.0(free-stock-music.com 공식 배포본, 사장님 「선술집 느낌」) — 출처는 CreditsSplash에 적는다. 평균 −15.1dB → 0.12면 ≈ −33.5dB.
        new Cue("Music/title_tavern_tales", TitleRound, TitleRound, 0.12f),
        new Cue("Music/binks_sake", 0, 1, 0.5f, once: true),             // 첫 화면 ~ 1라운드(2라운드 시작 때 끔)
        new Cue("Music/boss_r10_zoltraak", 10, 10, 0.1f, once: true, hold: true),   // 첫 보스(10라운드 주영호) — 11라운드 시작 때 끔
        new Cue("Music/boss_r20_journey", 20, 20, 0.11f, once: true, hold: true),   // 20라운드 보스(박은석) — 21라운드 시작 때 끔. 평균 −14.2dB → 0.11이면 ≈ −33.4dB
        new Cue("Music/boss_r30_hacking", 30, 30, 0.19f, once: true, hold: true),   // 30라운드 보스(김만경) — 31라운드 시작 때 끔. 평균 −18.9dB → 0.19이면 ≈ −33.3dB
        new Cue("Music/boss_r40_silent_solitude", 40, 40, 0.07f, once: true, hold: true),   // 40라운드 보스(김용태) — 평균 −10.2dB → 0.07이면 ≈ −33.3dB
        new Cue("Music/boss_r50_gotoubun", 50, 50, 0.07f, once: true, hold: true),   // 50라운드 보스(이태훈) — 평균 −10.1dB → 0.07이면 ≈ −33.2dB
        new Cue("Music/boss_r60_departure", 60, 60, 0.07f, once: true, hold: true),   // 60라운드 보스(정윤식) — 평균 −10.6dB → 0.07이면 ≈ −33.7dB
        new Cue("Music/boss_r65_danganronpa", 65, 65, 0.065f, once: true, hold: true),   // 65라운드 보스(이승우) — 평균 −9.8dB → 0.065이면 ≈ −33.5dB
        new Cue("Music/boss_r70_jonathan", 70, 70, 0.075f, once: true, hold: true),   // 70라운드 보스(신지우) — 평균 −11.4dB → 0.075이면 ≈ −33.9dB
        new Cue("Music/boss_r75_johnny", 75, 75, 0.085f, once: true, hold: true),   // 75라운드 마지막 보스(이이삭) — 평균 −12.3dB → 0.085이면 ≈ −33.7dB
        // 라운드 브금(10-01) — 2~59라운드(60라운드 대기 시간까지), 보스 라운드 동안 멈췄다가 이어서. 31분 곡이라 ≈R53까지 한 바퀴, 그 뒤 되풀이.
        //   사장님 10-01 「60라운드 이후부터는 배경으로 깔리는 브금을 꺼」 — 60R부터는 보스 곡(60·65·70·75)만 나온다.
        //   평균 −20.7dB → 0.2이면 ≈ −34.7dB(깔리는 곡이라 보스 곡보다 1dB쯤 작게).
        new Cue("Music/round_fourth_layer", 2, 59, 0.2f, resume: true),
    };
    const int TitleRound = -1;       // 첫 화면(NetBoot, 빌드 0번 씬)은 라운드 −1로 친다
    const float FadeSeconds = 1.75f;
    const bool BgmOn = true;

    static GameBgm instance;
    AudioSource source;
    readonly AudioClip[] clips = new AudioClip[Cues.Length];
    RoundManager roundManager;
    float nextFind;
    int current = -1;                // 지금 틀고 있는(또는 줄이는 중인) 곡 — -1이면 없음
    readonly float[] savedTime = new float[Cues.Length];   // Resume 곡이 멈춘 자리
    float pendingSeek = -1f;         // Play 직후에 옮길 자리(스트리밍 클립은 Play 전 time이 안 먹을 수 있다)
    bool fading;
    readonly bool[] finishedCue = new bool[Cues.Length];   // Once 곡이 끝까지 나왔다 — 다시 틀지 않는다
    float lastTime;                  // 직전 프레임 재생 위치(끝났는지 판정용)
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
        System.Array.Clear(finishedCue, 0, finishedCue.Length);
        lastTime = 0f;
        lastRound = 0;
        System.Array.Clear(savedTime, 0, savedTime.Length);
        pendingSeek = -1f;
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

    int CueFor(int round)
    {
        for (int i = 0; i < Cues.Length; i++)
            if (!finishedCue[i] && round >= Cues[i].FromRound && round <= Cues[i].ToRound) return i;
        return -1;
    }

    void Update()
    {
        if (source == null) return;

        int effective = 0;
        if (SceneManager.GetActiveScene().buildIndex == 0) effective = TitleRound;   // 첫 화면·대기실
        else if (TryReadRound(out int round, out bool preparing))
        {
            // 라운드 번호가 되돌아갔다 = 씬 로드 없이 새 판이 시작됐다 → 처음부터
            if (round < lastRound) ResetForNewGame();
            lastRound = round;
            effective = preparing ? round - 1 : round;   // 다음 라운드 준비 중 = 아직 앞 라운드 구간
        }

        int want = CueFor(effective);
        if (want >= 0 && clips[want] == null) want = -1;
        if (want != current && current >= 0 && !fading && !Cues[current].Hold) fading = true;   // 구간을 벗어남 → 줄여서 끈다

        if (fading)
        {
            source.volume -= Cues[current].Volume / FadeSeconds * Time.unscaledDeltaTime;
            if (source.volume > 0f) return;
            if (Cues[current].Resume) savedTime[current] = source.time;
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
            lastTime = 0f;
            pendingSeek = Cues[want].Resume ? savedTime[want] : -1f;
        }
        if (current < 0) return;

        bool on = GameSound.Enabled;
        source.loop = !Cues[current].Once;
        if (Cues[current].Once)
        {
            if (on && !source.isPlaying && lastTime > clips[current].length - 0.5f)   // 끝까지 나온 뒤 멈춤 — 이 줄은 끝, 다음 줄로
            {
                finishedCue[current] = true;
                source.Stop();
                source.clip = null;
                current = -1;
                return;
            }
            if (source.isPlaying) lastTime = source.time;
        }
        if (on && !source.isPlaying) source.UnPause();
        if (on && !source.isPlaying) source.Play();   // 아직 한 번도 안 틀었으면 UnPause로는 안 돈다
        else if (!on && source.isPlaying) source.Pause();
        if (pendingSeek > 0f && source.isPlaying) { source.time = Mathf.Min(pendingSeek, source.clip.length - 0.1f); pendingSeek = -1f; }
    }

    static string Describe() =>
        instance == null || instance.source == null ? "배경음악 없음"
            : instance.source.clip == null ? $"배경음악 쉬는 중 · 라운드 {instance.lastRound}"
            : $"배경음악 {instance.source.clip.name}: 재생 {instance.source.isPlaying} · 위치 {instance.source.time:0.0}/{instance.source.clip.length:0.0}초 · 볼륨 {instance.source.volume:0.000} · 되풀이 {instance.source.loop} · 줄이는 중 {instance.fading} · 라운드 {instance.lastRound}";
}
