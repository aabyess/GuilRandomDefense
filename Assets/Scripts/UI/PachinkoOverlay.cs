using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 30R 보스 파칭코 화면 오버레이(사장님 10-14, 시안 5 https://claude.ai/artifact/Y1Lwz6UjzAzchRANw58ACR). 규칙·네트워크는 PachinkoController(구현담당1)가 하고,
/// 이 오버레이는 <see cref="PachinkoController.PlayHandler"/>(내 plan 연출 시작) · <see cref="PachinkoController.PushedHandler"/>(내 PUSH 눌림/시간 끝)에만 붙는다.
///
/// 타이밍은 <see cref="PachinkoPlan.PreludeSeconds"/>와 같은 길이로 짰다 — 보류 0.9 + 세 줄 정지 2.4 + (리치 1.3 + 継続 N×1.05 + SP 배경 단계×0.75 + 슬롯 1.8 + 컷인 1.6) 뒤 PUSH 창이 열린다.
/// 화면 글자는 전부 일본어. 그림 재료는 Assets/Resources/Pachinko/(blender ~/GRD_pachinko_art 반입 · 폰트 Dela Gothic One OFL).
/// 게임 시간이 서 있으므로(GamePause) 전부 unscaled 시간이다.
///
/// 🔸 SP 리치 애니메이션 자리(사장님이 파일을 따로 주실 예정): <see cref="SpReachSlot"/>(RectTransform)에 <see cref="SpReachPlayer"/>를 걸면
///    SP 리치 구간(배경이 빨강·금일 때 1.8초)에 그 델리게이트가 (슬롯, 배경색, 길이)로 불린다. 안 걸려 있으면 「SPリーチ演出」 표시 칸만 보인다.
/// </summary>
public class PachinkoOverlay : MonoBehaviour
{
    const string Folder = "Pachinko/";
    const float SpSeconds = 1.8f;

    /// <summary>SP 리치 구간에 불린다: (애니메이션을 올릴 슬롯, 이 판의 SP 배경 단계, 길이 초). 호출한 쪽이 슬롯 안에 마음대로 그리고 끝나면 지운다(오버레이가 길이 뒤 슬롯 자식을 비운다).</summary>
    public static Action<RectTransform, PachinkoBg, float> SpReachPlayer;
    /// <summary>SP 리치 애니메이션을 올릴 칸(오버레이가 한 번 열린 뒤 생긴다).</summary>
    public static RectTransform SpReachSlot { get; private set; }
    /// <summary>켬/끔(끄면 PlayHandler를 안 걸어 컨트롤러의 임시 경로로 간다).</summary>
    public static bool Enabled = true;

    static PachinkoOverlay instance;
    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // ───────── 레이아웃(machine_layout.json, 1920×1080) ─────────
    [Serializable] class Rect4 { public float x, y, w, h; }
    [Serializable] class ReelRect { public int index; public float x, y, w, h; }
    [Serializable] class HoldRect { public float x, y, r; }
    [Serializable] class Layout { public Rect4 machine, marquee; public ReelRect[] reels; public HoldRect[] holds; public float paylineY; }
    Layout layout;

    // ───────── 화면 요소 ─────────
    GameObject host;
    RectTransform root, stage;
    Image missImage, eyeGlow, kanekiImage, vignette, heartRed, burstGlow, backdrop, flash, dim, ringImage, pushBtnImage, gaugeFill, bandImage, cutBand;
    RectTransform shatterRoot, kanekiRect, kanekiPic, shardRoot, speedRect, holdsRect, machineRect, frameGroup, pushGroup, bandRect, cutRect, spSlotPanel, raysRect, ticker;
    TMP_Text pauseText, tickerText, meterText, pushCount, pushKeyText, bandText, cutText, spText, yokokuSmall, chanceText;
    Image[] holdOrbs;
    Image holdRing;
    Image logoReach, logoKeizoku, logoGekiatsu, logoFukkatsu, logoOoatari, logoAtari, logoZannen;
    Reel[] reels;
    readonly List<Image> particlePool = new List<Image>();
    TMP_FontAsset font;
    Sprite circle;
    AudioSource audioSource;

    // 배경 영상 15초(사장님 10-14: 룰렛이 돌기 시작하면 깐다, 음악 포함판 — StreamingAssets)
    const string BgVideoFile = "pachinko_bg_15s.mp4";   // 무음판 — 음악은 별도 AudioSource(MusicClip)
    const float BgVideoSeconds = 15f, BgVideoFade = 0.5f;
    /// <summary>녹화 모드(PachinkoRecorder): 영상을 게임 시각에 묶는다.</summary>
    public static bool RecordMode;
    /// <summary>녹화용: (소리 이름, 게임 시각, 음량) — 대사·영상 음악이 울린 순간.</summary>
    public static Action<string, float, float> AudioEvent;
    UnityEngine.Video.VideoPlayer videoPlayer;
    RenderTexture videoRT;
    RawImage videoView;
    Image videoDim;
    AudioSource videoAudio;
    CanvasGroup kanekiGroup;
    float videoShow, videoShowTarget, videoStartTime;
    bool videoStopRequested;
    Coroutine videoCo;

    // 테마 투표 화면
    RectTransform voteRoot, voteStage;
    TMP_Text voteTimer, voteBarTg, voteBarRz, votePick, votePickSub;
    TMP_Text[] voteBadges = new TMP_Text[2];
    RectTransform[] voteFill = new RectTransform[2];
    Image[] voteCardImg = new Image[2];
    Image voteBarTgImg, voteBarRzImg, votePickBg;
    readonly VoteCard[] voteCards = new VoteCard[2];
    readonly int[] voteBy = new int[16];
    int voteLocalSlot, voteMine;   // voteMine: 0 무투표 1 東京 2 Re:ゼロ
    float voteEndTime;
    bool voteStandalone, voteOpen;
    static string activeSymbolDir = "Symbols";
    int curTheme;

    // Re:ゼロ 테마: 영상 파일·결과 영상(꽝)
    string videoFileName = BgVideoFile;
    bool videoNatural;
    UnityEngine.Video.VideoPlayer videoPlayer2;
    AudioSource videoAudio2;
    // 도쿄구울 음악: 영상은 15초에서 끝나도 음악은 결과 연출이 끝날 때까지(68초까지) 계속
    AudioSource musicSource;
    float musicFade = 1f;
    bool musicOn;
    public static AudioClip MusicClip;
    public static AudioClip[] RzMissLines = new AudioClip[2];
    public static AudioClip[] RzWinLines = new AudioClip[9];   // 리제로 당첨 대사(사장님 지정 전 — 비어 있음)

    // 영상 타격 연출(사장님 10-14): 공격·피격 순간마다 배경만 번쩍/「두둥」 — Resources/Pachinko/Impacts/<영상>.json
    [Serializable] class ImpactItem { public float t, strength; public string kind; }
    [Serializable] class ImpactList { public ImpactItem[] items; }
    ImpactItem[] impactTrack = new ImpactItem[0];
    int impactIdx, impactSource;   // 1 = 배경 영상 · 2 = 결과 영상
    float impactBase;
    RawImage ghostR, ghostC;
    Coroutine impactCo;

    RectTransform fireworkRoot;
    readonly List<Image> fwPool = new List<Image>();

    Coroutine running;
    bool standalone;          // 컨트롤러 없이 시험(Preview) — PUSH를 스스로 닫는다
    int pushedWho = int.MinValue;   // 내 PUSH 결과(누름 ≥0 / 자동 -1). int.MinValue = 아직
    float bgPulse, shakeUntil;
    Color bgTint = new Color(0.04f, 0.015f, 0.07f, 0.97f), bgTarget;
    bool pushFlashing;
    bool win;

    // ───────── 훅 연결 ─────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (!Enabled) return;
        PachinkoController.PlayHandler = plan => Ensure().Begin(plan, false);
        PachinkoController.PushedHandler = who => { if (instance != null) instance.pushedWho = who; };
        PachinkoController.VoteOpenHandler = secs => Ensure().OpenVoteUi(secs, false);
        PachinkoController.VoteTallyHandler = (by, local) => { if (instance != null) instance.SetTally(by, local); };
        PachinkoController.VoteCloseHandler = th => CloseVote(th);
    }

    // ───────── 테마 투표(사장님 10-14): 30R 보스 처치 직후 「パチンコ モード 選択」 — 東京喰種 / Re:ゼロ, 꾹 누르기 1초 ─────────
    /// <summary>투표 결과 테마(0=東京喰種 · 1=Re:ゼロ). PachinkoPlan.Roll이 plan.theme에 넣는다.</summary>
    public static int Theme;
    public static void OpenVote(float seconds) => Ensure().OpenVoteUi(seconds, false);
    public static void SetVoteTally(int[] bySlot, int localSlot) { if (instance != null) instance.SetTally(bySlot, localSlot); }
    public static void CloseVote(int theme) { Theme = theme; if (instance != null && instance.voteRoot.gameObject.activeSelf) instance.StartCoroutine(instance.VoteResult(theme)); }
    /// <summary>혼자 시험(촬영): 10초 모의 투표, 봇 3명이 중간에 표를 던지고 마감 때 표 비율로 한쪽을 뽑는다.</summary>
    public static void PreviewVote() => Ensure().OpenVoteUi(10f, true);
    public static bool IsVoting => instance != null && instance.voteRoot != null && instance.voteRoot.gameObject.activeSelf;

    /// <summary>컨트롤러 없이 한 판 보기(개발·촬영): PUSH 5초 뒤 스스로 결과로 간다. Space·클릭도 먹는다.</summary>
    public static void Preview(PachinkoPlan plan) { Theme = plan.theme; Ensure().Begin(plan, true); }

    public static bool IsPlaying => instance != null && instance.running != null;
    /// <summary>배경 영상이 지금 재생 중인가(FPS 측정용).</summary>
    public static bool BgVideoPlaying => instance != null && instance.videoPlayer != null && instance.videoPlayer.isPlaying;
    public static long BgVideoFrame => instance != null && instance.videoPlayer != null ? instance.videoPlayer.frame : -1;
    public static double BgVideoTime => instance != null && instance.videoPlayer != null ? instance.videoPlayer.time : -1;

    static PachinkoOverlay Ensure()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("[PachinkoOverlay]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PachinkoOverlay>();
        instance.host = go;
        instance.Build();
        return instance;
    }

    // ───────── 만들기 ─────────
    static Sprite Spr(string path)
    {
        if (spriteCache.TryGetValue(path, out Sprite s)) return s;
        s = Resources.Load<Sprite>(Folder + path);
        if (s == null)
        {
            Texture2D t = Resources.Load<Texture2D>(Folder + path);   // 임포트가 아직 스프라이트로 안 바뀐 경우
            if (t != null) s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        spriteCache[path] = s;
        return s;
    }

    RectTransform Rt(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>1920×1080 왼쪽 위 원점 픽셀 좌표로 놓는다.</summary>
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x + w * 0.5f, -(y + h * 0.5f));
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
    {
        RectTransform r = Rt(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = raycast; img.preserveAspect = sprite != null;
        return img;
    }

    TMP_Text Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, float outline = 0f)
    {
        RectTransform r = Rt(name, parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow;
        if (outline > 0f) { t.outlineWidth = outline; t.outlineColor = new Color32(30, 0, 12, 255); }
        return t;
    }

    void Build()
    {
        font = Resources.Load<TMP_FontAsset>(Folder + "DelaGothicOne SDF") ?? Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");
        if (font != null)
        {   // 한글 안내문은 Pretendard로 폴백(Dela Gothic One엔 한글이 없다)
            var ko = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");
            if (ko != null && font.fallbackFontAssetTable != null && !font.fallbackFontAssetTable.Contains(ko)) font.fallbackFontAssetTable.Add(ko);
        }
        TextAsset layoutJson = Resources.Load<TextAsset>(Folder + "machine_layout");
        layout = layoutJson != null ? JsonUtility.FromJson<Layout>(layoutJson.text) : null;
        if (layout == null || layout.reels == null || layout.reels.Length < 3)
        {
            layout = new Layout
            {
                machine = new Rect4 { x = 330, y = 190, w = 1260, h = 760 }, marquee = new Rect4 { x = 580, y = 230, w = 760, h = 60 }, paylineY = 576,
                reels = new[] { new ReelRect { index = 0, x = 414, y = 344, w = 349, h = 464 }, new ReelRect { index = 1, x = 785, y = 344, w = 349, h = 464 }, new ReelRect { index = 2, x = 1157, y = 344, w = 349, h = 464 } },
                holds = new[] { new HoldRect { x = 870, y = 892, r = 15 }, new HoldRect { x = 930, y = 892, r = 15 }, new HoldRect { x = 990, y = 892, r = 15 }, new HoldRect { x = 1050, y = 892, r = 15 } },
            };
        }
        circle = MakeCircle();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;   // 컷인(4000) 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.ignoreListenerPause = true; audioSource.spatialBlend = 0f;

        root = Rt("Root", transform); Stretch(root);
        backdrop = Img("backdrop", root, null, bgTint, raycast: true);   // 화면 전체를 막는다(뒤 클릭 차단)
        Stretch(backdrop.rectTransform);

        // 룰렛 배경 영상(맨 뒤): 세로에 맞추고 좌우를 자른다(cover) + 숫자가 읽히게 어두운 막
        videoRT = new RenderTexture(1920, 820, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "PachinkoBgVideo", filterMode = FilterMode.Bilinear, useMipMap = false, autoGenerateMips = false, antiAliasing = 1 };   // 영상 원본 크기 그대로(축소·재압축 없음)
        var vgo = new GameObject("bgVideo"); vgo.transform.SetParent(transform, false);
        videoPlayer = vgo.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer.playOnAwake = false; videoPlayer.isLooping = false; videoPlayer.waitForFirstFrame = true;
        videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture; videoPlayer.targetTexture = videoRT;
        videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
        videoAudio = vgo.AddComponent<AudioSource>(); videoAudio.playOnAwake = false; videoAudio.ignoreListenerPause = true; videoAudio.spatialBlend = 0f;
        videoPlayer.SetTargetAudioSource(0, videoAudio);
        videoView = Rt("videoView", root).gameObject.AddComponent<RawImage>(); videoView.texture = videoRT; videoView.raycastTarget = false;
        videoView.rectTransform.anchorMin = videoView.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        videoView.rectTransform.sizeDelta = new Vector2(1080f * 1920f / 820f, 1080f);   // 높이에 맞춤
        videoView.color = new Color(1f, 1f, 1f, 0f);
        videoDim = Img("videoDim", root, null, new Color(0f, 0f, 0f, 0f)); Stretch(videoDim.rectTransform);
        ghostR = Rt("ghostR", root).gameObject.AddComponent<RawImage>(); ghostR.texture = videoRT; ghostR.raycastTarget = false; ghostR.color = new Color(1f, 0f, 0f, 0f);
        ghostC = Rt("ghostC", root).gameObject.AddComponent<RawImage>(); ghostC.texture = videoRT; ghostC.raycastTarget = false; ghostC.color = new Color(0f, 1f, 1f, 0f);
        foreach (RawImage g in new[] { ghostR, ghostC }) { g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); g.rectTransform.sizeDelta = videoView.rectTransform.sizeDelta; }
        ghostR.transform.SetSiblingIndex(videoView.transform.GetSiblingIndex() + 1); ghostC.transform.SetSiblingIndex(videoView.transform.GetSiblingIndex() + 2);

        // 결과 영상용 두 번째 플레이어(리제로 꽝 6.8초) — 같은 RenderTexture에 그린다
        var vgo2 = new GameObject("resultVideo"); vgo2.transform.SetParent(transform, false);
        videoPlayer2 = vgo2.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer2.playOnAwake = false; videoPlayer2.isLooping = false; videoPlayer2.waitForFirstFrame = true;
        videoPlayer2.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture; videoPlayer2.targetTexture = videoRT;
        videoPlayer2.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
        videoAudio2 = vgo2.AddComponent<AudioSource>(); videoAudio2.playOnAwake = false; videoAudio2.ignoreListenerPause = true; videoAudio2.spatialBlend = 0f;
        videoPlayer2.SetTargetAudioSource(0, videoAudio2);
        musicSource = gameObject.AddComponent<AudioSource>(); musicSource.playOnAwake = false; musicSource.loop = false; musicSource.ignoreListenerPause = true; musicSource.spatialBlend = 0f;

        // 1920×1080 고정 무대(화면 비율이 달라도 맞춰 줄인다)
        stage = Rt("stage", root);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f); stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(1920f, 1080f); stage.anchoredPosition = Vector2.zero;

        // 햇살(당첨 때 돈다)
        raysRect = Rt("rays", stage); Place(raysRect, 960 - 1100, 540 - 1100, 2200, 2200);
        for (int i = 0; i < 12; i++)
        {
            Image ray = Img("ray" + i, raysRect, null, new Color(1f, 0.82f, 0.25f, 0.28f));
            ray.rectTransform.sizeDelta = new Vector2(150f, 1100f);
            ray.rectTransform.pivot = new Vector2(0.5f, 0f);
            ray.rectTransform.anchoredPosition = new Vector2(1100f, 1100f);
            ray.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 30f);
            ray.rectTransform.anchorMin = ray.rectTransform.anchorMax = Vector2.zero;
        }
        raysRect.gameObject.SetActive(false);

        // 카네키 그림(숫자 릴 뒤 배경, 사장님 10-14): 심장박동 → 꽝이면 산산조각, 당첨이면 win 그림으로
        kanekiRect = Rt("kaneki", stage); Place(kanekiRect, 0, 0, 1920, 1080);
        kanekiPic = Rt("pic", kanekiRect); Place(kanekiPic, 960 - 440, 0, 880, 1080);
        kanekiImage = kanekiPic.gameObject.AddComponent<Image>(); kanekiImage.sprite = Spr("Scene/helpme"); kanekiImage.raycastTarget = false; kanekiImage.preserveAspect = true;
        missImage = Img("miss", kanekiRect, Spr("Scene/pachinko_miss"), Color.white); Place(missImage.rectTransform, 960 - 800, 540 - 450, 1600, 900); missImage.enabled = false;
        eyeGlow = Img("eye", kanekiRect, circle, new Color(1f, 0.1f, 0.1f, 0f)); Place(eyeGlow.rectTransform, 1181 - 60, 419 - 60, 120, 120);
        heartRed = Img("heartRed", kanekiRect, null, new Color(0.8f, 0f, 0.05f, 0f)); Stretch(heartRed.rectTransform); heartRed.rectTransform.sizeDelta = new Vector2(2400, 1400);
        shardRoot = Rt("shards", kanekiRect); Place(shardRoot, 0, 0, 1920, 1080);
        vignette = Img("vignette", kanekiRect, MakeVignette(), Color.white); Place(vignette.rectTransform, -200, -120, 2320, 1320); vignette.preserveAspect = false;
        burstGlow = Img("burst", kanekiRect, circle, new Color(1f, 0.45f, 0.1f, 0f)); Place(burstGlow.rectTransform, 1100, 200, 500, 500);
        kanekiGroup = kanekiRect.gameObject.AddComponent<CanvasGroup>();
        kanekiRect.gameObject.SetActive(false);
        speedRect = Rt("speedLines", stage); Place(speedRect, 960 - 1100, 540 - 1100, 2200, 2200);
        for (int i = 0; i < 40; i++)
        {
            float ang = i * (360f / 40f) + UnityEngine.Random.Range(-3f, 3f), rad = ang * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float re = Mathf.Min(960f / Mathf.Max(0.001f, Mathf.Abs(d.x)), 540f / Mathf.Max(0.001f, Mathf.Abs(d.y))) * 1.04f;   // 화면 가장자리까지의 거리
            float len = UnityEngine.Random.Range(160f, 420f);
            Image ln = Img("ln" + i, speedRect, null, new Color(1f, 1f, 1f, 0.5f));
            ln.rectTransform.anchorMin = ln.rectTransform.anchorMax = Vector2.zero; ln.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            ln.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(2f, 5f), len);
            ln.rectTransform.anchoredPosition = new Vector2(1100f, 1100f) + d * (re - len * 0.5f);
            ln.rectTransform.localRotation = Quaternion.Euler(0, 0, ang - 90f);
        }
        speedRect.gameObject.SetActive(false);

        // 위 띠(흐르는 글자)
        ticker = Rt("ticker", stage); Place(ticker, 0, 100, 1920, 64);
        var tickBg = ticker.gameObject.AddComponent<Image>(); tickBg.color = new Color(0.78f, 0.1f, 0.45f, 0.92f); tickBg.raycastTarget = false;
        ticker.gameObject.AddComponent<RectMask2D>();
        tickerText = Label("tickerText", ticker, "★ G.R.D 大海賊フィーバー ★ BOSS DOWN! 運命のルーレット ★ 揃えば大当たり ★ G.R.D 大海賊フィーバー ★ BOSS DOWN! 運命のルーレット ★ 揃えば大当たり ★", 36f, Color.white, TextAlignmentOptions.MidlineLeft, 0.18f);
        tickerText.rectTransform.anchorMin = new Vector2(0, 0); tickerText.rectTransform.anchorMax = new Vector2(0, 1);
        tickerText.rectTransform.pivot = new Vector2(0, 0.5f); tickerText.rectTransform.sizeDelta = new Vector2(4600, 0);

        // 정지 안내
        pauseText = Label("pause", stage, "⏸ 전원 일시정지", 34f, Color.white, TextAlignmentOptions.Center, 0.15f);
        Place(pauseText.rectTransform, 660, 30, 600, 56);

        // 기계 = 바깥 장식 테두리(금 틀 바깥만) + 큰 숫자 세 줄(창 없이 화면 가운데를 크게 차지, 사장님 10-14 「실제 파칭코처럼」)
        machineRect = Rt("machine", stage); Place(machineRect, 0, 0, 1920, 1080);
        frameGroup = Rt("frameGroup", machineRect); Place(frameGroup, 0, 0, 1920, 1080);
        frameGroup.localScale = Vector3.one * 1.9f; frameGroup.anchoredPosition += new Vector2(0f, 20f);   // 금 틀은 화면 바깥으로 밀어 얇은 테두리만 보이게
        Image frame = Img("frame", frameGroup, Spr("machine_border") ?? Spr("machine_frame"), new Color(1f, 1f, 1f, 0.62f));
        Place(frame.rectTransform, 0, 0, 1920, 1080);
        holdsRect = Rt("holds", stage); Place(holdsRect, 0, 0, 1920, 1080);
        // 보류 구슬은 하나만(사장님 10-14): 조금 크게 — 색(흰·파랑·초록·빨강·금)이 기대도 힌트, 빨강·금은 더 강하게 맥동
        holdRing = Img("holdRing", holdsRect, circle, new Color(1f, 1f, 1f, 0f)); Place(holdRing.rectTransform, 960 - 70, 1000 - 70, 140, 140);
        holdOrbs = new Image[1];
        holdOrbs[0] = Img("hold", holdsRect, circle, new Color(0.62f, 0.66f, 0.74f, 0.95f));
        Place(holdOrbs[0].rectTransform, 960 - 38, 1000 - 38, 76, 76);
        reels = new Reel[3];
        for (int i = 0; i < 3; i++) reels[i] = new Reel(this, machineRect, i);

        // 연출 글자 그림(logos)
        logoReach = Logo("logo_reach", 960, 210, 1000); logoKeizoku = Logo("logo_keizoku", 960, 420, 900); logoGekiatsu = Logo("logo_gekiatsu", 960, 800, 900);
        logoFukkatsu = Logo("logo_fukkatsu", 960, 230, 1000); logoOoatari = Logo("logo_ooatari", 960, 200, 1100); logoAtari = Logo("logo_atari", 960, 200, 900); logoZannen = Logo("logo_zannen", 960, 220, 800);
        yokokuSmall = Label("yokokuSmall", stage, "", 44f, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center, 0.2f);
        Place(yokokuSmall.rectTransform, 560, 190, 800, 60); yokokuSmall.gameObject.SetActive(false);
        chanceText = Label("chance", stage, "CHANCE!", 54f, Color.white, TextAlignmentOptions.Center, 0.2f);
        Place(chanceText.rectTransform, 460, 140, 1000, 80); chanceText.gameObject.SetActive(false);

        // SP 리치 자리
        spSlotPanel = Rt("spSlot", stage); Place(spSlotPanel, 346, 173, 1228, 650);
        var spBg = spSlotPanel.gameObject.AddComponent<Image>(); spBg.color = new Color(0.1f, 0.04f, 0.16f, 0.95f); spBg.raycastTarget = false;
        spSlotPanel.gameObject.AddComponent<RectMask2D>();
        spText = Label("spText", spSlotPanel, "SPリーチ演出", 64f, new Color(1f, 0.82f, 0.25f), TextAlignmentOptions.Center, 0.15f);
        Stretch(spText.rectTransform);
        SpReachSlot = spSlotPanel;
        spSlotPanel.gameObject.SetActive(false);

        // 최종 컷인
        cutRect = Rt("cutin", stage); Place(cutRect, -160, 330, 2240, 340);
        cutBand = cutRect.gameObject.AddComponent<Image>(); cutBand.raycastTarget = false;
        cutText = Label("cutText", cutRect, "", 120f, Color.white, TextAlignmentOptions.Center, 0.22f);
        Stretch(cutText.rectTransform);
        cutRect.gameObject.SetActive(false);

        // PUSH
        pushGroup = Rt("push", stage); Place(pushGroup, 560, 140, 800, 800);
        ringImage = Img("ring", pushGroup, circle, new Color(1f, 0.82f, 0.25f, 0f)); Place(ringImage.rectTransform, 200, 130, 400, 400);
        pushBtnImage = Img("button", pushGroup, Spr("UI/push_button_normal"), Color.white, raycast: true);
        Place(pushBtnImage.rectTransform, 180, 110, 440, 440);
        var button = pushBtnImage.gameObject.AddComponent<Button>(); button.transition = UnityEngine.UI.Selectable.Transition.None;
        button.onClick.AddListener(OnPushClicked);
        Image keycap = Img("keycap", pushGroup, Spr("UI/keycap_space"), Color.white); Place(keycap.rectTransform, 150, 570, 300, 134);
        pushKeyText = Label("pushKey", pushGroup, "를 누르세요!", 56f, Color.white, TextAlignmentOptions.MidlineLeft, 0.22f); Place(pushKeyText.rectTransform, 470, 590, 380, 100);
        pushCount = Label("pushCount", pushGroup, "5", 150f, Color.white, TextAlignmentOptions.Center, 0.25f); Place(pushCount.rectTransform, 300, -40, 200, 160);
        Image gaugeBg = Img("gaugeBg", pushGroup, null, new Color(0f, 0f, 0f, 0.55f)); Place(gaugeBg.rectTransform, 150, 730, 500, 22);
        gaugeFill = Img("gaugeFill", gaugeBg.transform, null, new Color(1f, 0.55f, 0.15f));
        gaugeFill.rectTransform.anchorMin = Vector2.zero; gaugeFill.rectTransform.anchorMax = Vector2.one; gaugeFill.rectTransform.offsetMin = gaugeFill.rectTransform.offsetMax = Vector2.zero;
        dim = Img("pushDim", stage, null, new Color(0f, 0f, 0f, 0f)); Stretch(dim.rectTransform); dim.rectTransform.SetSiblingIndex(pushGroup.GetSiblingIndex());
        dim.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); dim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); dim.rectTransform.sizeDelta = new Vector2(2600, 1500);
        pushGroup.SetAsLastSibling();
        pushGroup.localScale = Vector3.one * 1.3f;   // 화면 가운데 크게
        pushGroup.gameObject.SetActive(false);

        // 보상 띠
        {   // 「번쩍임 줄이기」 토글(켜면 경고 스트로브 끄고 섬광 α 절반)
            Image rb = Img("reduceFlash", stage, null, new Color(0f, 0f, 0f, 0.4f), true);
            Place(rb.rectTransform, 24, 1030, 270, 40);
            TMP_Text rl = Label("reduceLabel", rb.transform, "", 24f, Color.white, TextAlignmentOptions.Center);
            Stretch(rl.rectTransform);
            void Paint() { rl.text = (ReduceFlash ? "☑ " : "☐ ") + "번쩍임 줄이기"; }
            Paint();
            rb.gameObject.AddComponent<Button>().onClick.AddListener(() => { ReduceFlash = !ReduceFlash; Paint(); });
        }
        bandRect = Rt("reward", stage); Place(bandRect, 192, 860, 1536, 150);
        bandImage = bandRect.gameObject.AddComponent<Image>(); bandImage.sprite = Spr("UI/reward_band"); bandImage.raycastTarget = false;
        bandText = Label("bandText", bandRect, "", 56f, Color.white, TextAlignmentOptions.Center, 0.2f); Stretch(bandText.rectTransform);
        bandRect.gameObject.SetActive(false);
        fireworkRoot = Rt("fireworks", stage); Place(fireworkRoot, 0, 0, 1920, 1080);   // 그림 위 · 보상 띠 아래
        fireworkRoot.SetSiblingIndex(bandRect.GetSiblingIndex());

        meterText = Label("meter", stage, "기대도 –", 34f, Color.white, TextAlignmentOptions.MidlineRight, 0.15f);
        Place(meterText.rectTransform, 1420, 990, 480, 60);

        shatterRoot = Rt("shatterRoot", root); Stretch(shatterRoot);   // 화면 전체 캡처를 깨는 층(스테이지 위, 섬광 아래)
        flash = Img("flash", root, null, new Color(1f, 1f, 1f, 0f)); Stretch(flash.rectTransform);

        BuildVoteUi();
        root.gameObject.SetActive(false);
    }

    // ───────── 투표 화면 ─────────
    class VoteCard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        public bool holding, clicked; public float progress, pressT = 1f;
        public void OnPointerDown(PointerEventData e) { holding = true; }
        public void OnPointerUp(PointerEventData e) { holding = false; }
        public void OnPointerExit(PointerEventData e) { holding = false; }
        public void OnPointerClick(PointerEventData e) { clicked = true; }   // 한 번 클릭으로 투표(바꿔 클릭 허용)
    }

    void BuildVoteUi()
    {
        voteRoot = Rt("voteRoot", transform); Stretch(voteRoot);
        var bg = voteRoot.gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.01f, 0.07f, 0.95f); bg.raycastTarget = true;
        voteStage = Rt("voteStage", voteRoot);
        voteStage.anchorMin = voteStage.anchorMax = new Vector2(0.5f, 0.5f); voteStage.pivot = new Vector2(0.5f, 0.5f);
        voteStage.sizeDelta = new Vector2(1920f, 1080f); voteStage.anchoredPosition = Vector2.zero;
        var title = Label("voteTitle", voteStage, "パチンコ モード 選択", 92f, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.Center, 0.2f);
        Place(title.rectTransform, 360, 50, 1200, 130);
        var pick1 = Label("voteAsk", voteStage, "선택하세요", 84f, Color.white, TextAlignmentOptions.Center, 0.2f);
        Place(pick1.rectTransform, 460, 170, 1000, 110);
        var sub = Label("voteSub", voteStage, "카드를 클릭해 투표 · 표가 많을수록 뽑힐 확률↑", 38f, new Color(0.85f, 0.8f, 0.95f), TextAlignmentOptions.Center, 0.1f);
        Place(sub.rectTransform, 260, 280, 1400, 60);
        voteTimer = Label("voteTimer", voteStage, "10", 96f, Color.white, TextAlignmentOptions.MidlineRight, 0.2f);
        Place(voteTimer.rectTransform, 1560, 40, 300, 130);
        string[] names = { "東京喰種", "Re:ゼロ" }, arts = { "喰種", "Re:0" }, descs = { "불꽃·수정 숫자 · 유리가 깨진다", "보석 숫자 · 사망 귀환" };
        Color[] cols = { new Color(0.45f, 0.04f, 0.1f, 0.95f), new Color(0.16f, 0.1f, 0.42f, 0.95f) };
        Color[] fills = { new Color(1f, 0.45f, 0.1f, 0.55f), new Color(0.3f, 0.9f, 0.85f, 0.55f) };
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? 130f : 1010f;
            RectTransform card = Rt("card" + i, voteStage); Place(card, x, 360, 780, 450);
            voteCardImg[i] = card.gameObject.AddComponent<Image>(); voteCardImg[i].color = cols[i]; voteCardImg[i].raycastTarget = true;
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1f, 1f, 1f, 0.25f); outline.effectDistance = new Vector2(4f, -4f);
            voteCards[i] = card.gameObject.AddComponent<VoteCard>();
            RectTransform fill = Rt("fill", card); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero; fill.sizeDelta = new Vector2(0f, 0f);
            var fi = fill.gameObject.AddComponent<Image>(); fi.color = fills[i]; fi.raycastTarget = false;
            voteFill[i] = fill;
            var art = Label("art", card, arts[i], 260f, new Color(1f, 1f, 1f, 0.16f), TextAlignmentOptions.Center);
            Stretch(art.rectTransform);
            var nm = Label("name", card, names[i], 84f, Color.white, TextAlignmentOptions.BottomLeft, 0.2f);
            nm.rectTransform.anchorMin = new Vector2(0f, 0f); nm.rectTransform.anchorMax = new Vector2(1f, 0f); nm.rectTransform.pivot = new Vector2(0.5f, 0f);
            nm.rectTransform.anchoredPosition = new Vector2(0f, 70f); nm.rectTransform.sizeDelta = new Vector2(-60f, 120f);
            var ds = Label("desc", card, descs[i], 34f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.BottomLeft);
            ds.rectTransform.anchorMin = new Vector2(0f, 0f); ds.rectTransform.anchorMax = new Vector2(1f, 0f); ds.rectTransform.pivot = new Vector2(0.5f, 0f);
            ds.rectTransform.anchoredPosition = new Vector2(0f, 20f); ds.rectTransform.sizeDelta = new Vector2(-60f, 50f);
            var hint = Label("hint", card, "클릭해서 투표", 34f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.TopLeft, 0.15f);
            hint.rectTransform.anchorMin = new Vector2(0f, 1f); hint.rectTransform.anchorMax = new Vector2(1f, 1f); hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -20f); hint.rectTransform.sizeDelta = new Vector2(-60f, 50f);
            voteBadges[i] = Label("badges", card, "", 40f, new Color(1f, 0.95f, 0.5f), TextAlignmentOptions.TopRight, 0.2f);
            voteBadges[i].rectTransform.anchorMin = new Vector2(0f, 1f); voteBadges[i].rectTransform.anchorMax = new Vector2(1f, 1f); voteBadges[i].rectTransform.pivot = new Vector2(0.5f, 1f);
            voteBadges[i].rectTransform.anchoredPosition = new Vector2(0f, -20f); voteBadges[i].rectTransform.sizeDelta = new Vector2(-60f, 60f);
        }
        // 비율 막대
        voteBarTgImg = Img("barTg", voteStage, null, new Color(0.95f, 0.3f, 0.12f)); Place(voteBarTgImg.rectTransform, 130, 860, 830, 70);
        voteBarRzImg = Img("barRz", voteStage, null, new Color(0.35f, 0.3f, 0.95f)); Place(voteBarRzImg.rectTransform, 960, 860, 830, 70);
        voteBarTg = Label("barTgT", voteStage, "도쿄구울 50%", 40f, Color.white, TextAlignmentOptions.Center, 0.15f); Place(voteBarTg.rectTransform, 130, 860, 830, 70);
        voteBarRz = Label("barRzT", voteStage, "리제로 50%", 40f, Color.white, TextAlignmentOptions.Center, 0.15f); Place(voteBarRz.rectTransform, 960, 860, 830, 70);
        // 결과
        votePickBg = Img("pickBg", voteStage, null, new Color(0f, 0f, 0f, 0f)); Place(votePickBg.rectTransform, -200, -200, 2320, 1480);
        votePick = Label("pick", voteStage, "", 130f, Color.white, TextAlignmentOptions.Center, 0.25f); Place(votePick.rectTransform, 160, 380, 1600, 200);
        votePickSub = Label("pickSub", voteStage, "", 44f, new Color(0.85f, 0.8f, 0.95f), TextAlignmentOptions.Center, 0.12f); Place(votePickSub.rectTransform, 260, 600, 1400, 70);
        votePick.gameObject.SetActive(false); votePickSub.gameObject.SetActive(false);
        voteRoot.gameObject.SetActive(false);
    }

    void OpenVoteUi(float seconds, bool standaloneVote)
    {
        voteStandalone = standaloneVote; voteOpen = true;
        voteRoot.gameObject.SetActive(true);
        System.Array.Clear(voteBy, 0, voteBy.Length); voteMine = 0;
        voteLocalSlot = standaloneVote ? 0 : GameAuthority.LocalPlayerId;
        voteEndTime = Now + seconds;
        votePick.gameObject.SetActive(false); votePickSub.gameObject.SetActive(false); votePickBg.color = new Color(0f, 0f, 0f, 0f);
        foreach (VoteCard c in voteCards) { c.holding = false; c.progress = 0f; }
        RefreshVoteBars();
        if (standaloneVote) StartCoroutine(VoteBots(seconds));
    }

    IEnumerator VoteBots(float seconds)
    {
        int[] choices = { UnityEngine.Random.Range(1, 3), UnityEngine.Random.Range(1, 3), UnityEngine.Random.Range(1, 3) };
        for (int b = 0; b < 3; b++)
        {
            yield return Wait(seconds * UnityEngine.Random.Range(0.12f, 0.28f));
            if (!voteOpen) yield break;
            voteBy[b + 1] = choices[b];
            RefreshVoteBars();
            if (b == 0) voteCards[UnityEngine.Random.Range(0, 2)].clicked = true;   // 시험: 내 클릭도 한 번 보여 준다
        }
    }

    void SetTally(int[] by, int localSlot)
    {
        if (by == null) return;
        for (int i = 0; i < voteBy.Length && i < by.Length; i++) voteBy[i] = by[i];
        voteLocalSlot = localSlot; voteMine = voteBy[Mathf.Clamp(localSlot, 0, voteBy.Length - 1)];
        RefreshVoteBars();
    }

    void CastLocal(int theme)   // 0 東京 · 1 Re:ゼロ
    {
        voteMine = theme + 1; voteBy[Mathf.Clamp(voteLocalSlot, 0, voteBy.Length - 1)] = voteMine;
        RefreshVoteBars();
        if (!voteStandalone) PachinkoController.CastVote(theme);
        Play(Slot.Hold);
    }

    void RefreshVoteBars()
    {
        int tg = 0, rz = 0; var b0 = new StringBuilder(); var b1 = new StringBuilder();
        for (int i = 0; i < voteBy.Length; i++)
        {
            if (voteBy[i] == 1) { tg++; b0.Append("P").Append(i + 1).Append(' '); }
            else if (voteBy[i] == 2) { rz++; b1.Append("P").Append(i + 1).Append(' '); }
        }
        voteBadges[0].text = b0.ToString(); voteBadges[1].text = b1.ToString();
        float pt = tg + rz > 0 ? (float)tg / (tg + rz) : 0.5f;
        float total = 1660f, wT = Mathf.Max(60f, total * pt), wR = Mathf.Max(60f, total - wT);
        voteBarTgImg.rectTransform.anchoredPosition = new Vector2(130f + wT * 0.5f, -(860f + 35f)); voteBarTgImg.rectTransform.sizeDelta = new Vector2(wT, 70f);
        voteBarRzImg.rectTransform.anchoredPosition = new Vector2(130f + wT + wR * 0.5f, -(860f + 35f)); voteBarRzImg.rectTransform.sizeDelta = new Vector2(wR, 70f);
        voteBarTg.rectTransform.anchoredPosition = voteBarTgImg.rectTransform.anchoredPosition; voteBarTg.rectTransform.sizeDelta = new Vector2(wT, 70f);
        voteBarRz.rectTransform.anchoredPosition = voteBarRzImg.rectTransform.anchoredPosition; voteBarRz.rectTransform.sizeDelta = new Vector2(wR, 70f);
        voteBarTg.text = wT > 240f ? $"도쿄구울 {Mathf.RoundToInt(pt * 100f)}%" : ""; voteBarRz.text = wR > 240f ? $"리제로 {Mathf.RoundToInt((1f - pt) * 100f)}%" : "";
        Color chosen = new Color(1f, 0.85f, 0.25f, 1f);
        voteCardImg[0].GetComponent<Outline>().effectColor = voteMine == 1 ? chosen : new Color(1f, 1f, 1f, 0.25f);
        voteCardImg[1].GetComponent<Outline>().effectColor = voteMine == 2 ? chosen : new Color(1f, 1f, 1f, 0.25f);
        voteCardImg[0].GetComponent<Outline>().effectDistance = voteMine == 1 ? new Vector2(10f, -10f) : new Vector2(4f, -4f);
        voteCardImg[1].GetComponent<Outline>().effectDistance = voteMine == 2 ? new Vector2(10f, -10f) : new Vector2(4f, -4f);
    }

    void VoteUpdate()
    {
        if (voteRoot == null || !voteRoot.gameObject.activeSelf) return;
        Rect r = ((RectTransform)voteRoot).rect;
        if (r.width > 1f && r.height > 1f) { float sc = Mathf.Min(r.width / 1920f, r.height / 1080f); voteStage.localScale = new Vector3(sc, sc, 1f); }
        if (!voteOpen) return;
        float left = Mathf.Max(0f, voteEndTime - Now);
        voteTimer.text = Mathf.CeilToInt(left).ToString();
        voteTimer.color = left <= 3f ? new Color(1f, 0.3f, 0.25f) : Color.white;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        for (int i = 0; i < 2; i++)
        {
            VoteCard c = voteCards[i];
            bool keyed = kb != null && (i == 0 ? kb.digit1Key.wasPressedThisFrame : kb.digit2Key.wasPressedThisFrame);
            if (c.clicked || keyed) { c.clicked = false; c.pressT = 0f; CastLocal(i); }
            c.pressT = Mathf.Min(1f, c.pressT + Dt / 0.22f);
            // 눌리듯 들어갔다 나오는 짧은 반응(0.93까지 눌렸다가 선택 카드는 1.03으로 튀어나온다)
            float dip = c.pressT < 0.4f ? Mathf.Lerp(1f, 0.93f, c.pressT / 0.4f) : Mathf.Lerp(0.93f, voteMine == i + 1 ? 1.03f : 1f, (c.pressT - 0.4f) / 0.6f);
            voteFill[i].anchorMax = new Vector2(0f, 1f);
            voteCards[i].transform.localScale = Vector3.one * (c.pressT >= 1f ? (c.holding ? 0.97f : (voteMine == i + 1 ? 1.03f : 1f)) : dip);
        }
        if (voteStandalone && left <= 0f)
        {
            voteOpen = false;
            int tg = 0, rz = 0; for (int i = 0; i < voteBy.Length; i++) { if (voteBy[i] == 1) tg++; else if (voteBy[i] == 2) rz++; }
            float pt = tg + rz > 0 ? (float)tg / (tg + rz) : 0.5f;
            CloseVote(UnityEngine.Random.value < pt ? 0 : 1);
        }
    }

    IEnumerator VoteResult(int theme)
    {
        voteOpen = false;
        int tg = 0, rz = 0; for (int i = 0; i < voteBy.Length; i++) { if (voteBy[i] == 1) tg++; else if (voteBy[i] == 2) rz++; }
        float pt = tg + rz > 0 ? (float)tg / (tg + rz) : 0.5f;
        votePick.text = theme == 0 ? "도쿄구울 모드 선택!" : "리제로 모드 선택!";
        votePick.color = theme == 0 ? new Color(1f, 0.55f, 0.2f) : new Color(0.5f, 0.92f, 1f);
        votePickSub.text = $"도쿄구울 {Mathf.RoundToInt(pt * 100f)}% · 리제로 {Mathf.RoundToInt((1f - pt) * 100f)}% 중에서 뽑혔습니다";
        votePick.gameObject.SetActive(true); votePickSub.gameObject.SetActive(true);
        float t = 0f;
        while (t < 1.2f)
        {
            t += Dt; votePickBg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / 0.3f) * 0.85f);
            float pop = 1f + 0.25f * Mathf.Exp(-t * 8f) * Mathf.Cos(t * 18f);
            votePick.rectTransform.localScale = Vector3.one * pop;
            yield return null;
        }
        voteRoot.gameObject.SetActive(false);
    }

    Image Logo(string spriteName, float cx, float cy, float width)
    {
        Sprite s = Spr("Logos/" + spriteName);
        Image img = Img(spriteName, stage, s, Color.white);
        float aspect = s != null ? s.rect.height / s.rect.width : 0.4f;
        Place(img.rectTransform, cx - width * 0.5f, cy - width * aspect * 0.5f, width, width * aspect);
        img.gameObject.SetActive(false);
        return img;
    }

    static Sprite vignetteSprite;
    static Sprite MakeVignette()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.7f + dy * dy);
                float a = Mathf.SmoothStep(0.45f, 1.05f, d) * 0.92f;
                px[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return vignetteSprite;
    }

    /// <summary>유리 조각 한 장: 삼각형 하나를 그림의 UV 그대로 그린다.</summary>
    class ShardGraphic : MaskableGraphic
    {
        public Texture tex; public Vector2[] pts = new Vector2[3]; public Vector2[] uvs = new Vector2[3];
        public override Texture mainTexture => tex != null ? tex : s_WhiteTexture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < 3; i++) vh.AddVert(new Vector3(pts[i].x, pts[i].y, 0f), color, uvs[i]);
            vh.AddTriangle(0, 1, 2);
        }
    }

    static Sprite MakeCircleStatic() => null;

    static Sprite MakeCircle()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - d) * 12f);
                float shade = 1f - 0.35f * Mathf.Clamp01(d) + 0.3f * Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.3f) * (dx + 0.3f) + (dy - 0.3f) * (dy - 0.3f)) * 2f);
                byte v = (byte)(Mathf.Clamp01(shade) * 255f);
                px[y * n + x] = new Color32(v, v, v, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ───────── 릴(숫자 한 줄: 창·바탕 없이 큰 숫자가 세로로 흘러내린다) ─────────
    const float NumberSize = 640f, NumberSpacing = 1100f, ColumnWidth = 900f;   // 가로는 넉넉히(기울기·불꽃이 옆 칸으로 나가도 안 잘림), 세로만 자른다
    static readonly float[] BigX = { 420f, 960f, 1500f };
    const float BigY = 600f, SmallY = 320f, SmallScale = 0.34f;   // 미니 릴: 연출 중에도 위쪽 가운데에 작은 3칸이 항상 보인다(상단 띠 아래)
    static readonly float[] SmallX = { 730f, 960f, 1190f };
    class Reel
    {
        public float scroll;       // 셀 단위 위치. 정수 = 그 번호가 정가운데
        public bool spinning;
        public Vector2 punch = Vector2.one;   // 정지 타격(스쿼시) 배율
        public float emphasis = 1f;           // 리치 강조 배율
        public float liftT;                   // 가운데 줄: 착지 전까지 조금 작고 위(1) → 제자리(0)
        public float shakeX;
        public bool pink;                     // 리치 변형(_pink)
        public CanvasGroup group;
        public Image hit;                     // 정지 순간 그 숫자만 흰 섬광
        public RectTransform Window => window;
        readonly Image[] cells = new Image[3];
        readonly Image[] overlays = new Image[3];
        readonly int index;
        readonly RectTransform window;
        public Reel(PachinkoOverlay owner, Transform parent, int i)
        {
            index = i;
            window = owner.Rt("reel" + i, parent);
            Place(window, BigX[i] - ColumnWidth * 0.5f, BigY - 540f, ColumnWidth, 1080f);
            window.gameObject.AddComponent<RectMask2D>();
            for (int k = 0; k < 3; k++)
            {
                cells[k] = owner.Img("cell" + k, window, null, Color.white);
                cells[k].rectTransform.sizeDelta = new Vector2(NumberSize, NumberSize);
                cells[k].rectTransform.anchorMin = cells[k].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                overlays[k] = owner.Img("spark" + k, cells[k].transform, null, Color.white);
                overlays[k].rectTransform.anchorMin = Vector2.zero; overlays[k].rectTransform.anchorMax = Vector2.one;
                overlays[k].rectTransform.offsetMin = overlays[k].rectTransform.offsetMax = Vector2.zero;
            }
            hit = owner.Img("hit", window, null, new Color(1f, 1f, 1f, 0f));
            hit.rectTransform.anchorMin = hit.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hit.rectTransform.sizeDelta = new Vector2(NumberSize, NumberSize);
            group = window.gameObject.AddComponent<CanvasGroup>();
            Refresh();
        }
        public Sprite CenterSprite => cells[1].sprite;
        static Sprite SymSpr(string file) => Spr(activeSymbolDir + "/" + file) ?? Spr("Symbols/" + file);   // 테마별 심볼 세트(없으면 도쿄구울 v3로)
        public static int SymbolAt(int index) => ((index % 9) + 9) % 9 + 1;
        public void Refresh()
        {
            int baseIndex = Mathf.FloorToInt(scroll);
            float frac = scroll - baseIndex;
            for (int k = 0; k < 3; k++)
            {
                int idx = baseIndex + (k - 1);   // k=1이 가운데 칸
                int sym = SymbolAt(idx);
                int fi = (int)(Now * 12f) % 6;
                bool fire = sym % 2 == 1;   // 홀수(7 금색 포함) = 불꽃 6프레임 / 짝수 = 크리스털(+별빛 2프레임)
                if (spinning) { cells[k].sprite = SymSpr($"symbol_{sym}_blur"); overlays[k].enabled = false; }
                else if (pink && SymSpr($"symbol_{sym}_pink") != null) { cells[k].sprite = SymSpr($"symbol_{sym}_pink"); overlays[k].enabled = false; }
                else if (fire) { cells[k].sprite = SymSpr($"symbol_{sym}_f{fi}") ?? SymSpr($"symbol_{sym}"); overlays[k].enabled = false; }
                else { cells[k].sprite = SymSpr($"symbol_{sym}"); overlays[k].sprite = SymSpr($"symbol_{sym}_s{(fi / 3) % 2}"); overlays[k].enabled = overlays[k].sprite != null; }
                cells[k].rectTransform.anchoredPosition = new Vector2(0f, (frac - (k - 1)) * NumberSpacing);
            }
        }
        /// <summary>t = 0(큰 숫자 가운데) → 1(작게 오른쪽 위로 — 연출이 화면을 쓸 때).</summary>
        public void SetSmall(float t)
        {
            float x = Mathf.Lerp(BigX[index], SmallX[index], t), y = Mathf.Lerp(BigY, SmallY, t);
            float lift = liftT * (1f - t);
            window.anchoredPosition = new Vector2(x + shakeX, -y + 45f * lift);
            float sc = Mathf.Lerp(1f, SmallScale, t) * emphasis * (1f - 0.1f * lift);
            window.localScale = new Vector3(sc * punch.x, sc * punch.y, 1f);
        }
        public void Highlight(Color c) { foreach (Image cell in cells) cell.color = c; }
        public IEnumerator Stop(int symbol, float seconds, float speed)
        {
            float s0 = scroll;
            float travel = Mathf.Max(5f, speed * seconds / 3f);
            int target = Mathf.CeilToInt(s0 + travel);
            while (SymbolAt(target) != symbol) target++;
            float t = 0f;
            while (t < seconds)
            {
                t += Dt;
                float u = Mathf.Clamp01(t / seconds);
                float e = 1f - Mathf.Pow(1f - u, 3f);
                scroll = Mathf.Lerp(s0, target, e);
                spinning = u < 0.75f;
                Refresh();
                yield return null;
            }
            scroll = target; spinning = false; Refresh();
        }
    }
    float numbersSmallT, numbersSmallTarget, duckTarget = 1f;
    bool heartbeat;

    // ───────── 연출 ─────────
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f), RedC = new Color(1f, 0.18f, 0.29f), BlueC = new Color(0.16f, 0.65f, 1f), GreenC = new Color(0.23f, 1f, 0.42f), WhiteC = new Color(0.62f, 0.66f, 0.74f);

    void Begin(PachinkoPlan plan, bool isStandalone)
    {
        if (running != null) StopCoroutine(running);
        standalone = isStandalone;
        pushedWho = int.MinValue;
        win = plan.win;
        curTheme = plan.theme; Theme = plan.theme;
        activeSymbolDir = plan.theme == 1 ? "SymbolsRz" : "Symbols";
        root.gameObject.SetActive(true);
        ResetVisuals();
        duckTarget = BgmDuck;
        running = StartCoroutine(plan.theme == 1 ? SequenceRz(plan) : Sequence(plan));
    }

    void ResetVisuals()
    {
        bgTarget = new Color(0.04f, 0.015f, 0.07f, 0.97f); bgTint = bgTarget; backdrop.color = bgTint;
        foreach (Image l in new[] { logoReach, logoKeizoku, logoGekiatsu, logoFukkatsu, logoOoatari, logoAtari, logoZannen }) l.gameObject.SetActive(false);
        yokokuSmall.gameObject.SetActive(false); chanceText.gameObject.SetActive(false);
        spSlotPanel.gameObject.SetActive(false); cutRect.gameObject.SetActive(false); pushGroup.gameObject.SetActive(false); bandRect.gameObject.SetActive(false);
        raysRect.gameObject.SetActive(false);
        dim.color = new Color(0f, 0f, 0f, 0f); flash.color = new Color(1f, 1f, 1f, 0f);
        foreach (Image o in holdOrbs) { o.color = WhiteC; o.rectTransform.localScale = Vector3.one; }
        foreach (Reel r in reels) { r.spinning = false; r.Highlight(Color.white); r.pink = false; r.punch = Vector2.one; r.emphasis = 1f; r.liftT = 0f; r.shakeX = 0f; r.group.alpha = 1f; r.hit.color = new Color(1f, 1f, 1f, 0f); r.Refresh(); }
        winFlashActive = false;
        if (sharpRays != null) sharpRays.gameObject.SetActive(false);
        if (lbTop != null) { lbTop.gameObject.SetActive(false); lbBottom.gameObject.SetActive(false); }
        if (returnFade != null) returnFade.gameObject.SetActive(false);
        numbersSmallT = numbersSmallTarget = 0f;
        foreach (Image p in particlePool) p.gameObject.SetActive(false);
        kanekiRect.gameObject.SetActive(true); kanekiImage.sprite = Spr("Scene/helpme"); kanekiImage.enabled = true; kanekiImage.color = Color.white;
        Place(kanekiPic, 960 - 440, 0, 880, 1080); kanekiPic.localScale = Vector3.one; kanekiPic.localRotation = Quaternion.identity;
        foreach (Transform sh in shardRoot) Destroy(sh.gameObject);
        missImage.enabled = false; eyeGlow.color = new Color(1f, 0.1f, 0.1f, 0f);
        heartRed.color = new Color(0.8f, 0f, 0.05f, 0f); burstGlow.color = new Color(1f, 0.45f, 0.1f, 0f); heartbeat = true;
        StopVideoNow();
        if (musicSource != null) { musicSource.Stop(); musicOn = false; musicFade = 1f; }
        if (videoPlayer2 != null) videoPlayer2.Stop();
        speedRect.gameObject.SetActive(false);
        machineRect.localScale = Vector3.one * 0.5f; machineRect.localRotation = Quaternion.Euler(0, 0, -6f);
        meterText.text = "기대도 –";
        bgPulse = 0f;
        pushFlashing = false;
        stage.localPosition = Vector3.zero;
    }

    static string[] HoldNames = { "white", "blue", "green", "red", "gold" };
    int expect;   // 기대도(표시용 대략값)
    void SetExpect(int v) { expect = v; meterText.text = $"기대도 {v}%"; }

    IEnumerator Sequence(PachinkoPlan p)
    {
        float t0 = Now;
        Play(Slot.Open);
        // 등장
        float u = 0f;
        while (u < 0.5f)
        {
            u += Dt;
            float e = Mathf.Clamp01(u / 0.5f); e = 1f - Mathf.Pow(1f - e, 3f);
            machineRect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, e);
            machineRect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-6f, 0f, e));
            yield return null;
        }
        machineRect.localScale = Vector3.one; machineRect.localRotation = Quaternion.identity;

        // 1) 보류 색 — 0.9초
        yield return Wait(0.5f);
        Color holdColor = HoldColor(p.hold);
        holdOrbs[0].color = holdColor;
        if (p.hold != PachinkoHold.White) Play(Slot.Hold);
        int[] holdPct = { 2, 8, 20, 80, 100 };
        SetExpect(holdPct[(int)p.hold]);
        yield return Wait(0.4f);

        // 2) 세 줄 돌고 왼쪽 → 오른쪽 정지 — 2.4초
        const float speed = 9f;
        curReach = p.reach; reelsCenterHeld = true;
        { int off = UnityEngine.Random.Range(0, 9); for (int ri = 0; ri < 3; ri++) reels[ri].scroll = off + ri * 3; }   // 시작 숫자도 칸마다 다르게
        foreach (Reel r in reels) r.spinning = true;
        reels[1].liftT = 1f;   // 가운데 줄은 착지 전까지 조금 작고 위
        Play(Slot.ReelSpin);
        videoFileName = BgVideoFile; videoNatural = false;
        StartVideo();   // 룰렛이 돌기 시작하면 배경 영상 15초
        StartMusic();   // 음악은 결과 연출이 끝날 때까지
        StartCoroutine(SpinLoop(speed));
        yield return Wait(0.03f);
        Coroutine leftStop = StartCoroutine(StopReel(0, p.left, 1.57f, spinSpeeds[0]));
        Coroutine rightStop = StartCoroutine(StopReel(2, p.right, 2.37f, spinSpeeds[2]));
        yield return Wait(1.6f); Play(Slot.ReelStop);
        yield return Wait(0.8f); Play(Slot.ReelStop);

        if (!p.reach) yield return Wait(0.4f);
        else
        {
            foreach (Image o in holdOrbs) o.rectTransform.localScale = Vector3.one;
            reels[1].Highlight(new Color(1f, 0.78f, 0.8f));
            // 3) リーチ — 1.3초
            Play(Slot.Reach);
            Tentacles(2);   // 카구네 촉수
            StartCoroutine(ReachEmphasis()); SharpRays(true);
            yield return Pop(logoReach, 1.3f, 3f);
            SetExpect(Mathf.Max(expect, 10));
            if (p.yokoku > 0 || p.bg != PachinkoBg.None || p.cut != PachinkoCut.None) numbersSmallTarget = 1f;   // 연출이 화면을 쓰는 동안 숫자는 작게 오른쪽 위로
            // 4) 継続 ×N — 각 1.05초
            for (int k = 1; k <= p.yokoku; k++)
            {
                yokokuSmall.text = $"連続予告 ×{k + 1}"; yokokuSmall.gameObject.SetActive(true);
                Play(Slot.Yokoku);
                SetExpect(Mathf.Min(95, expect + 12));
                yield return Pop(logoKeizoku, 0.9f, 2.4f);
                yokokuSmall.gameObject.SetActive(false);
                yield return Wait(0.15f);
            }
            // 5) SP 리치 배경 단계 — 단계×0.75초 + (초록 아니면) 슬롯 1.8초
            if (p.bg != PachinkoBg.None)
            {
                Color[] bgs = { new Color(0.01f, 0.25f, 0.14f, 0.92f), new Color(0.42f, 0.02f, 0.08f, 0.92f), new Color(0.45f, 0.3f, 0f, 0.92f) };
                string[] chance = { "CHANCE!", "激熱 CHANCE!!", "金 確定級!!!" };
                Color[] chanceColor = { GreenC, RedC, Gold };
                int[] pct = { 25, 56, 85 };
                int top = (int)p.bg - 1;
                for (int k = 0; k <= top; k++)
                {
                    bgTarget = bgs[k]; bgPulse = k;
                    Impact(k == 0 ? new Color(0.5f, 1f, 0.7f, 0.5f) : new Color(1f, 0.25f, 0.25f, 0.7f), 0.4f, k >= 1);
                    if (k == 1) StartCoroutine(Embers());
                    chanceText.text = chance[k]; chanceText.color = chanceColor[k]; chanceText.gameObject.SetActive(true);
                    if (k == 1) { Play(Slot.SpStage); StartCoroutine(Letterbox("SP リーチ", "SPECIAL REACH", true)); }
                    SetExpect(Mathf.Max(expect, pct[k]));
                    yield return Wait(0.75f);
                }
                chanceText.gameObject.SetActive(false);
                if (p.bg != PachinkoBg.Green)
                {
                    yield return SpReach(p.bg);
                }
                if (lbTop != null && lbTop.gameObject.activeSelf) StartCoroutine(Letterbox("", "", false));
            }
            // 6) 최종 컷인 — 1.6초
            if (p.cut != PachinkoCut.None)
            {
                yield return Cutin(p.cut);
            }
        }

        // PUSH 창이 열리는 시각까지 맞춘다
        float target = t0 + p.PreludeSeconds();
        while (Now < target) yield return null;

        // 7) PUSH — PushSeconds 동안
        SharpRays(false);
        yield return PushWindow();
        if (pushedWho == int.MinValue)
        {
            // 컨트롤러 알림을 기다린다(최대 4초). 시험용이면 바로 간다.
            float wait = standalone ? 0f : 4f;
            while (pushedWho == int.MinValue && wait > 0f) { wait -= Dt; yield return null; }
            if (pushedWho == int.MinValue) pushedWho = -1;
        }
        pushGroup.gameObject.SetActive(false); dim.color = new Color(0, 0, 0, 0);
        float resultStart = Now;
        videoStopRequested = true;   // 15초 전에 결과가 나면 결과 연출과 함께 페이드

        // 8) 가운데 줄 정지 — 숫자가 다시 큼직하게
        Stop(Slot.PushOpen); Stop(Slot.ReelSpin);
        numbersSmallTarget = 0f;
        reels[1].Highlight(Color.white);
        if (p.win && !p.revive) yield return WarnStrobe();   // 大当たり 직전 경고 스트로브
        int shown = p.revive ? (p.left % 9) + 1 : p.center;
        float stopDur = p.reach ? 0.9f : 0.6f;
        StopAllSpin();
        yield return StopReel(1, shown, stopDur, spinSpeeds[1] * 0.5f);
        Play(Slot.ReelStop);
        if (p.revive)
        {
            yield return ShatterScreen(true, 1.3f);    // 꽝처럼 금이 가다가 멈추고, 역재생처럼 도로 붙는다
            yield return Wait(0.2f);
            bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f);
            Play(Slot.Revive);
            StartCoroutine(WinPicture());
            yield return Pop(logoFukkatsu, 0.9f, 3f);
            reels[1].spinning = true;
            yield return StopReel(1, p.center, 0.55f, 14f);
        }
        yield return Wait(0.3f);

        // 9) 결과
        if (p.win)
        {
            if (!p.revive) StartCoroutine(WinPicture());
            StartCoroutine(WinFlashStandard()); StartCoroutine(ImpactCut(curTheme == 1 ? new Color(0.7f, 0.5f, 1f) : new Color(0.9f, 0.05f, 0.1f))); shakeUntil = Now + 0.4f;
            StartCoroutine(WinGlints(p.ResultSeconds()));
            StartCoroutine(LineFlare(470f, curTheme == 1 ? new Color(0.8f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.7f, 0.9f)));
            StartCoroutine(curTheme == 1 ? RzWinFx() : KagureWinFx());
            LoadDefaultSfx(); PlayLine(WinLines[Mathf.Clamp(p.center, 1, 9) - 1]);   // 숫자별 대사(111~999) — 당첨 확정 순간
            SetExpect(100);
            if (p.jackpot)
            {
                bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f); bgPulse = 3f;
                Play(Slot.Ooatari);
                StartCoroutine(Confetti(Mathf.Max(5.5f, p.ResultSeconds() - 0.4f)));
                StartCoroutine(Fireworks(true, 0));
                StartCoroutine(Pop(logoOoatari, p.ResultSeconds() + 0.5f, 1.2f, keep: true));
                StartCoroutine(Later(3.0f, Fireworks(true, curTheme)));
                ShowBand("大当たり 보상: 랜덤 유닛 ×1");
            }
            else
            {
                StartCoroutine(Fireworks(false, 0));
                bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f);
                StartCoroutine(PlayFor(Slot.Atari, 6f));
                StartCoroutine(Pop(logoAtari, p.ResultSeconds() + 0.5f, 1.2f, keep: true));
                ShowBand("当たり 보상: 전설·히든 위습 ×1");
            }
        }
        else
        {
            LoadDefaultSfx(); PlayLine(MissLine);   // 「運なんて存在しない」(리제) — 그림이 깨지는 순간
            StartCoroutine(ShatterScreen(false, 0.8f));   // 꽝: 지금 화면 전체가 유리창처럼 깨진다
            StartCoroutine(Pop(logoZannen, 4f, 3f, keep: true));
            ShowBand(p.reach ? "아쉬움 보상: 희귀함 위습 ×1" : "아쉬움 보상: 특별함 위습 ×1");
        }

        // 컨트롤러가 정한 결과 시간만큼 보여 주고 닫는다
        float until = resultStart + p.ResultSeconds();
        while (Now < until) yield return null;
        if (musicOn) StartCoroutine(MusicFadeOut(1f));   // 보상 띠가 끝나는 순간 1초 페이드아웃 → 게임 BGM 복귀
        yield return ReturnFade();
        while (VoiceActive() || musicOn) yield return null;   // 대사·음악 페이드는 끝까지
        StopAllSfx();
        StopVideoNow();
        duckTarget = 1f; GameBgm.Duck = 1f;
        root.gameObject.SetActive(false);
        StartCoroutine(ReturnFadeOut());
        running = null;
    }

    // ───────── 화려한 연출 도구(사장님 「파칭코 영상처럼 화려하게」) ─────────
    /// <summary>화면 전환 섬광(흰/붉은/금) + 방사 속도선 + 화면 흔들림.</summary>
    void Impact(Color tint, float shake = 0.35f, bool lines = true)
    {
        StartCoroutine(FlashColor(tint, 0.06f));
        shakeUntil = Mathf.Max(shakeUntil, Now + Mathf.Min(shake, 0.3f));
        if (lines) StartCoroutine(SpeedBurst(0.2f));
    }

    IEnumerator FlashColor(Color c, float seconds)
    {
        // 흰 섬광은 2~3프레임만(0.06초 이하, α 0.5 이하) — 길게 남기지 않는다
        if (winFlashActive) yield break;
        float sec = Mathf.Min(seconds, 0.06f);
        float t = 0f; float amax = Mathf.Min(c.a, 0.5f);
        while (t < sec) { t += Dt; if (winFlashActive) yield break; flash.color = new Color(c.r, c.g, c.b, amax * (1f - t / sec)); yield return null; }
        if (!winFlashActive) flash.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>임팩트 컷(애니 연출): 1~2프레임 흰색 → 1~2프레임 고대비 흑 + 한 색 사선만 남기고 바로 원래대로.</summary>
    IEnumerator ImpactCut(Color accent)
    {
        if (!winFlashActive) flash.color = new Color(1f, 1f, 1f, 0.9f);
        yield return null;
        if (!winFlashActive) flash.color = new Color(0f, 0f, 0f, 0.9f);
        var slashes = new List<RectTransform>();
        for (int i = 0; i < 4; i++)
        {
            RectTransform r = Rt("slash", shatterRoot);
            var im = r.gameObject.AddComponent<Image>(); im.color = accent; im.raycastTarget = false;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(UnityEngine.Random.Range(5f, 14f), 3000f);
            r.anchoredPosition = new Vector2(UnityEngine.Random.Range(-700f, 700f), UnityEngine.Random.Range(-300f, 300f));
            r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(25f, 65f) * (i % 2 == 0 ? 1f : -1f));
            slashes.Add(r);
        }
        yield return null;
        foreach (RectTransform r in slashes) if (r != null) Destroy(r.gameObject);
        if (!winFlashActive) flash.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>아나모픽 줄 플레어: 가로로 가늘고 긴 빛줄기(0.15초, 원형 글로우 금지).</summary>
    IEnumerator LineFlare(float y, Color c, int count = 2)
    {
        var rts = new List<Image>();
        for (int i = 0; i < count; i++)
        {
            Sprite streak = FxSpr(c.b > c.r ? "streak_teal" : "streak_white");
            Image im = Img("flare", fireworkRoot, streak, c);
            im.preserveAspect = false;
            im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0f, 1f);
            im.rectTransform.sizeDelta = streak != null ? new Vector2(2300f, UnityEngine.Random.Range(40f, 70f)) : new Vector2(1920f, UnityEngine.Random.Range(3f, 6f));
            im.rectTransform.anchoredPosition = new Vector2(960f, -(y + i * UnityEngine.Random.Range(-26f, 26f)));
            rts.Add(im);
        }
        float t = 0f;
        while (t < 0.15f)
        {
            t += Dt; float u = t / 0.15f;
            foreach (Image im in rts) { im.rectTransform.localScale = new Vector3(Mathf.Lerp(0.15f, 1.1f, Mathf.Sqrt(u)), 1f, 1f); Color cc = c; cc.a = c.a * (1f - u); im.color = cc; }
            yield return null;
        }
        foreach (Image im in rts) Destroy(im.gameObject);
    }

    Image speedImage;
    IEnumerator SpeedBurst(float seconds)
    {
        Sprite sl = FxSpr("speedlines");
        if (sl != null)
        {   // blender 집중선 그림(가운데 비움) — 화면 전체, 0.2초
            if (speedImage == null)
            {
                speedImage = Img("speedImg", stage, sl, new Color(1f, 1f, 1f, 0f)); speedImage.preserveAspect = false; Place(speedImage.rectTransform, 0, 0, 1920, 1080);
                speedImage.transform.SetSiblingIndex(speedRect.GetSiblingIndex());
            }
            speedImage.gameObject.SetActive(true);
            float st = 0f;
            while (st < seconds)
            {
                st += Dt; float u = st / seconds;
                speedImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 0.96f, u);
                speedImage.color = new Color(1f, 1f, 1f, (1f - u) * 0.8f);
                yield return null;
            }
            speedImage.gameObject.SetActive(false);
            yield break;
        }
        speedRect.gameObject.SetActive(true);
        Image[] lines = speedRect.GetComponentsInChildren<Image>();
        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float u = t / seconds;
            speedRect.localScale = Vector3.one * Mathf.Lerp(1.08f, 0.94f, u);   // 가장자리에서 중심 쪽으로 쓸려 들어온다
            foreach (Image ln in lines) { Color c = ln.color; c.a = (1f - u) * 0.55f * UnityEngine.Random.Range(0.6f, 1f); ln.color = c; }
            yield return null;
        }
        speedRect.gameObject.SetActive(false);
    }

    /// <summary>색수차: 빨강·청록 복제본이 어긋나 있다가 모이며 사라진다(그림 등장 때).</summary>
    IEnumerator Ghosts(Image logo, float seconds)
    {
        var ghosts = new List<Image>();
        for (int g = 0; g < 2; g++)
        {
            GameObject clone = Instantiate(logo.gameObject, logo.transform.parent);
            clone.name = "ghost"; clone.transform.SetSiblingIndex(logo.transform.GetSiblingIndex());
            Image gi = clone.GetComponent<Image>(); gi.color = g == 0 ? new Color(1f, 0.1f, 0.2f, 0.7f) : new Color(0.1f, 0.9f, 1f, 0.7f);
            gi.raycastTarget = false; ghosts.Add(gi);
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Dt; float u = t / seconds; float off = Mathf.Lerp(34f, 0f, u);
            ghosts[0].rectTransform.anchoredPosition = logo.rectTransform.anchoredPosition + new Vector2(-off, 0f);
            ghosts[1].rectTransform.anchoredPosition = logo.rectTransform.anchoredPosition + new Vector2(off, 0f);
            ghosts[0].rectTransform.localScale = ghosts[1].rectTransform.localScale = logo.rectTransform.localScale * (1f + 0.05f * u);
            foreach (Image gi in ghosts) { Color c = gi.color; c.a = 0.7f * (1f - u); gi.color = c; }
            yield return null;
        }
        foreach (Image gi in ghosts) if (gi != null) Destroy(gi.gameObject);
    }

    /// <summary>불꽃 파티클(빨강·금 배경이 번쩍일 때 아래에서 솟는다).</summary>
    IEnumerator Embers()
    {
        var live = new List<(Image img, float x, float y, float vy, float sway, float phase)>();
        while (running != null && (bgPulse >= 1f || live.Count > 0))
        {
            float dt = Dt;
            if (bgPulse >= 1f)
                for (int n = 0; n < 2; n++)
                {
                    Image img = GetParticle();
                    img.sprite = null; img.color = UnityEngine.Random.value < 0.5f ? new Color(1f, 0.5f, 0.1f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
                    img.rectTransform.sizeDelta = new Vector2(3f, UnityEngine.Random.Range(8f, 18f)); img.rectTransform.localRotation = Quaternion.identity;
                    live.Add((img, UnityEngine.Random.Range(0f, 1920f), 1100f, UnityEngine.Random.Range(300f, 800f), UnityEngine.Random.Range(20f, 80f), UnityEngine.Random.Range(0f, 6f)));
                }
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i]; p.y -= p.vy * dt;
                p.img.rectTransform.anchoredPosition = new Vector2(p.x + Mathf.Sin(Now * 3f + p.phase) * p.sway, -p.y);
                Color c = p.img.color; c.a = Mathf.Clamp01(p.y / 700f); p.img.color = c;
                if (p.y < -40f) { p.img.gameObject.SetActive(false); live.RemoveAt(i); } else live[i] = p;
            }
            yield return null;
        }
        foreach (var p in live) p.img.gameObject.SetActive(false);
    }

    // ───────── 카네키 그림: 산산조각 · 부활 · 당첨 전환 ─────────
    /// <summary>금이 가기 시작한다(방사형 금 + 흔들림). 부활이면 여기서 멈춘다.</summary>
    IEnumerator Cracks(float seconds)
    {
        heartbeat = false;
        Vector2 hit = new Vector2(960f, 470f);
        var lines = new List<RectTransform>();
        for (int i = 0; i < 12; i++)
        {
            RectTransform r = Rt("crack", shardRoot);
            var im = r.gameObject.AddComponent<Image>(); im.color = new Color(1f, 0.9f, 0.45f, 0.95f); im.raycastTarget = false;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = new Vector2(hit.x, -hit.y); r.sizeDelta = new Vector2(UnityEngine.Random.Range(3f, 6f), 0f);
            r.localRotation = Quaternion.Euler(0, 0, i * 30f + UnityEngine.Random.Range(-10f, 10f));
            lines.Add(r);
        }
        Impact(new Color(1f, 1f, 1f, 0.7f), 0.5f, false);
        float t = 0f;
        while (t < seconds)
        {
            t += Dt; float u = Mathf.Clamp01(t / seconds);
            foreach (RectTransform r in lines) r.sizeDelta = new Vector2(r.sizeDelta.x, Mathf.Lerp(0f, 760f, 1f - Mathf.Pow(1f - u, 2f)));
            shakeUntil = Now + 0.05f;
            yield return null;
        }
        foreach (RectTransform r in lines) Destroy(r.gameObject);
    }

    /// <summary>그림을 삼각 조각으로 나눈다(조각마다 UV 유지). reverse면 흩어진 조각이 도로 붙는다.</summary>
    IEnumerator Shatter(float seconds, bool reverse)
    {
        heartbeat = false;
        Sprite spr = kanekiImage.sprite;
        if (spr == null) yield break;
        float aspect = spr.rect.width / spr.rect.height;
        float H = 1080f, W = H * aspect; if (W > 880f) { W = 880f; H = W / aspect; }
        Rect uvRect = new Rect(spr.textureRect.x / spr.texture.width, spr.textureRect.y / spr.texture.height, spr.textureRect.width / spr.texture.width, spr.textureRect.height / spr.texture.height);
        const int cols = 7, rows = 9;
        var grid = new Vector2[cols + 1, rows + 1];
        for (int gy = 0; gy <= rows; gy++)
            for (int gx = 0; gx <= cols; gx++)
            {
                float jx = (gx == 0 || gx == cols) ? 0f : UnityEngine.Random.Range(-0.35f, 0.35f), jy = (gy == 0 || gy == rows) ? 0f : UnityEngine.Random.Range(-0.35f, 0.35f);
                grid[gx, gy] = new Vector2(((gx + jx) / cols - 0.5f) * W, ((gy + jy) / rows - 0.5f) * H);
            }
        var shards = new List<(RectTransform rt, Vector2 basePos, Vector2 vel, float spin, float delay, ShardGraphic g)>();
        Vector2 impact = new Vector2(0f, 40f);
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                Vector2 a = grid[gx, gy], b = grid[gx + 1, gy], c = grid[gx + 1, gy + 1], d = grid[gx, gy + 1];
                Vector2[][] tris = UnityEngine.Random.value < 0.5f ? new[] { new[] { a, b, c }, new[] { a, c, d } } : new[] { new[] { a, b, d }, new[] { b, c, d } };
                foreach (Vector2[] tri in tris)
                {
                    Vector2 cen = (tri[0] + tri[1] + tri[2]) / 3f;
                    RectTransform rt = Rt("shard", shardRoot);
                    var sg = rt.gameObject.AddComponent<ShardGraphic>();
                    sg.tex = spr.texture; sg.raycastTarget = false;
                    for (int i = 0; i < 3; i++)
                    {
                        sg.pts[i] = tri[i] - cen;
                        sg.uvs[i] = new Vector2(uvRect.x + (tri[i].x / W + 0.5f) * uvRect.width, uvRect.y + (tri[i].y / H + 0.5f) * uvRect.height);
                    }
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.sizeDelta = Vector2.zero;
                    Vector2 basePos = new Vector2(960f + cen.x, -540f + cen.y);
                    rt.anchoredPosition = basePos;
                    Vector2 dir = cen - impact; float dist = Mathf.Max(60f, dir.magnitude); dir /= dist;
                    float speed = Mathf.Clamp(260000f / (dist + 150f), 250f, 1100f) * UnityEngine.Random.Range(0.7f, 1.3f);
                    shards.Add((rt, basePos, dir * speed, UnityEngine.Random.Range(-420f, 420f), dist / 3000f, sg));
                }
            }
        kanekiImage.enabled = false;
        if (!reverse) { Impact(new Color(1f, 1f, 1f, 0.85f), 0.6f, true); StartCoroutine(Dust(36)); }
        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / seconds);
            float f = reverse ? 1f - u : u;
            foreach (var sh in shards)
            {
                float tt = Mathf.Max(0f, f - sh.delay * 0.3f) * seconds;
                sh.rt.anchoredPosition = sh.basePos + sh.vel * tt + new Vector2(0f, -1500f * tt * tt);
                sh.rt.localRotation = Quaternion.Euler(0, 0, sh.spin * tt);
                sh.g.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.45f, 1f, f));
            }
            yield return null;
        }
        foreach (var sh in shards) if (sh.rt != null) Destroy(sh.rt.gameObject);
        if (reverse) kanekiImage.enabled = true;   // 다 붙었다 — 그림 복귀
    }

    IEnumerator Dust(int count)
    {
        var live = new List<(Image img, Vector2 pos, Vector2 vel, float life)>();
        for (int i = 0; i < count; i++)
        {
            Image img = GetParticle();
            img.sprite = null; img.color = UnityEngine.Random.value < 0.5f ? Color.white : new Color(1f, 0.9f, 0.5f);
            float sz = UnityEngine.Random.Range(5f, 14f); img.rectTransform.sizeDelta = new Vector2(sz, sz);
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f), sp = UnityEngine.Random.Range(200f, 900f);
            live.Add((img, new Vector2(960f, 470f), new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, UnityEngine.Random.Range(0.5f, 1.1f)));
        }
        float t = 0f;
        while (t < 1.2f)
        {
            t += Dt;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i]; p.pos += p.vel * Dt; p.vel.y -= 900f * Dt; live[i] = p;
                p.img.rectTransform.anchoredPosition = new Vector2(p.pos.x, -p.pos.y);
                Color c = p.img.color; c.a = Mathf.Clamp01(1f - t / p.life); p.img.color = c;
            }
            yield return null;
        }
        foreach (var p in live) p.img.gameObject.SetActive(false);
    }

    /// <summary>꽝: 깨진 그림 뒤에서 kaneki_miss가 어둡게 시작해 밝아지고, 붉은 눈이 은은하게 빛난다.</summary>
    /// <summary>지금 화면 전체를 캡처해 유리조각으로 깬다. reverse=false(꽝): 금 → 0.8초 안에 확 퍼지며 조각이 떨어지고 뒤에서 kaneki_miss가 드러난다.
    /// reverse=true(부활): 금이 가다 멈추고 조각이 살짝 벌어졌다 도로 붙는다.</summary>
    IEnumerator ShatterScreen(bool reverse, float seconds)
    {
        heartbeat = false;
        yield return new WaitForEndOfFrame();
        Texture2D cap = ScreenCapture.CaptureScreenshotAsTexture();
        if (cap == null) { if (!reverse) { if (curTheme == 1) StartCoroutine(PlayResultVideo()); else StartCoroutine(MissReveal()); } yield break; }
        Rect rr = root.rect; float W = rr.width, H = rr.height;
        const int cols = 12, rows = 7;
        var grid = new Vector2[cols + 1, rows + 1];
        for (int gy = 0; gy <= rows; gy++)
            for (int gx = 0; gx <= cols; gx++)
            {
                float jx = (gx == 0 || gx == cols) ? 0f : UnityEngine.Random.Range(-0.38f, 0.38f), jy = (gy == 0 || gy == rows) ? 0f : UnityEngine.Random.Range(-0.38f, 0.38f);
                grid[gx, gy] = new Vector2(((gx + jx) / cols - 0.5f) * W, ((gy + jy) / rows - 0.5f) * H);
            }
        var shards = new List<(RectTransform rt, Vector2 basePos, Vector2 vel, float spin, float delay, ShardGraphic g, float tint)>();
        Vector2 impact = new Vector2(W * 0.03f, H * 0.08f);
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                Vector2 a = grid[gx, gy], b = grid[gx + 1, gy], c = grid[gx + 1, gy + 1], d = grid[gx, gy + 1];
                Vector2[][] tris = UnityEngine.Random.value < 0.5f ? new[] { new[] { a, b, c }, new[] { a, c, d } } : new[] { new[] { a, b, d }, new[] { b, c, d } };
                foreach (Vector2[] tri in tris)
                {
                    Vector2 cen = (tri[0] + tri[1] + tri[2]) / 3f;
                    RectTransform rt = Rt("shard", shatterRoot);
                    var sg = rt.gameObject.AddComponent<ShardGraphic>();
                    sg.tex = cap; sg.raycastTarget = false;
                    for (int i = 0; i < 3; i++) { sg.pts[i] = tri[i] - cen; sg.uvs[i] = new Vector2(tri[i].x / W + 0.5f, tri[i].y / H + 0.5f); }
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = Vector2.zero; rt.anchoredPosition = cen;
                    Vector2 dir = cen - impact; float dist = Mathf.Max(80f, dir.magnitude); dir /= dist;
                    float speed = Mathf.Clamp(520000f / (dist + 200f), 500f, 1700f) * UnityEngine.Random.Range(0.7f, 1.3f);
                    shards.Add((rt, cen, dir * speed, UnityEngine.Random.Range(-480f, 480f), dist / 5000f, sg, UnityEngine.Random.Range(0.86f, 1.12f)));
                }
            }
        // 금(충격점에서 방사)
        var lines = new List<RectTransform>();
        for (int i = 0; i < 14; i++)
        {
            RectTransform r = Rt("crack", shatterRoot);
            var im = r.gameObject.AddComponent<Image>(); im.color = curTheme == 1 ? new Color(0.85f, 0.75f, 1f, 0.95f) : new Color(1f, 0.28f, 0.25f, 0.95f); im.raycastTarget = false;   // TG 붉은 균열 · RZ 보라/은빛
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = impact; r.sizeDelta = new Vector2(UnityEngine.Random.Range(3f, 6f), 0f);
            r.localRotation = Quaternion.Euler(0, 0, i * (360f / 14f) + UnityEngine.Random.Range(-12f, 12f));
            lines.Add(r);
        }
        Impact(new Color(1f, 1f, 1f, 0.5f), 0.4f, false);
        float ct = 0f; const float crackSeconds = 0.22f;
        while (ct < crackSeconds)
        {
            ct += Dt; float u = Mathf.Clamp01(ct / crackSeconds);
            foreach (RectTransform r in lines) r.sizeDelta = new Vector2(r.sizeDelta.x, Mathf.Lerp(0f, Mathf.Max(W, H), u));
            shakeUntil = Now + 0.05f;
            yield return null;
        }
        if (!reverse)
        {
            // 쨍 — 섬광 · 흔들림 · 가루. 아래 층은 kaneki_miss로 바꿔 둔다(그림이 조각 뒤에서 드러난다).
            StopVideoNow();
            kanekiImage.enabled = false;
            if (curTheme == 1) StartCoroutine(PlayResultVideo()); else StartCoroutine(MissReveal());
            Impact(new Color(1f, 1f, 1f, 0.95f), 0.7f, true);
            StartCoroutine(GlassDust(shatterRoot, impact, 60));
        }
        foreach (RectTransform r in lines) Destroy(r.gameObject);
        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / seconds);
            float f = reverse ? 0.22f * Mathf.Sin(u * Mathf.PI) : u;
            foreach (var sh in shards)
            {
                float tt = Mathf.Max(0f, f - sh.delay * 0.15f) * (reverse ? 1f : 0.9f);
                sh.rt.anchoredPosition = sh.basePos + sh.vel * tt + new Vector2(0f, reverse ? 0f : -2600f * tt * tt);
                sh.rt.localRotation = Quaternion.Euler(0, 0, sh.spin * tt * (reverse ? 0.5f : 1f));
                float fade = reverse ? 0f : Mathf.SmoothStep(0.5f, 1f, f);
                float b = reverse ? 1f : Mathf.Lerp(1f, sh.tint, 0.8f);   // 조각마다 빛 반사처럼 밝기가 다르다
                sh.g.color = curTheme == 1 ? new Color(b * 0.92f, b * 0.9f, b * 1.1f, 1f - fade) : new Color(b, b * 0.97f, b * 0.97f, 1f - fade);
            }
            yield return null;
        }
        foreach (var sh in shards) if (sh.rt != null) Destroy(sh.rt.gameObject);
        Destroy(cap);
    }

    IEnumerator GlassDust(Transform parent, Vector2 origin, int count)
    {
        var live = new List<(Image img, Vector2 pos, Vector2 vel, float life)>();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("dust", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
            img.color = UnityEngine.Random.value < 0.6f ? Color.white : new Color(0.8f, 0.95f, 1f);
            float sz = UnityEngine.Random.Range(4f, 12f); img.rectTransform.sizeDelta = new Vector2(sz, sz);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f), sp = UnityEngine.Random.Range(300f, 1400f);
            live.Add((img, origin, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, UnityEngine.Random.Range(0.4f, 1f)));
        }
        float t = 0f;
        while (t < 1.1f)
        {
            t += Dt;
            for (int i = 0; i < live.Count; i++)
            {
                var p = live[i]; p.pos += p.vel * Dt; p.vel.y -= 1200f * Dt; live[i] = p;
                if (p.img == null) continue;
                p.img.rectTransform.anchoredPosition = p.pos;
                Color c = p.img.color; c.a = Mathf.Clamp01(1f - t / p.life); p.img.color = c;
            }
            yield return null;
        }
        foreach (var p in live) if (p.img != null) Destroy(p.img.gameObject);
    }

    IEnumerator MissReveal()
    {
        missImage.sprite = Spr("Scene/pachinko_miss");
        if (missImage.sprite == null) yield break;
        missImage.enabled = true;
        videoShow = videoShowTarget = 0f;   // 영상은 끊고 아래 층을 드러낸다
        float t = 0f;
        while (true)
        {
            t += Dt;
            float b = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(t / 1.4f));
            missImage.color = new Color(b, b, b, 1f);
            eyeGlow.color = new Color(1f, 0.12f, 0.1f, Mathf.Clamp01(t / 1.4f) * (0.28f + 0.2f * Mathf.Sin(t * 5f)));
            yield return null;
            if (running == null) yield break;
        }
    }

    /// <summary>당첨: 섬광과 함께 win 그림으로 — 붉은 카구네 쪽에서 빛이 폭발한다.</summary>
    IEnumerator WinPicture()
    {
        heartbeat = false;
        Impact(new Color(1f, 1f, 1f, 0.5f), 0.3f, true);
        StartCoroutine(ImpactCut(new Color(0.9f, 0.05f, 0.1f)));
        kanekiImage.enabled = true;
        kanekiImage.sprite = Spr("Scene/win");
        Place(kanekiPic, 960 - 800, 540 - 450, 1600, 900);
        kanekiPic.localRotation = Quaternion.identity;
        StartCoroutine(LineFlare(560f, new Color(1f, 0.25f, 0.2f, 0.95f), 3));   // 붉은 카구네 쪽 줄 플레어
        float t = 0f;
        while (t < 0.6f)
        {
            t += Dt; float u = t / 0.6f;
            kanekiPic.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(u * 2f), 3f));
            yield return null;
        }
        kanekiPic.localScale = Vector3.one;
    }

    // ───────── Re:ゼロ 테마 ─────────
    void PrepareResultVideo()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, "rezero_miss_6s.mp4");
        if (!System.IO.File.Exists(path)) return;
        videoPlayer2.url = path;
        if (RecordMode) videoPlayer2.timeReference = UnityEngine.Video.VideoTimeReference.ExternalTime;
        videoPlayer2.Prepare();
    }

    /// <summary>꽝: 깨진 화면 뒤에서 결과 영상(6.8초, 소리 포함)이 나온다.</summary>
    IEnumerator PlayResultVideo()
    {
        StopVideoNow();
        videoShow = videoShowTarget = 1f;
        float wait = 0f;
        while (!videoPlayer2.isPrepared && wait < 3f) { wait += Dt; yield return null; }
        if (!videoPlayer2.isPrepared) yield break;
        float st = Now;
        videoPlayer2.time = 0.0; videoPlayer2.Play();
        AudioEvent?.Invoke("video:rezero_miss_6s.mp4", st, AudioPrefs.MusicVolume);
        LoadImpacts("rezero_miss_6s.mp4", 2, st);
        while (running != null && videoPlayer2.isPlaying) { if (RecordMode) videoPlayer2.externalReferenceTime = Now - st; yield return null; }
    }

    IEnumerator WaitUntilTime(float t) { while (Now < t) yield return null; }

    IEnumerator SequenceRz(PachinkoPlan p)
    {
        float t0 = Now;
        kanekiRect.gameObject.SetActive(false); heartbeat = false;
        Play(Slot.Open);
        float u = 0f;
        while (u < 0.4f)
        {
            u += Dt; float e = Mathf.Clamp01(u / 0.4f); e = 1f - Mathf.Pow(1f - e, 3f);
            machineRect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, e);
            machineRect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-6f, 0f, e));
            yield return null;
        }
        machineRect.localScale = Vector3.one; machineRect.localRotation = Quaternion.identity;

        // 룰렛 시작 = 배경 영상(결과가 미리 정해져 있어 꽝/당첨 영상을 고른다) — 영상 끝에서 결과
        curReach = p.reach; reelsCenterHeld = true;
        { int off = UnityEngine.Random.Range(0, 9); for (int ri = 0; ri < 3; ri++) reels[ri].scroll = off + ri * 3; }
        foreach (Reel r in reels) r.spinning = true;
        reels[1].liftT = 1f;
        videoFileName = p.win ? "rezero_spin_win.mp4" : "rezero_spin_miss.mp4"; videoNatural = true;
        StartVideo();
        if (!p.win) PrepareResultVideo();
        StartCoroutine(SpinLoop(9f));
        holdOrbs[0].color = HoldColor(p.hold);
        SetExpect(new[] { 2, 8, 20, 80, 100 }[(int)p.hold]);

        float P = p.PreludeSeconds();
        yield return WaitUntilTime(t0 + P * 0.28f);
        StartCoroutine(StopReel(0, p.left, 1.5f, spinSpeeds[0]));
        yield return WaitUntilTime(t0 + P * 0.52f);
        StartCoroutine(StopReel(2, p.right, 1.9f, spinSpeeds[2]));
        if (p.reach)
        {
            yield return WaitUntilTime(t0 + P * 0.52f + 2.0f);
            reels[1].Highlight(new Color(1f, 0.78f, 0.8f));
            StartCoroutine(GearFx(0.5f));   // 사망 귀환 — 시계 톱니
            StartCoroutine(ReachEmphasis()); SharpRays(true);
            StartCoroutine(Pop(logoReach, 1.3f, 3f));
            SetExpect(Mathf.Max(expect, 10));
            float next = t0 + P * 0.52f + 3.6f;
            for (int k = 1; k <= p.yokoku && next + 1.05f < t0 + P; k++)
            {
                yield return WaitUntilTime(next);
                yokokuSmall.text = $"連続予告 ×{k + 1}"; yokokuSmall.gameObject.SetActive(true);
                SetExpect(Mathf.Min(95, expect + 12));
                StartCoroutine(Pop(logoKeizoku, 0.9f, 2.4f));
                yield return Wait(1.0f); yokokuSmall.gameObject.SetActive(false);
                next += 1.2f;
            }
        }
        yield return WaitUntilTime(t0 + P);
        SharpRays(false);

        yield return PushWindow();
        if (pushedWho == int.MinValue)
        {
            float wait = standalone ? 0f : 4f;
            while (pushedWho == int.MinValue && wait > 0f) { wait -= Dt; yield return null; }
            if (pushedWho == int.MinValue) pushedWho = -1;
        }
        pushGroup.gameObject.SetActive(false); dim.color = new Color(0, 0, 0, 0);
        float resultStart = Now;

        // 가운데 줄 정지 — 영상이 끝나는 순간 결과
        reels[1].Highlight(Color.white);
        if (p.win) yield return WarnStrobe();
        StopAllSpin();
        yield return StopReel(1, p.center, p.reach ? 0.9f : 0.6f, spinSpeeds[1] * 0.5f);
        yield return Wait(0.2f);

        if (p.win)
        {
            if (videoPlayer.isPlaying) videoPlayer.Pause();   // 마지막 프레임을 붙잡아 둔다
            StartCoroutine(WinFlashStandard()); StartCoroutine(ImpactCut(curTheme == 1 ? new Color(0.7f, 0.5f, 1f) : new Color(0.9f, 0.05f, 0.1f))); shakeUntil = Now + 0.4f;
            StartCoroutine(WinGlints(p.ResultSeconds()));
            StartCoroutine(LineFlare(470f, curTheme == 1 ? new Color(0.8f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.7f, 0.9f)));
            StartCoroutine(curTheme == 1 ? RzWinFx() : KagureWinFx());
            SetExpect(100);
            LoadRzLines();
            AudioClip line = RzWinLines[Mathf.Clamp(p.center, 1, 9) - 1];
            if (line != null) PlayLine(line);
            if (p.jackpot)
            {
                StartCoroutine(Confetti(Mathf.Max(5.5f, p.ResultSeconds() - 0.4f)));
                StartCoroutine(Fireworks(true, 1));
                StartCoroutine(Pop(logoOoatari, p.ResultSeconds() + 0.5f, 1.2f, keep: true));
                StartCoroutine(Later(3.0f, Fireworks(true, curTheme)));
                ShowBand("大当たり 보상: 랜덤 유닛 ×1");
            }
            else
            {
                StartCoroutine(Fireworks(false, 1));
                StartCoroutine(Pop(logoAtari, p.ResultSeconds() + 0.5f, 1.2f, keep: true));
                ShowBand("当たり 보상: 전설·히든 위습 ×1");
            }
        }
        else
        {
            LoadRzLines();
            PlayLine(RzMissLines[p.reach ? 0 : 1]);   // 리치 꽝 = R10(あなた怠惰ですね) · 그냥 꽝 = R11(脳が震える)
            StartCoroutine(ShatterScreen(false, 0.8f));
            StartCoroutine(Butterflies(10, 1.4f));
            StartCoroutine(Pop(logoZannen, 4f, 3f, keep: true));
            ShowBand(p.reach ? "아쉬움 보상: 희귀함 위습 ×1" : "아쉬움 보상: 특별함 위습 ×1");
        }
        float until = resultStart + p.ResultSeconds();
        while (Now < until) yield return null;
        yield return ReturnFade();
        while (VoiceActive()) yield return null;
        StopAllSfx();
        StopVideoNow();
        if (videoPlayer2 != null) videoPlayer2.Stop();
        duckTarget = 1f; GameBgm.Duck = 1f;
        root.gameObject.SetActive(false);
        StartCoroutine(ReturnFadeOut());
        running = null;
    }

    static void LoadRzLines()
    {
        for (int i = 0; i < 2; i++) if (RzMissLines[i] == null) RzMissLines[i] = Resources.Load<AudioClip>(Folder + (i == 0 ? "Sfx/rz_line_reachmiss" : "Sfx/rz_line_miss"));
        for (int i = 0; i < 9; i++) if (RzWinLines[i] == null) RzWinLines[i] = Resources.Load<AudioClip>(Folder + $"Sfx/rz_line_win_{i + 1}");   // 111~999 (777=エミリア 「ありがとうって…」)
    }

    // ───────── 당첨 폭죽(사장님 10-14): 아래에서 꼬리를 끌며 솟아 정점에서 구형으로 터진다 ─────────
    class Fw { public Sprite spark; public Image img; public Vector2 pos, vel; public float age, life, size, gravity, drag, apexY; public Color col; public int kind; public bool spawned2; }   // kind 0 로켓 · 1 불꽃 · 2 꼬리 · 3 2차 반짝

    Image GetFwImage()
    {
        foreach (Image i in fwPool) if (!i.gameObject.activeSelf) { i.gameObject.SetActive(true); return i; }
        Image img = Img("fw", fireworkRoot, circle, Color.white);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
        fwPool.Add(img);
        return img;
    }

    IEnumerator Fireworks(bool jackpot, int theme)
    {
        Color[] palette = theme == 1
            ? new[] { new Color(0.85f, 0.9f, 1f), new Color(0.3f, 0.95f, 0.85f), new Color(0.65f, 0.45f, 1f), Color.white }
            : (jackpot ? new[] { new Color(1f, 0.82f, 0.25f), new Color(1f, 0.2f, 0.25f), Color.white, new Color(1f, 0.55f, 0.1f) }
                       : new[] { new Color(1f, 0.82f, 0.25f), new Color(1f, 0.55f, 0.1f), Color.white });
        int shots = jackpot ? 12 : UnityEngine.Random.Range(5, 7);
        var live = new List<Fw>();
        float next = 0f; int launched = 0; bool volley = false;
        Color Pick() => jackpot && UnityEngine.Random.value < 0.4f ? Color.HSVToRGB(UnityEngine.Random.value, 0.85f, 1f) : palette[UnityEngine.Random.Range(0, palette.Length)];
        void Launch(float x)
        {
            Image img = GetFwImage();
            var f = new Fw { img = img, pos = new Vector2(x, 1110f), vel = new Vector2(UnityEngine.Random.Range(-40f, 40f), -UnityEngine.Random.Range(1100f, 1500f)), age = 0f, life = 2f, size = 6f, gravity = 0f, drag = 1f,
                             apexY = UnityEngine.Random.Range(220f, 560f), col = Pick(), kind = 0 };
            live.Add(f);
        }
        void Burst(Fw rocket)
        {
            int n = jackpot ? UnityEngine.Random.Range(90, 121) : UnityEngine.Random.Range(60, 91);
            Color main = rocket.col, alt = palette[UnityEngine.Random.Range(0, palette.Length)];
            float baseSpeed = UnityEngine.Random.Range(320f, 560f);
            for (int i = 0; i < n; i++)
            {
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float sp = baseSpeed * Mathf.Sqrt(UnityEngine.Random.value) + 80f;   // 구형(속이 찬 원)
                Image img = GetFwImage();
                img.preserveAspect = true;
                live.Add(new Fw { img = img, spark = FxSpr("spark_" + UnityEngine.Random.Range(1, 9)), pos = rocket.pos, vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, age = 0f, life = UnityEngine.Random.Range(1.0f, 1.7f),
                                  size = UnityEngine.Random.Range(2.5f, 4.5f), gravity = 520f, drag = 0.985f, col = UnityEngine.Random.value < 0.7f ? main : alt, kind = 1 });
            }
            StartCoroutine(FlashColor(new Color(1f, 0.95f, 0.8f, 0.1f), 0.12f));   // 터질 때 작은 섬광
            Play(Slot.Firework);
        }
        while ((launched < shots || live.Count > 0 || (jackpot && !volley)) && running != null)
        {
            float dt = Dt;
            next -= dt;
            if (launched < shots && next <= 0f)
            {
                Launch(UnityEngine.Random.Range(160f, 1760f)); launched++;
                if (jackpot && launched % 3 == 0) Launch(UnityEngine.Random.Range(160f, 1760f));   // 777은 동시 다발
                next = jackpot ? UnityEngine.Random.Range(0.18f, 0.3f) : UnityEngine.Random.Range(0.25f, 0.4f);
            }
            else if (jackpot && launched >= shots && !volley && live.Count < 60)
            {   // 마지막 한 번 — 화면 가득 동시에
                volley = true;
                for (int k = 0; k < 8; k++) Launch(180f + k * 223f + UnityEngine.Random.Range(-40f, 40f));
            }
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Fw f = live[i];
                f.age += dt;
                if (f.kind == 0)
                {   // 로켓: 꼬리를 끌며 솟는다
                    f.vel.y *= 0.985f; f.pos += f.vel * dt;
                    if (UnityEngine.Random.value < 0.9f)
                    {
                        Image t = GetFwImage();
                        live.Add(new Fw { img = t, pos = f.pos, vel = new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(20f, 90f)), age = 0f, life = 0.4f, size = 3f, gravity = 0f, drag = 1f, col = f.col, kind = 2 });
                    }
                    if (f.pos.y <= f.apexY || f.vel.y > -250f) { Burst(f); f.img.gameObject.SetActive(false); live.RemoveAt(i); continue; }
                }
                else
                {
                    f.vel.y += f.gravity * dt; f.vel *= Mathf.Pow(f.drag, dt * 60f); f.pos += f.vel * dt;
                    if (f.kind == 1 && !f.spawned2 && f.age > f.life * 0.6f && UnityEngine.Random.value < 0.05f)
                    {   // 터진 뒤 작은 2차 반짝
                        f.spawned2 = true;
                        for (int k = 0; k < 3; k++)
                        {
                            Image t = GetFwImage();
                            live.Add(new Fw { img = t, pos = f.pos, vel = new Vector2(UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(-90f, 90f)), age = 0f, life = 0.4f, size = 3f, gravity = 200f, drag = 0.97f, col = Color.white, kind = 3 });
                        }
                    }
                    if (f.age >= f.life) { f.img.gameObject.SetActive(false); live.RemoveAt(i); continue; }
                }
                float k01 = Mathf.Clamp01(f.age / Mathf.Max(0.01f, f.life));
                float a = f.kind == 0 ? 1f : (1f - k01) * (f.kind == 1 ? (0.6f + 0.4f * Mathf.Sin(f.age * 40f + f.size)) : 1f);   // 반짝임 꼬리
                f.img.rectTransform.anchoredPosition = new Vector2(f.pos.x, -f.pos.y);
                float sz = f.size * (f.kind == 1 ? Mathf.Lerp(1f, 0.5f, k01) : 1f);
                float spd = f.vel.magnitude;
                if (f.spark != null && f.kind == 1)
                {   // blender 불똥 그림(날카로운 별빛·파편)
                    f.img.sprite = f.spark;
                    float ss = Mathf.Lerp(46f, 18f, k01) * (f.size / 3.5f);
                    f.img.rectTransform.sizeDelta = new Vector2(ss, ss);
                    f.img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, f.age * 240f * (f.size - 3f) + f.size * 40f);
                }
                else
                {
                    f.img.sprite = null;   // 둥근 빛구슬 대신 속도 방향의 가는 불똥
                    f.img.rectTransform.sizeDelta = new Vector2(sz, f.kind == 0 ? 24f : Mathf.Clamp(spd * 0.045f, 6f, 26f));
                    f.img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-f.vel.y, f.vel.x) * Mathf.Rad2Deg - 90f);
                }
                f.img.color = new Color(f.col.r, f.col.g, f.col.b, Mathf.Clamp01(a));
            }
            yield return null;
        }
        foreach (Fw f in live) f.img.gameObject.SetActive(false);
        foreach (Image i in fwPool) i.gameObject.SetActive(false);
    }

    // ───────── 도쿄구울: 카구네 촉수 · 붓 획 / 리제로: 시계 톱니 · 보라 나비 ─────────
    class RibbonGraphic : MaskableGraphic
    {
        public List<Vector2> pts = new List<Vector2>(); public float w0 = 30f, w1 = 4f; public bool both;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int n = pts.Count; if (n < 2) return;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 tan = (b - a).normalized; Vector2 nor = new Vector2(-tan.y, tan.x);
                float u = (float)i / (n - 1);
                float w = both ? w0 * Mathf.Sin(u * Mathf.PI) : Mathf.Lerp(w0, w1, u);
                vh.AddVert(pts[i] + nor * w * 0.5f, color, Vector2.zero);
                vh.AddVert(pts[i] - nor * w * 0.5f, color, Vector2.zero);
            }
            for (int i = 0; i < n - 1; i++) { int k = i * 2; vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k + 1, k + 3, k + 2); }
        }
    }

    class ButterflyGraphic : MaskableGraphic
    {
        public float flap = 1f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float w = 22f * Mathf.Max(0.15f, Mathf.Abs(flap)), h = 18f;
            // 날개 네 장(삼각형 둘씩): 위쪽 큰 날개 + 아래쪽 작은 날개
            Vector2[][] tris = {
                new[] { Vector2.zero, new Vector2(-w, h), new Vector2(-w * 0.9f, -h * 0.2f) },
                new[] { Vector2.zero, new Vector2(w, h), new Vector2(w * 0.9f, -h * 0.2f) },
                new[] { Vector2.zero, new Vector2(-w * 0.7f, -h * 0.1f), new Vector2(-w * 0.5f, -h * 0.9f) },
                new[] { Vector2.zero, new Vector2(w * 0.7f, -h * 0.1f), new Vector2(w * 0.5f, -h * 0.9f) } };
            int vi = 0;
            foreach (Vector2[] t in tris) { for (int i = 0; i < 3; i++) vh.AddVert(t[i], color, Vector2.zero); vh.AddTriangle(vi, vi + 1, vi + 2); vi += 3; }
        }
    }

    /// <summary>blender fx 그림(Resources/Pachinko/Fx/<이름>) — 없으면 null(코드 대체물로 폴백). 파일만 바꾸면 교체된다.</summary>
    static Sprite FxSpr(string name) => Spr("Fx/" + name);

    Image NewFxImage(Sprite sprite, Color color, Vector2 size)
    {
        Image im = Img("fx", fireworkRoot, sprite, color);
        im.preserveAspect = false;
        im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0f, 1f);
        im.rectTransform.sizeDelta = size;
        return im;
    }

    RibbonGraphic NewRibbon(Color c, float w0, float w1, bool both = false)
    {
        RectTransform r = Rt("ribbon", fireworkRoot);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero;
        var g = r.gameObject.AddComponent<RibbonGraphic>(); g.color = c; g.w0 = w0; g.w1 = w1; g.both = both; g.raycastTarget = false;
        return g;
    }

    /// <summary>붉은 카구네 촉수가 화면 가장자리에서 휘감아 들어왔다 빠진다(도쿄구울 테마: 리치·컷인·당첨 순간).</summary>
    IEnumerator Tentacle(Vector2 from, Vector2 to, float seconds, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        RibbonGraphic outline = NewRibbon(new Color(0.03f, 0f, 0.02f, 0.95f), 46f, 6f), body = NewRibbon(new Color(0.8f, 0.04f, 0.1f, 0.95f), 30f, 3f);
        Vector2 perp = new Vector2(-(to - from).y, (to - from).x).normalized;
        float phase = UnityEngine.Random.Range(0f, 6.28f), amp = UnityEngine.Random.Range(90f, 170f);
        const int N = 26;
        float t = 0f;
        while (t < seconds)
        {
            t += Dt; float u = t / seconds;
            float ext = u < 0.45f ? Mathf.SmoothStep(0f, 1f, u / 0.45f) : (u < 0.62f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.62f) / 0.38f));
            outline.pts.Clear(); body.pts.Clear();
            for (int i = 0; i <= N; i++)
            {
                float k = (float)i / N * ext;
                Vector2 pt = Vector2.Lerp(from, to, k) + perp * Mathf.Sin(k * 7f + phase + t * 6f) * amp * k;
                Vector2 ui = new Vector2(pt.x, -pt.y);
                outline.pts.Add(ui); body.pts.Add(ui);
            }
            outline.SetVerticesDirty(); body.SetVerticesDirty();
            yield return null;
        }
        Destroy(outline.gameObject); Destroy(body.gameObject);
    }

    void Tentacles(int count)
    {
        bool haveArt = FxSpr("kagune_1") != null;
        var order = new List<int> { 1, 2, 3, 4 };
        for (int i = 0; i < count; i++)
        {
            if (haveArt)
            {
                int pick = order[UnityEngine.Random.Range(0, order.Count)]; order.Remove(pick); if (order.Count == 0) order.AddRange(new[] { 1, 2, 3, 4 });
                StartCoroutine(KagureImage(pick, i * 0.08f));
                continue;
            }
            int edge = UnityEngine.Random.Range(0, 4);
            Vector2 from = edge == 0 ? new Vector2(-40f, UnityEngine.Random.Range(150f, 950f)) : edge == 1 ? new Vector2(1960f, UnityEngine.Random.Range(150f, 950f))
                         : edge == 2 ? new Vector2(UnityEngine.Random.Range(200f, 1700f), 1120f) : new Vector2(UnityEngine.Random.Range(200f, 1700f), -40f);
            Vector2 to = new Vector2(960f + UnityEngine.Random.Range(-420f, 420f), 540f + UnityEngine.Random.Range(-230f, 230f));
            StartCoroutine(Tentacle(from, to, UnityEngine.Random.Range(0.55f, 0.8f), i * 0.08f));
        }
    }

    /// <summary>blender 카구네 그림: 1·2 아래 왼/오른쪽 · 3 왼쪽 가장자리 · 4 아래 가운데 — 가장자리에서 휘감아 들어왔다 빠진다.</summary>
    IEnumerator KagureImage(int variant, float delay)
    {
        if (delay > 0f) yield return Wait(delay);
        Sprite sp = FxSpr("kagune_" + variant);
        if (sp == null) yield break;
        float h = 1080f * 1.05f, w = h * 0.5f;   // 1024×2048 비율
        bool mirror = UnityEngine.Random.value < 0.5f;
        Image im = NewFxImage(sp, new Color(1f, 1f, 1f, 0.96f), new Vector2(w, h));
        Vector2 home, away;   // 화면 좌표(왼쪽 위 원점), 이미지 중심
        switch (variant)
        {
            case 1: home = new Vector2(w * 0.5f - 40f, 1080f - h * 0.5f + 60f); away = home + new Vector2(0f, h * 0.7f); break;
            case 2: home = new Vector2(1920f - w * 0.5f + 40f, 1080f - h * 0.5f + 60f); away = home + new Vector2(0f, h * 0.7f); break;
            case 3: home = new Vector2(w * 0.5f - 60f, 540f); away = home + new Vector2(-w, 0f); break;
            default: home = new Vector2(960f, 1080f - h * 0.5f + 80f); away = home + new Vector2(0f, h * 0.7f); break;
        }
        if (mirror && variant != 4) { home.x = 1920f - home.x; away.x = 1920f - away.x; im.rectTransform.localScale = new Vector3(-1f, 1f, 1f); }
        const float total = 0.75f;
        float t = 0f;
        while (t < total)
        {
            t += Dt; float u = t / total;
            float k = u < 0.4f ? Mathf.SmoothStep(0f, 1f, u / 0.4f) : (u < 0.6f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
            Vector2 pos = Vector2.Lerp(away, home, k) + new Vector2(Mathf.Sin(t * 22f) * 6f, 0f) * k;   // 꿈틀
            im.rectTransform.anchoredPosition = new Vector2(pos.x, -pos.y);
            yield return null;
        }
        Destroy(im.gameObject);
    }

    /// <summary>먹/피 붓 획 베기: 대각선으로 한 번 쓱.</summary>
    IEnumerator BrushSlash(bool leftToRight)
    {
        string[] names = { "brush_ink", "brush_blood", "brush_ink_red" };
        Sprite bs = FxSpr(names[UnityEngine.Random.Range(0, names.Length)]);
        if (bs != null)
        {
            Image im = NewFxImage(bs, Color.white, new Vector2(2300f, 575f));
            im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Horizontal; im.fillOrigin = leftToRight ? 0 : 1; im.fillAmount = 0f;
            im.rectTransform.anchoredPosition = new Vector2(960f, -540f);
            im.rectTransform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-28f, 28f));
            if (!leftToRight) im.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            float bt = 0f;
            while (bt < 0.42f)
            {
                bt += Dt; float u = bt / 0.42f;
                im.fillAmount = Mathf.Clamp01(u / 0.25f);   // 쓱
                im.color = new Color(1f, 1f, 1f, u < 0.5f ? 1f : 1f - (u - 0.5f) / 0.5f);
                yield return null;
            }
            Destroy(im.gameObject);
            yield break;
        }
        Vector2 a = leftToRight ? new Vector2(-80f, 90f) : new Vector2(2000f, 90f), b = leftToRight ? new Vector2(2000f, 1000f) : new Vector2(-80f, 1000f);
        RibbonGraphic ink = NewRibbon(new Color(0.02f, 0.01f, 0.02f, 0.95f), 150f, 0f, true), edge = NewRibbon(new Color(0.9f, 0.05f, 0.1f, 0.95f), 16f, 0f, true);
        const int N = 22;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Dt; float u = t / 0.45f;
            float draw = Mathf.Clamp01(u / 0.22f), fade = Mathf.Clamp01(1f - (u - 0.45f) / 0.55f);
            ink.pts.Clear(); edge.pts.Clear();
            Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (int i = 0; i <= N; i++)
            {
                float k = (float)i / N * draw;
                Vector2 pt = Vector2.Lerp(a, b, k) + perp * Mathf.Sin(k * 3f) * 24f;
                ink.pts.Add(new Vector2(pt.x, -pt.y)); edge.pts.Add(new Vector2(pt.x, -pt.y) + perp * 38f);
            }
            ink.color = new Color(0.02f, 0.01f, 0.02f, 0.95f * fade); edge.color = new Color(0.9f, 0.05f, 0.1f, 0.95f * fade);
            ink.SetVerticesDirty(); edge.SetVerticesDirty();
            yield return null;
        }
        Destroy(ink.gameObject); Destroy(edge.gameObject);
    }

    IEnumerator KagureWinFx()
    {
        Tentacles(4);
        yield return Wait(0.18f);
        yield return BrushSlash(UnityEngine.Random.value < 0.5f);
    }

    /// <summary>리제로: 시계 톱니가 짧게 돌고(사망 귀환) 보라 나비가 흩어진다.</summary>
    IEnumerator GearFx(float seconds)
    {
        Sprite emblem = FxSpr("clock_emblem");
        if (emblem != null)
        {
            Image em = NewFxImage(emblem, new Color(1f, 1f, 1f, 0.9f), new Vector2(900f, 900f));
            em.rectTransform.anchoredPosition = new Vector2(960f, -540f);
            float gt = 0f;
            while (gt < seconds)
            {
                gt += Dt; float u = gt / seconds;
                em.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.12f, u);
                em.rectTransform.localRotation = Quaternion.Euler(0, 0, -u * 220f);
                em.color = new Color(1f, 1f, 1f, 0.9f * (1f - u * u));
                yield return null;
            }
            Destroy(em.gameObject);
            yield break;
        }
        RectTransform g = Rt("gear", fireworkRoot); Place(g, 960 - 400, 540 - 400, 800, 800);
        var parts = new List<Image>();
        Color c = new Color(0.78f, 0.7f, 1f, 0.9f);
        for (int i = 0; i < 24; i++)
        {
            Image tk = Img("tick", g, null, c); tk.rectTransform.anchorMin = tk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tk.rectTransform.sizeDelta = new Vector2(i % 6 == 0 ? 10f : 5f, i % 6 == 0 ? 60f : 30f);
            float a = i * 15f * Mathf.Deg2Rad;
            tk.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 330f;
            tk.rectTransform.localRotation = Quaternion.Euler(0, 0, i * 15f - 90f);
            parts.Add(tk);
        }
        for (int h = 0; h < 2; h++)
        {
            Image hand = Img("hand", g, null, c); hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hand.rectTransform.pivot = new Vector2(0.5f, 0f); hand.rectTransform.sizeDelta = new Vector2(7f, h == 0 ? 250f : 170f);
            hand.rectTransform.anchoredPosition = Vector2.zero; parts.Add(hand);
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Dt; float u = t / seconds;
            g.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.12f, u);
            g.localRotation = Quaternion.Euler(0, 0, -u * 200f);
            parts[24].rectTransform.localRotation = Quaternion.Euler(0, 0, -u * 720f); parts[25].rectTransform.localRotation = Quaternion.Euler(0, 0, -u * 90f);
            foreach (Image im in parts) { Color cc = c; cc.a = 0.9f * (1f - u * u); im.color = cc; }
            yield return null;
        }
        Destroy(g.gameObject);
    }

    IEnumerator Butterflies(int count, float seconds)
    {
        Sprite[] frames = { FxSpr("butterfly_0"), FxSpr("butterfly_1"), FxSpr("butterfly_2"), FxSpr("butterfly_3") };
        if (frames[0] != null)
        {
            var bf = new List<(Image im, Vector2 pos, Vector2 vel, float phase, float size)>();
            for (int i = 0; i < count; i++)
            {
                float sz = UnityEngine.Random.Range(90f, 170f);
                Image im = NewFxImage(frames[0], Color.white, new Vector2(sz, sz));
                bf.Add((im, new Vector2(UnityEngine.Random.Range(560f, 1360f), UnityEngine.Random.Range(560f, 900f)),
                        new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-260f, -90f)), UnityEngine.Random.Range(0f, 6f), sz));
            }
            float bt = 0f;
            while (bt < seconds)
            {
                bt += Dt; float u = bt / seconds;
                for (int i = 0; i < bf.Count; i++)
                {
                    var b = bf[i];
                    b.pos += b.vel * Dt; b.pos.x += Mathf.Sin(bt * 3f + b.phase) * 60f * Dt; bf[i] = b;
                    int fr = (int)((bt * 12f + b.phase) % 4f);
                    b.im.sprite = frames[Mathf.Clamp(fr, 0, 3)] ?? frames[0];
                    b.im.rectTransform.anchoredPosition = new Vector2(b.pos.x, -b.pos.y);
                    b.im.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bt * 2f + b.phase) * 18f);
                    b.im.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - u * u));
                }
                yield return null;
            }
            foreach (var b in bf) if (b.im != null) Destroy(b.im.gameObject);
            yield break;
        }
        var live = new List<(ButterflyGraphic g, Vector2 pos, Vector2 vel, float phase)>();
        for (int i = 0; i < count; i++)
        {
            RectTransform r = Rt("butterfly", fireworkRoot);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.sizeDelta = Vector2.zero;
            var bg = r.gameObject.AddComponent<ButterflyGraphic>(); bg.raycastTarget = false;
            bg.color = Color.HSVToRGB(UnityEngine.Random.Range(0.72f, 0.85f), 0.55f, 1f);
            Vector2 p0 = new Vector2(UnityEngine.Random.Range(560f, 1360f), UnityEngine.Random.Range(560f, 900f));
            live.Add((bg, p0, new Vector2(UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-260f, -90f)), UnityEngine.Random.Range(0f, 6f)));
        }
        float t = 0f;
        while (t < seconds)
        {
            t += Dt; float u = t / seconds;
            for (int i = 0; i < live.Count; i++)
            {
                var b = live[i];
                b.pos += b.vel * Dt; b.pos.x += Mathf.Sin(t * 3f + b.phase) * 60f * Dt;
                live[i] = b;
                b.g.flap = Mathf.Cos(t * 18f + b.phase);
                b.g.rectTransform.anchoredPosition = new Vector2(b.pos.x, -b.pos.y);
                Color cc = b.g.color; cc.a = Mathf.Clamp01(1f - u * u); b.g.color = cc;
                b.g.SetVerticesDirty();
            }
            yield return null;
        }
        foreach (var b in live) if (b.g != null) Destroy(b.g.gameObject);
    }

    IEnumerator RzWinFx()
    {
        StartCoroutine(GearFx(0.55f));
        StartCoroutine(Butterflies(14, 1.5f));
        yield break;
    }

    // ───────── 영상 타격 연출 ─────────
    void LoadImpacts(string videoFile, int source, float baseTime)
    {
        impactTrack = new ImpactItem[0]; impactIdx = 0; impactSource = source; impactBase = baseTime;
        string name = System.IO.Path.GetFileNameWithoutExtension(videoFile);
        TextAsset ta = Resources.Load<TextAsset>(Folder + "Impacts/" + name);
        if (ta == null) return;
        ImpactList list = JsonUtility.FromJson<ImpactList>("{\"items\":" + ta.text + "}");
        if (list != null && list.items != null) impactTrack = list.items;
    }

    void ProcessImpacts()
    {
        if (impactIdx >= impactTrack.Length || impactSource == 0) return;
        UnityEngine.Video.VideoPlayer vp = impactSource == 1 ? videoPlayer : videoPlayer2;
        if (vp == null || !vp.isPlaying) return;
        // 영상 시각 기준 — 녹화 모드에선 자체 시계(프레임이 끊겨도 맞는다)
        float vt = RecordMode ? Now - impactBase : (float)vp.time;
        while (impactIdx < impactTrack.Length && impactTrack[impactIdx].t <= vt)
        {
            ImpactItem it = impactTrack[impactIdx++];
            if (vt - it.t > 0.4f) continue;   // 이미 지나간 것(탐색 직후 등)은 건너뜀
            if (impactCo != null) StopCoroutine(impactCo);
            impactCo = StartCoroutine(ImpactFx(Mathf.Clamp01(it.strength), it.kind == "both"));
        }
    }

    IEnumerator ImpactFx(float strength, bool both)
    {
        // ① 흰 섬광 α 0.25~0.6 · 0.12초
        StartCoroutine(FlashColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 0.5f, strength)), 0.06f));
        float shake = Mathf.Lerp(4f, 14f, strength), pulse = Mathf.Lerp(0.04f, 0.08f, strength);
        RectTransform vr = videoView.rectTransform;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Dt; float u = t / 0.25f;
            float k = 1f - u;
            float sc = 1f + pulse * Mathf.Sin(u * Mathf.PI);   // ③ 1.0 → 1.04~1.08 → 1.0 「두둥」
            Vector2 off = t < 0.2f ? new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake)) * k : Vector2.zero;   // ② 4~14px · 0.2초
            vr.localScale = Vector3.one * sc; vr.anchoredPosition = off;
            if (both)   // ④ 색수차
            {
                float d = 18f * strength * k;
                ghostR.rectTransform.localScale = ghostC.rectTransform.localScale = Vector3.one * sc;
                ghostR.rectTransform.anchoredPosition = off + new Vector2(-d, 0f); ghostC.rectTransform.anchoredPosition = off + new Vector2(d, 0f);
                ghostR.color = new Color(1f, 0f, 0f, 0.45f * k * videoShow); ghostC.color = new Color(0f, 1f, 1f, 0.45f * k * videoShow);
            }
            yield return null;
        }
        vr.localScale = Vector3.one; vr.anchoredPosition = Vector2.zero;
        ghostR.color = new Color(1f, 0f, 0f, 0f); ghostC.color = new Color(0f, 1f, 1f, 0f);
        impactCo = null;
    }

    // ───────── 룰렛 배경 영상 ─────────
    void StartVideo()
    {
        if (videoCo != null) StopCoroutine(videoCo);
        videoStopRequested = false;
        videoCo = StartCoroutine(VideoRoutine());
    }

    IEnumerator VideoRoutine()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[파칭코] 배경 영상 파일 없음: " + path); videoCo = null; yield break; }
        videoPlayer.url = path;
        if (RecordMode) videoPlayer.timeReference = UnityEngine.Video.VideoTimeReference.ExternalTime;
        videoPlayer.Prepare();
        float wait = 0f;
        while (!videoPlayer.isPrepared && wait < 5f) { wait += Dt; yield return null; }
        if (!videoPlayer.isPrepared) { Debug.LogWarning("[파칭코] 배경 영상 준비 실패"); videoCo = null; yield break; }
        if (videoStopRequested) { videoCo = null; yield break; }
        videoStartTime = Now;
        videoPlayer.time = 0.0;
        videoPlayer.Play();
        videoShowTarget = 1f;
        AudioEvent?.Invoke("video:" + videoFileName, videoStartTime, AudioPrefs.MusicVolume);
        LoadImpacts(videoFileName, 1, videoStartTime);
        if (videoNatural)
        {   // Re:ゼロ: 영상이 끝나면 마지막 프레임을 붙잡아 둔다(정지는 StopVideoNow가)
            while (!videoStopRequested) yield return null;
            videoCo = null; yield break;
        }
        while (Now - videoStartTime < BgVideoSeconds && !videoStopRequested) yield return null;
        videoShowTarget = 0f;
        float t = 0f;
        while (t < BgVideoFade) { t += Dt; yield return null; }
        videoPlayer.Stop();
        videoCo = null;
    }

    // ───────── 도쿄구울 음악(영상은 15초, 음악은 결과 연출이 끝날 때까지) ─────────
    void StartMusic()
    {
        if (MusicClip == null) MusicClip = Resources.Load<AudioClip>(Folder + "Sfx/pachinko_music");
        if (MusicClip == null || musicSource == null) return;
        musicSource.clip = MusicClip; musicSource.time = 0f; musicSource.Play();
        musicOn = true; musicFade = 1f; duckTarget = 0f;   // 영상·음악이 도는 동안 게임 BGM은 0
        AudioEvent?.Invoke("music:" + MusicClip.name, Now, AudioPrefs.MusicVolume);
    }

    IEnumerator MusicFadeOut(float seconds)
    {
        AudioEvent?.Invoke("musicfade", Now, seconds);
        float t = 0f;
        while (t < seconds) { t += Dt; musicFade = 1f - Mathf.Clamp01(t / seconds); yield return null; }
        musicSource.Stop(); musicOn = false; musicFade = 1f;
        duckTarget = 1f;   // 게임 BGM 복귀
    }

    void UpdateMusic()
    {
        if (musicSource == null || !musicOn) return;
        musicSource.volume = Mathf.Clamp01(AudioPrefs.MusicVolume * (VoiceActive() ? 0.4f : 1f) * musicFade);
    }

    void StopVideoNow()
    {
        if (videoCo != null) { StopCoroutine(videoCo); videoCo = null; }
        if (videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared)) videoPlayer.Stop();
        videoShow = videoShowTarget = 0f;
        videoStopRequested = false;
        impactSource = 0; impactTrack = new ImpactItem[0];
    }

    IEnumerator Wait(float s) { float end = Now + s; while (Now < end) yield return null; }

    Color HoldColor(PachinkoHold h) { switch (h) { case PachinkoHold.Blue: return BlueC; case PachinkoHold.Green: return GreenC; case PachinkoHold.Red: return RedC; case PachinkoHold.Gold: return Gold; default: return Color.white; } }

    float[] spinSpeeds = { 9f, 7.2f, 10.6f };   // 칸마다 속도를 달리해 셋이 서로 다른 숫자로 빠르게 돈다(시안 2.9/2.3/3.4 비율)
    bool curReach;
    IEnumerator SpinLoop(float speed)
    {
        float elapsed = 0f;
        bool any = true;
        while (any)
        {
            any = false;
            elapsed += Dt;
            for (int i = 0; i < reels.Length; i++)
            {
                Reel r = reels[i];
                if (!r.spinning || stopping.Contains(r)) continue;
                float v = spinSpeeds[i];
                // 가운데는 오른쪽이 선 뒤(2.4초~) 서서히 느려지며 긴장한다(리치면 더 느리고 길게)
                if (i == 1 && elapsed > 2.4f) v *= Mathf.Lerp(1f, curReach ? 0.16f : 0.3f, Mathf.Clamp01((elapsed - 2.4f) / (curReach ? 2.2f : 1.4f)));
                r.scroll += v * Dt; r.Refresh(); any = true;
            }
            if (reelsCenterHeld) any = true;
            yield return null;
            if (running == null) yield break;
        }
    }
    readonly HashSet<Reel> stopping = new HashSet<Reel>();
    bool reelsCenterHeld = true;

    IEnumerator StopReel(int index, int symbol, float seconds, float speed)
    {
        Reel r = reels[index];
        stopping.Add(r);
        yield return r.Stop(symbol, seconds, speed);
        stopping.Remove(r);
        r.Highlight(Color.white);
        StartCoroutine(Thud(r));
    }

    void StopAllSpin() { reelsCenterHeld = false; }


    // ───────── PM 10-14 숫자·화면 연출(레퍼런스 8기종 프레임 분석) ─────────
    /// <summary>「번쩍임 줄이기」 — 켜면 경고 스트로브를 끄고 섬광 α를 절반으로(PlayerPrefs, 오버레이 왼쪽 아래 작은 토글).</summary>
    public static bool ReduceFlash
    {
        get { try { return PlayerPrefs.GetInt("pachinko_reduce_flash", 0) == 1; } catch { return false; } }
        set { try { PlayerPrefs.SetInt("pachinko_reduce_flash", value ? 1 : 0); } catch { } }
    }
    bool winFlashActive;

    /// <summary>정지 타격: 0.08초 아래로 6% 눌렸다 튀어 오름 + 그 숫자만 0.05초 흰 섬광 + 2px 「탁」 흔들림. 7이면 금 빛 쓸기 + 반짝.</summary>
    IEnumerator Thud(Reel r)
    {
        int idx = Array.IndexOf(reels, r);
        r.hit.sprite = r.CenterSprite;
        bool seven = Reel.SymbolAt(Mathf.RoundToInt(r.scroll)) == 7;
        if (seven) { StartCoroutine(Sweep(r, new Color(1f, 0.88f, 0.4f, 0.9f), 0.45f, 0.1f)); StartCoroutine(SevenGlints(r)); }
        float t = 0f; const float total = 0.2f;
        int frame = 0;
        while (t < total)
        {
            t += Dt; frame++;
            float sy;
            if (t < 0.08f) sy = Mathf.Lerp(1f, 0.94f, t / 0.08f);                  // 눌림
            else if (t < 0.14f) sy = Mathf.Lerp(0.94f, 1.04f, (t - 0.08f) / 0.06f); // 튀어 오름
            else sy = Mathf.Lerp(1.04f, 1f, (t - 0.14f) / 0.06f);
            r.punch = new Vector2(1f + (1f - sy) * 0.6f, sy);
            r.hit.color = new Color(1f, 1f, 1f, t < 0.05f ? 0.9f * (1f - t / 0.05f) : 0f);
            r.shakeX = t < 0.1f ? (frame % 2 == 0 ? 2f : -2f) : 0f;
            if (idx == 1) r.liftT = Mathf.Max(0f, 1f - t / 0.1f);
            yield return null;
        }
        r.punch = Vector2.one; r.shakeX = 0f; r.liftT = 0f; r.hit.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>대각 빛 쓸기: 숫자 모양으로만 잘린 밝은 띠가 왼 → 오른쪽으로 지나간다.</summary>
    IEnumerator Sweep(Reel r, Color c, float seconds, float delay = 0f)
    {
        if (delay > 0f) yield return Wait(delay);
        RectTransform m = Rt("sweepMask", r.Window);
        m.anchorMin = m.anchorMax = new Vector2(0.5f, 0.5f);
        m.sizeDelta = new Vector2(NumberSize, NumberSize); m.anchoredPosition = Vector2.zero;
        var mi = m.gameObject.AddComponent<Image>(); mi.sprite = r.CenterSprite; mi.raycastTarget = false;
        var mk = m.gameObject.AddComponent<Mask>(); mk.showMaskGraphic = false;
        RectTransform bar = Rt("bar", m);
        var bi = bar.gameObject.AddComponent<Image>(); bi.color = c; bi.raycastTarget = false;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.sizeDelta = new Vector2(130f, NumberSize * 1.7f);
        bar.localRotation = Quaternion.Euler(0, 0, 20f);
        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            bar.anchoredPosition = new Vector2(Mathf.Lerp(-NumberSize * 0.75f, NumberSize * 0.75f, t / seconds), 0f);
            yield return null;
        }
        Destroy(m.gameObject);
    }

    /// <summary>7: 별/광채 반짝 6개(위로 흩어졌다 사라짐).</summary>
    IEnumerator SevenGlints(Reel r)
    {
        RectTransform parent = (RectTransform)r.Window.parent;
        var items = new List<(RectTransform rt, Image im, Vector2 v, float life)>();
        float sc = r.Window.localScale.x;
        for (int i = 0; i < 7; i++)
        {
            RectTransform g = Rt("glint", parent);
            var im = g.gameObject.AddComponent<Image>(); im.raycastTarget = false;
            im.sprite = FxSpr("spark_" + UnityEngine.Random.Range(1, 9)); if (im.sprite == null) { Destroy(g.gameObject); continue; }
            im.preserveAspect = true; im.color = new Color(1f, 0.9f, 0.5f, 1f);
            g.anchorMin = g.anchorMax = r.Window.anchorMin;
            g.pivot = new Vector2(0.5f, 0.5f);
            float s = UnityEngine.Random.Range(70f, 140f) * Mathf.Max(0.5f, sc);
            g.sizeDelta = new Vector2(s, s);
            g.anchoredPosition = r.Window.anchoredPosition + new Vector2(UnityEngine.Random.Range(-220f, 220f), UnityEngine.Random.Range(-60f, 260f)) * sc;
            items.Add((g, im, new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(40f, 140f)), UnityEngine.Random.Range(0.4f, 0.7f)));
        }
        float t = 0f;
        while (items.Count > 0)
        {
            t += Dt;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float u = t / it.life;
                if (u >= 1f) { Destroy(it.rt.gameObject); items.RemoveAt(i); continue; }
                it.rt.anchoredPosition += it.v * Dt;
                it.rt.localRotation = Quaternion.Euler(0, 0, u * 60f);
                it.rt.localScale = Vector3.one * (u < 0.3f ? u / 0.3f : 1f - (u - 0.3f) * 0.5f);
                it.im.color = new Color(1f, 0.9f, 0.5f, 1f - u);
            }
            yield return null;
        }
    }

    /// <summary>리치 강조(에바 216.5s): 양쪽 숫자가 커지며 잔상 → 0.6초 뒤 원래 크기 + 빛 쓸기. 리치 변형(_pink)으로 바꾼다.</summary>
    IEnumerator ReachEmphasis()
    {
        var ghosts = new List<(Image im, int side, int j)>();
        foreach (int side in new[] { 0, 2 })
        {
            Reel r = reels[side];
            for (int j = 1; j <= 3; j++)
            {
                Image g = Img("ghost", r.Window, r.CenterSprite, new Color(1f, 1f, 1f, 0.32f / j));
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(NumberSize, NumberSize);
                g.rectTransform.SetAsFirstSibling();
                ghosts.Add((g, side, j));
            }
            r.pink = true;
        }
        float t = 0f; const float total = 0.6f;
        StartCoroutine(Sweep(reels[0], new Color(1f, 1f, 1f, 0.85f), 0.4f, 0.25f));
        StartCoroutine(Sweep(reels[2], new Color(1f, 1f, 1f, 0.85f), 0.4f, 0.25f));
        while (t < total)
        {
            t += Dt;
            float u = t / total;
            float s = u < 0.3f ? 1f + 0.3f * (1f - Mathf.Pow(1f - u / 0.3f, 3f)) : 1.3f - 0.3f * Mathf.SmoothStep(0f, 1f, (u - 0.3f) / 0.7f);
            reels[0].emphasis = reels[2].emphasis = s;
            foreach (var g in ghosts)
            {
                float dir = g.side == 0 ? -1f : 1f;
                g.im.rectTransform.anchoredPosition = new Vector2(dir * 38f * g.j * (1f - u), -16f * g.j * (1f - u));
                g.im.color = new Color(1f, 1f, 1f, 0.32f / g.j * (1f - u));
            }
            yield return null;
        }
        reels[0].emphasis = reels[2].emphasis = 1f;
        foreach (var g in ghosts) if (g.im != null) Destroy(g.im.gameObject);
    }

    /// <summary>경고 스트로브(大当たり·금 컷인 직전): 0.3초 흑/백 교대 → 0.1초 검정. 「번쩍임 줄이기」면 건너뛴다.</summary>
    IEnumerator WarnStrobe()
    {
        if (ReduceFlash) yield break;
        winFlashActive = true;
        float t = 0f; bool white = false;
        while (t < 0.3f)
        {
            t += Dt; white = !white;
            flash.color = white ? new Color(1f, 1f, 1f, 0.8f) : new Color(0f, 0f, 0f, 0.9f);
            yield return null;
        }
        flash.color = new Color(0f, 0f, 0f, 1f);
        yield return Wait(0.1f);
        winFlashActive = false;
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>당첨 섬광 표준(가로·도쿄구울·심포기어 프레임 측정): 검정 1프레임 → 흰색 0.2초 → 0.5초에 흰→노랑→금으로 사라짐(파랑 채널 먼저).</summary>
    IEnumerator WinFlashStandard()
    {
        winFlashActive = true;
        float a = ReduceFlash ? 0.5f : 1f;
        flash.color = new Color(0f, 0f, 0f, a);
        yield return null;
        flash.color = new Color(1f, 1f, 1f, a);
        yield return Wait(0.2f);
        float t = 0f; const float fade = 0.5f;
        while (t < fade)
        {
            t += Dt;
            float p = Mathf.Clamp01(t / fade);
            flash.color = new Color(1f, 1f - 0.2f * p, Mathf.Clamp01(1f - 2.2f * p), a * (1f - p * p));
            yield return null;
        }
        winFlashActive = false;
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>당첨 후 결과 시간 동안 세 숫자에 빛 쓸기를 주기적으로(공백 메우기).</summary>
    IEnumerator WinGlints(float seconds)
    {
        float end = Now + seconds, next = Now + 0.9f;
        while (Now < end)
        {
            if (Now >= next)
            {
                next += 1.1f;
                for (int i = 0; i < 3; i++) StartCoroutine(Sweep(reels[i], new Color(1f, 0.92f, 0.55f, 0.8f), 0.45f, i * 0.12f));
            }
            yield return null;
        }
    }

    IEnumerator Later(float seconds, IEnumerator e) { yield return Wait(seconds); yield return e; }

    // 복귀 페이드: 0.25초 어두워짐 → 오버레이 닫힘(정지 해제) → 0.25초 게임 화면 밝아짐
    Image returnFade;
    IEnumerator ReturnFade()
    {
        if (returnFade == null)
        {
            returnFade = Img("returnFade", root.parent, null, Color.black);
            Stretch(returnFade.rectTransform);
            returnFade.transform.SetAsLastSibling();
        }
        returnFade.gameObject.SetActive(true);
        float t = 0f;
        while (t < 0.25f) { t += Dt; returnFade.color = new Color(0f, 0f, 0f, t / 0.25f); yield return null; }
        returnFade.color = Color.black;
    }
    IEnumerator ReturnFadeOut()
    {
        float t = 0f;
        while (t < 0.25f) { t += Dt; returnFade.color = new Color(0f, 0f, 0f, 1f - t / 0.25f); yield return null; }
        returnFade.gameObject.SetActive(false);
    }

    // 회전 집중선(가로 126.6s·105.4s): 노랑·검정 날카로운 방사선이 숫자 뒤에서 천천히 돈다
    class RaysGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int n = 36; float R = 1600f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2f, a1 = a0 + Mathf.PI * 2f / n * 0.5f;
                Color c = i % 2 == 0 ? new Color(1f, 0.85f, 0.2f, 0.26f) : new Color(0f, 0f, 0f, 0.34f);
                int b = vh.currentVertCount;
                vh.AddVert(Vector3.zero, c, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * R, c, Vector2.zero);
                vh.AddVert(new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * R, c, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
            }
        }
    }
    RectTransform sharpRays;
    void SharpRays(bool on)
    {
        if (sharpRays == null)
        {
            sharpRays = Rt("sharpRays", stage);
            var g = sharpRays.gameObject.AddComponent<RaysGraphic>(); g.raycastTarget = false;
            sharpRays.anchorMin = sharpRays.anchorMax = new Vector2(0f, 1f);
            sharpRays.anchoredPosition = new Vector2(960f, -540f);
            sharpRays.sizeDelta = Vector2.zero;
            sharpRays.SetSiblingIndex(Mathf.Max(0, machineRect.GetSiblingIndex()));
        }
        sharpRays.gameObject.SetActive(on);
    }

    // SP 리치 레터박스: 위아래 검은 띠가 0.3초에 화면 12%씩 들어오고 제목 카드
    RectTransform lbTop, lbBottom; TMP_Text lbTitle;
    IEnumerator Letterbox(string title, string sub, bool on)
    {
        if (lbTop == null)
        {
            Image a = Img("lbTop", stage, null, Color.black), b = Img("lbBottom", stage, null, Color.black);
            lbTop = a.rectTransform; lbBottom = b.rectTransform;
            lbTop.anchorMin = new Vector2(0, 1); lbTop.anchorMax = new Vector2(1, 1); lbTop.pivot = new Vector2(0.5f, 1f);
            lbBottom.anchorMin = new Vector2(0, 0); lbBottom.anchorMax = new Vector2(1, 0); lbBottom.pivot = new Vector2(0.5f, 0f);
            lbTitle = Label("lbTitle", lbBottom, "", 64f, Color.white, TextAlignmentOptions.Center, 0.2f);
            Stretch(lbTitle.rectTransform);
        }
        lbTop.gameObject.SetActive(true); lbBottom.gameObject.SetActive(true);
        lbTop.SetAsLastSibling(); lbBottom.SetAsLastSibling();
        lbTitle.text = on ? $"{title}  <size=60%><color=#ff8a8a>{sub}</color></size>" : "";
        float h = 1080f * 0.12f, t = 0f;
        while (t < 0.3f)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / 0.3f); u = on ? 1f - Mathf.Pow(1f - u, 3f) : 1f - u;
            lbTop.sizeDelta = new Vector2(0f, h * u); lbBottom.sizeDelta = new Vector2(0f, h * u);
            yield return null;
        }
        if (!on) { lbTop.gameObject.SetActive(false); lbBottom.gameObject.SetActive(false); }
    }

    IEnumerator Pop(Image logo, float seconds, float overshoot, bool keep = false)
    {
        logo.gameObject.SetActive(true);
        StartCoroutine(Ghosts(logo, 0.45f));
        Impact(new Color(1f, 0.85f, 0.85f, 0.55f), 0.3f, true);
        shakeUntil = Mathf.Max(shakeUntil, Now + 0.12f);
        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / 0.14f);
            float s = Mathf.Lerp(Mathf.Min(overshoot, 1.2f), 1f, 1f - Mathf.Pow(1f - u, 3f));   // 120% → 100% 「쾅」
            if (t > 0.14f) s = 1f + 0.03f * Mathf.Exp(-(t - 0.14f) * 8f) * Mathf.Sin((t - 0.14f) * 36f);
            logo.rectTransform.localScale = Vector3.one * s;
            Color c = logo.color; c.a = Mathf.Clamp01(u * 1.5f); logo.color = c;
            yield return null;
        }
        if (!keep) logo.gameObject.SetActive(false);
        else logo.rectTransform.localScale = Vector3.one;
    }

    IEnumerator SpReach(PachinkoBg bg)
    {
        Play(Slot.SpLoop);
        if (SpReachPlayer == null)
        {   // 내용이 아직 없으면 칸을 아예 띄우지 않는다 — 배경 영상·리치 연출이 그대로 이어지게 그 시간만큼 긴장 연출로 채운다
            StartCoroutine(SpeedBurst(0.2f));
            yield return Wait(SpSeconds);
            Stop(Slot.SpLoop);
            yield break;
        }
        spSlotPanel.gameObject.SetActive(true);
        foreach (Transform c in spSlotPanel) if (c != spText.transform) Destroy(c.gameObject);
        bool custom = SpReachPlayer != null;
        spText.gameObject.SetActive(!custom);
        if (custom) { try { SpReachPlayer(spSlotPanel, bg, SpSeconds); } catch (Exception e) { Debug.LogException(e); } }
        yield return Wait(SpSeconds);
        foreach (Transform c in spSlotPanel) if (c != spText.transform) Destroy(c.gameObject);
        Stop(Slot.SpLoop);
        spSlotPanel.gameObject.SetActive(false);
        if (bg == PachinkoBg.Red || bg == PachinkoBg.Gold) { logoGekiatsu.gameObject.SetActive(false); }
    }

    IEnumerator Cutin(PachinkoCut cut)
    {
        Color band = cut == PachinkoCut.Gold ? new Color(0.95f, 0.72f, 0.15f) : cut == PachinkoCut.Red ? new Color(0.82f, 0.08f, 0.2f) : new Color(0.1f, 0.38f, 0.82f);
        cutBand.color = band;
        cutText.text = cut == PachinkoCut.Gold ? "俺が…喰う!!!" : cut == PachinkoCut.Red ? "終わらせる!!" : "いくぞ!";
        cutText.color = cut == PachinkoCut.Gold ? new Color(1f, 0.97f, 0.78f) : Color.white;
        cutRect.gameObject.SetActive(true);
        Tentacles(3); StartCoroutine(BrushSlash(true));
        if (cut == PachinkoCut.Gold && !ReduceFlash) StartCoroutine(WarnStrobe()); else StartCoroutine(ImpactCut(new Color(0.9f, 0.05f, 0.1f)));
        Play(cut == PachinkoCut.Gold ? Slot.CutinGold : Slot.Cutin);
        if (cut == PachinkoCut.Gold) { SetExpect(100); } else if (cut == PachinkoCut.Red) SetExpect(Mathf.Max(expect, 56));
        float t = 0f; const float total = 1.6f;
        while (t < total)
        {
            t += Dt;
            float u = t / total;
            float x = u < 0.2f ? Mathf.Lerp(-2400f, 0f, 1f - Mathf.Pow(1f - u / 0.2f, 3f)) : (u > 0.8f ? Mathf.Lerp(0f, 2400f, Mathf.Pow((u - 0.8f) / 0.2f, 2f)) : 0f);
            cutRect.anchoredPosition = new Vector2(960f - 160f + 1120f - 960f + x, cutRect.anchoredPosition.y);
            yield return null;
        }
        cutRect.gameObject.SetActive(false);
        Place(cutRect, -160, 330, 2240, 340);
    }

    IEnumerator PushWindow()
    {
        Impact(new Color(1f, 0.9f, 0.3f, 0.6f), 0.35f, true);
        pushGroup.gameObject.SetActive(true);
        pushBtnImage.sprite = Spr("UI/push_button_normal");
        pushBtnImage.rectTransform.localScale = Vector3.one;
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        Play(Slot.PushOpen);
        float total = PachinkoSettings.PushSeconds;
        float t0 = Now;
        int lastSec = -1;
        pushFlashing = true;
        while (true)
        {
            float elapsed = Now - t0;
            float left = Mathf.Max(0f, total - elapsed);
            gaugeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left / total), 1f);   // 폭으로 줄인다(스프라이트 없는 Filled는 비율이 안 먹는다)
            gaugeFill.color = left <= 1f ? (Mathf.Repeat(elapsed * 8f, 1f) < 0.5f ? new Color(1f, 0.1f, 0.1f) : new Color(1f, 0.55f, 0.5f)) : new Color(1f, 0.55f, 0.15f);   // 1초 이하 빨강 깜빡
            int sec = Mathf.CeilToInt(left);
            if (sec != lastSec) { lastSec = sec; pushCount.text = sec.ToString(); if (sec > 0) Play(Slot.PushTick); }
            float beat = 1f + 0.08f * Mathf.Sin(elapsed * 28f);
            pushBtnImage.rectTransform.localScale = Vector3.one * beat;
            pushCount.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Abs(Mathf.Sin(elapsed * 6f)));
            ringImage.rectTransform.localScale = Vector3.one * (0.7f + (elapsed % 1f) * 0.9f);
            Color rc = Gold; rc.a = 0.9f * (1f - (elapsed % 1f)); ringImage.color = rc;
            if (pushedWho != int.MinValue) break;   // 컨트롤러가 눌림을 알렸다
            if (standalone && left <= 0f) break;
            if (!standalone && left <= 0f && elapsed > total + 0.3f) break;
            yield return null;
            if (standalone && SpacePressed()) { pushedWho = 0; break; }
        }
        pushFlashing = false;
        pushBtnImage.sprite = Spr("UI/push_button_pressed");
        pushBtnImage.rectTransform.localScale = Vector3.one;
        Play(Slot.PushPress);
        StartCoroutine(Flash(0.3f));
        yield return Wait(0.3f);
    }

    static bool SpacePressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    void OnPushClicked()
    {
        if (standalone) { pushedWho = 0; return; }
        PachinkoController.RequestPush();
    }

    void ShowBand(string text)
    {
        bandText.text = text; bandRect.gameObject.SetActive(true);
        StartCoroutine(BandIn());
    }

    IEnumerator BandIn()
    {
        float t = 0f;
        while (t < 0.5f)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / 0.4f);
            bandRect.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, 1f - Mathf.Pow(1f - u, 3f));
            yield return null;
        }
        while (bandRect.gameObject.activeInHierarchy)
        {
            bandRect.anchoredPosition = new Vector2(bandRect.anchoredPosition.x, -(860f + 75f) + Mathf.Sin(Now * 6f) * 6f);
            yield return null;
        }
    }

    IEnumerator Flash(float seconds)
    {
        yield return FlashColor(new Color(1f, 1f, 1f, 0.5f), 0.06f);
    }

    IEnumerator Fade(float seconds)
    {
        float t = 0f;
        CanvasGroup g = root.GetComponent<CanvasGroup>();
        if (g == null) g = root.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 1f;
        while (t < seconds) { t += Dt; g.alpha = 1f - t / seconds; yield return null; }
        g.alpha = 1f;
    }

    IEnumerator Confetti(float seconds)
    {
        Color[] cols = { RedC, Gold, BlueC, GreenC, new Color(0.6f, 0.3f, 1f), Color.white };
        var live = new List<(Image img, float x, float y, float vy, float rot, float rv, float life)>();
        float t = 0f, spawnAcc = 0f;
        while (t < seconds)
        {
            float dt = Dt; t += dt; spawnAcc += dt * 38f;
            while (spawnAcc >= 1f && t < seconds - 1.5f)
            {
                spawnAcc -= 1f;
                Image img = GetParticle();
                bool coin = false;   // 둥근 금화는 촌스러워 뺐다 — 얇은 색종이 조각만
                img.sprite = coin ? circle : null; img.color = coin ? Gold : cols[UnityEngine.Random.Range(0, cols.Length)];
                float size = coin ? UnityEngine.Random.Range(34f, 50f) : UnityEngine.Random.Range(14f, 26f);
                img.rectTransform.sizeDelta = coin ? new Vector2(size, size) : new Vector2(size * 0.5f, size);
                live.Add((img, UnityEngine.Random.Range(0f, 1920f), -40f, UnityEngine.Random.Range(380f, 820f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-540f, 540f), 0f));
            }
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.y += p.vy * dt; p.rot += p.rv * dt;
                p.img.rectTransform.anchoredPosition = new Vector2(p.x, -p.y);
                p.img.rectTransform.localRotation = Quaternion.Euler(0, 0, p.rot);
                if (p.y > 1160f) { p.img.gameObject.SetActive(false); live.RemoveAt(i); } else live[i] = p;
            }
            yield return null;
        }
        foreach (var p in live) p.img.gameObject.SetActive(false);
    }

    Image GetParticle()
    {
        foreach (Image i in particlePool) if (!i.gameObject.activeSelf) { i.gameObject.SetActive(true); return i; }
        Image img = Img("particle", stage, null, Color.white);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
        particlePool.Add(img);
        return img;
    }

    // ───────── 매 프레임: 배경 색·박동·흔들림·광선 ─────────
    // 시계: 녹화 모드에선 프레임마다 정확히 1/30초(captureFramerate에서도 unscaledDeltaTime은 실시간이라 따로 센다).
    static float clockNow;
    static float Dt => RecordMode ? 1f / 30f : Time.unscaledDeltaTime;
    static float Now => RecordMode ? clockNow : Time.unscaledTime;

    /// <summary>녹화용: 매 프레임(플레이어 루프) 한 번 불린다.</summary>
    public static Action FrameHook;

    void Update()
    {
        if (RecordMode) clockNow += 1f / 30f;
        FrameHook?.Invoke();
        VoteUpdate();
        UpdateMusic();
        ProcessImpacts();
        if (!root.gameObject.activeSelf) return;
        // 무대 크기 맞추기
        Rect r = root.rect;
        if (r.width > 1f && r.height > 1f)
        {
            float s = Mathf.Min(r.width / 1920f, r.height / 1080f);
            stage.localScale = new Vector3(s, s, 1f);
        }
        bgTint = Color.Lerp(bgTint, bgTarget, Dt * 6f);
        float bgA = 1f - videoShow;   // 영상이 깔리면 어두운 바탕을 걷는다
        float pulse = bgPulse >= 1f ? 0.5f + 0.5f * Mathf.Sin(Now * (bgPulse >= 3f ? 18f : 8f)) : 0f;
        Color bgc = Color.Lerp(bgTint, Color.white * 0.2f + bgTint * 0.8f, pulse * 0.4f); bgc.a *= bgA;
        backdrop.color = bgc;
        videoShow = Mathf.MoveTowards(videoShow, videoShowTarget, Dt / (videoShowTarget > videoShow ? 0.25f : BgVideoFade));
        videoView.color = new Color(1f, 1f, 1f, videoShow);
        videoDim.color = new Color(0f, 0f, 0f, 0.15f * videoShow);   // 숫자가 읽힐 만큼만(α 0.2 이하)
        if (kanekiGroup != null) kanekiGroup.alpha = 1f - videoShow;
        if (videoAudio != null) videoAudio.volume = Mathf.Clamp01(AudioPrefs.MusicVolume * (VoiceActive() ? 0.4f : 1f) * Mathf.Clamp01(videoShow * 4f));
        if (videoAudio2 != null) videoAudio2.volume = Mathf.Clamp01(AudioPrefs.MusicVolume * (VoiceActive() ? 0.4f : 1f));
        if (RecordMode && videoPlayer != null && videoPlayer.isPlaying) videoPlayer.externalReferenceTime = Now - videoStartTime;
        GameBgm.Duck = Mathf.MoveTowards(GameBgm.Duck, duckTarget, Dt * 2f);
        if (kanekiRect.gameObject.activeSelf && heartbeat)
        {
            float tb = Now % 0.9f;
            float hb = Mathf.Exp(-tb * 16f) + (tb >= 0.2f ? Mathf.Exp(-(tb - 0.2f) * 16f) * 0.8f : 0f);   // 두-둥
            kanekiPic.localScale = Vector3.one * (1f + 0.08f * hb);
            heartRed.color = new Color(0.8f, 0f, 0.05f, 0.28f * hb);
            if (hb > 0.6f) kanekiPic.anchoredPosition = new Vector2(960f + UnityEngine.Random.Range(-4f, 4f), kanekiPic.anchoredPosition.y);
        }
        if (reels != null && numbersSmallT <= 0f) foreach (Reel rl in reels) if (!rl.spinning) rl.Refresh();   // 불꽃 프레임 순환
        numbersSmallT = Mathf.MoveTowards(numbersSmallT, numbersSmallTarget, Dt * 3f);
        if (reels != null && reels.Length == 3) reels[1].group.alpha = (curReach && numbersSmallT > 0.6f && reels[1].spinning) ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Now * 8f)) : 1f;   // 리치면 미니 릴 가운데 칸 깜빡
        if (sharpRays != null && sharpRays.gameObject.activeSelf) sharpRays.localRotation = Quaternion.Euler(0f, 0f, -Now * 6f);
        if (reels != null) foreach (Reel rl in reels) rl.SetSmall(numbersSmallT);
        if (tickerText != null)
        {
            float x = -(Now * 160f % 2300f);
            tickerText.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
        if (Now < shakeUntil) stage.localPosition = new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-8f, 8f), 0f);
        else if (stage.localPosition != Vector3.zero) stage.localPosition = Vector3.zero;
        // 보류 구슬 박동
        if (holdOrbs != null)
        {
            Image o = holdOrbs[0];
            bool strong = o.color == RedC || o.color == Gold;
            bool soft = o.color == BlueC || o.color == GreenC;
            float sp = strong ? (o.color == Gold ? 26f : 18f) : 8f;
            float amp = strong ? (o.color == Gold ? 0.3f : 0.22f) : (soft ? 0.07f : 0f);
            o.rectTransform.localScale = Vector3.one * (1f + amp * Mathf.Sin(Now * sp));
            if (holdRing != null)
            {
                Color rc = o.color; rc.a = (strong ? 0.55f : soft ? 0.25f : 0f) * (0.5f + 0.5f * Mathf.Sin(Now * sp));
                holdRing.color = rc;
                holdRing.rectTransform.localScale = Vector3.one * (1f + (strong ? 0.5f : 0.2f) * (0.5f + 0.5f * Mathf.Sin(Now * sp)));
            }
        }
        // PUSH 버튼 번쩍
        if (pushFlashing) pushBtnImage.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.7f), Mathf.Abs(Mathf.Sin(Now * 14f)));
        else pushBtnImage.color = Color.white;
    }

    // ───────── 소리 슬롯(사장님 10-14: 기준 영상 e도쿄구울에서 잘라 둔 것을 기본값으로, 슬롯마다 교체 가능 — Sfx[슬롯]=null이면 무음) ─────────
    public enum Slot { Open, Hold, ReelSpin, ReelStop, Reach, Yokoku, SpStage, SpLoop, Cutin, CutinGold, PushOpen, PushTick, PushPress, Revive, Atari, Ooatari, Zannen, PushVoice, WinVoice, MissVoice, Firework }
    /// <summary>슬롯별 소리. 코드에서 PachinkoOverlay.Sfx[(int)Slot.Reach] = clip 으로 바꾼다.</summary>
    public static readonly AudioClip[] Sfx = new AudioClip[Enum.GetValues(typeof(Slot)).Length];
    /// <summary>슬롯별 음량(폰 촬영이라 가게 소음이 섞여 있어 슬롯마다 따로 맞춘다).</summary>
    public static readonly float[] SfxGain = { 1f, 1f, 0.5f, 1f, 0.9f, 1f, 0.8f, 0.5f, 0.8f, 0.8f, 0.8f, 1f, 1f, 1f, 0.9f, 0.9f, 0.9f, 1f, 1f, 1f, 1f };
    static readonly bool[] SfxLoop = { false, false, true, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false , false , false };
    /// <summary>대사가 나오는 동안 다른 효과음을 줄이는 배율.</summary>
    public static float VoiceDuckOthers = 0.3f;
    /// <summary>파칭코 중 게임 배경음을 줄이는 양(−12dB ≈ 0.25). 끝나면 복원.</summary>
    public static float BgmDuck = 0.25f;

    /// <summary>숫자(1~9)별 당첨 대사 — 인덱스 0=111 … 8=999(당첨 확정 순간, 부활도 최종 숫자 기준). 인스펙터 대신 코드에서 바꾼다: PachinkoOverlay.WinLines[3] = clip.</summary>
    public static readonly AudioClip[] WinLines = new AudioClip[9];
    /// <summary>꽝·리치 꽝 대사(D09).</summary>
    public static AudioClip MissLine;
    static bool sfxLoaded;
    static void LoadDefaultSfx()
    {
        if (sfxLoaded) return; sfxLoaded = true;
        for (int i = 0; i < 9; i++) if (WinLines[i] == null) WinLines[i] = Resources.Load<AudioClip>(Folder + $"Sfx/line_win_{i + 1}");
        if (MissLine == null) MissLine = Resources.Load<AudioClip>(Folder + "Sfx/line_miss");
        // 효과음 슬롯(Sfx[])은 사장님 결정으로 전부 비워 둔다 — 나중에 파일만 꽂으면 된다.
    }

    AudioSource lineSource;
    void PlayLine(AudioClip clip)
    {
        LoadDefaultSfx();
        if (clip == null) return;
        if (lineSource == null)
        {
            lineSource = host.AddComponent<AudioSource>();
            lineSource.playOnAwake = false; lineSource.ignoreListenerPause = true; lineSource.spatialBlend = 0f;
        }
        lineSource.clip = clip; lineSource.volume = Mathf.Clamp01(AudioPrefs.SfxVolume);
        lineSource.Play();
        AudioEvent?.Invoke(clip.name, Now, lineSource.volume);
    }

    readonly Dictionary<Slot, AudioSource> slotSources = new Dictionary<Slot, AudioSource>();

    void Play(Slot slot)
    {
        LoadDefaultSfx();
        AudioClip clip = Sfx[(int)slot];
        if (clip == null) return;
        if (!slotSources.TryGetValue(slot, out AudioSource src) || src == null)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false; src.ignoreListenerPause = true; src.spatialBlend = 0f;
            slotSources[slot] = src;
        }
        src.clip = clip; src.loop = SfxLoop[(int)slot];
        float vol = SfxGain[(int)slot] * AudioPrefs.SfxVolume;
        if (VoiceActive()) vol *= VoiceDuckOthers;   // 대사 우선
        src.volume = Mathf.Clamp01(vol);
        src.Play();
    }

    bool VoiceActive() => lineSource != null && lineSource.isPlaying;

    void Stop(Slot slot) { if (slotSources.TryGetValue(slot, out AudioSource src) && src != null) src.Stop(); }
    void StopAllSfx() { foreach (AudioSource src in slotSources.Values) if (src != null) src.Stop(); }
    IEnumerator PlayFor(Slot slot, float seconds) { Play(slot); yield return Wait(seconds); Stop(slot); }
}
