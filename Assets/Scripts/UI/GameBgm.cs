using UnityEngine;

/// <summary>
/// 배경음악(사장님 지시 2026-09-30 「기본적인 배경음악이 깔렸으면」). Resources/Sounds/Bgm/ 아래 한 곡을 처음부터 끝까지 되풀이한다.
/// 첫 화면(멀티 NetBoot)부터 게임 끝까지 끊기지 않게 씬과 무관하게 스스로 붙는다(GameVersion과 같은 방식).
/// 메뉴의 「소리 끄기」(<see cref="GameSound.Enabled"/>)를 그대로 따른다 — 끄면 멈추고, 켜면 멈춘 자리에서 이어진다.
/// 곡을 바꾸려면 파일을 넣고 <see cref="Track"/>만 고친다. 볼륨은 효과음·획득 음성(0.7)에 묻히지 않게 낮게 둔다.
/// </summary>
public class GameBgm : MonoBehaviour
{
    const string Track = "Sounds/Bgm/binks_sake";
    const float Volume = 0.22f;

    static GameBgm instance;
    AudioSource source;

    /// <summary>지금 실제로 나고 있나(테스트용).</summary>
    public static bool IsPlaying => instance != null && instance.source != null && instance.source.isPlaying;

    /// <summary>재생 위치(초, 테스트용) — 시간이 흐르는지로 「정말 도는지」를 본다.</summary>
    public static float Position => instance != null && instance.source != null ? instance.source.time : -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
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
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = Volume;
        source.priority = 0;   // 효과음이 몰려도 배경음악이 밀려나지 않게
        Debug.Log($"[소리] 배경음악 {clip.name}({clip.length:0}초, 볼륨 {Volume:0.00})");
    }

    void Update()
    {
        if (source == null) return;
        bool want = GameSound.Enabled;
        if (want && !source.isPlaying) source.UnPause();
        if (want && !source.isPlaying) source.Play();   // 아직 한 번도 안 틀었으면 UnPause로는 안 돈다
        else if (!want && source.isPlaying) source.Pause();
    }

    static string Describe() =>
        instance == null || instance.source == null ? "배경음악 없음"
            : $"배경음악 {instance.source.clip.name}: 재생 {instance.source.isPlaying} · 위치 {instance.source.time:0.0}/{instance.source.clip.length:0.0}초 · 볼륨 {instance.source.volume:0.00} · 되풀이 {instance.source.loop}";
}
