using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 판 들어가기 전 로딩 화면 — 사장님 2026-09-26 「구랜디 시작화면 이걸로 해 주라, 원랜디처럼 시작 전에 로딩 잠깐 할 때 이거 나오게」
/// → 이어서 「방 만들고 시작할 때 3초 정도 빌드할 시간 주고, 이 이미지를 전체로 띄운 다음 밑에 진행률, 가운데 글씨로 구랜디」.
/// 그림은 화면을 **꽉 채운다**(4:3이라 16:9에선 위아래가 조금 잘린다), 가운데 큰 「구랜디」, 맨 아래 진행 막대와 %.
///
/// 언제 뜨나
///  · 게임 씬이 첫 씬인 빌드(지금 main — 혼자 하기만): 앱이 켜질 때 스스로 뜬다(<see cref="AutoShowOnLaunch"/>).
///  · 첫 화면(NetBoot, 혼자 하기/같이 하기)이 있는 빌드: 게임 씬을 부르기 직전에 부르는 쪽이 <see cref="Show"/>를 부른다.
/// 언제 걷히나 — 게임 씬(RoundManager가 있는 씬)이 올라오고 몇 프레임 지난 뒤, 최소 <see cref="MinVisibleSeconds"/>초는 보인 다음 페이드.
/// 씬 로드는 동기라 진짜 진행률을 못 받는다 — 막대는 기다리는 동안 90%까지 차오르다 준비되면 끝까지 찬다(워크3 막대와 같은 느낌).
///
/// ⚠️ 에디터에서는 스스로 안 뜬다 — gameshot 판이 첫 몇 초를 찍거나 누르는데, 덮개가 그걸 가린다. 에디터 확인은 call:LoadingScreen.Preview.
/// 그림: Resources/UI/LoadingScreen.jpg (원본 IMG_5302.HEIC, 5712×4284 → 2048×1536).
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    const string ImagePath = "UI/LoadingScreen";
    const float MinVisibleSeconds = 3f;   // 사장님 「3초 정도」
    const float FadeSeconds = 0.4f;
    const int SettleFrames = 3;          // 게임 씬 첫 프레임들의 몰아치는 초기화(조합표 인형 668기 등)를 덮개 뒤로
    const float ImageAspect = 4f / 3f;

    static LoadingScreen instance;

    CanvasGroup group;
    RectTransform barFill;
    TMP_Text caption;
    TMP_Text percent;
    float shownAt;
    float fakeProgress;
    int readyFrames;
    float fadeStartedAt = -1f;

    public static bool IsShowing => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoShowOnLaunch()
    {
        if (Application.isEditor) return;
        // 첫 씬이 게임 씬일 때만 — 첫 화면(NetBoot)이 먼저면 그 화면을 가리지 않고, 게임 씬을 부를 때 Show를 부른다.
        if (SceneUtility.GetScenePathByBuildIndex(0).EndsWith("/SampleScene.unity")) Show();
    }

    /// <summary>로딩 화면을 띄운다(이미 떠 있으면 그대로). 게임 씬이 준비되면 스스로 걷힌다.</summary>
    public static void Show()
    {
        if (instance != null) return;
        GameObject host = new GameObject("LoadingScreen");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<LoadingScreen>();
        instance.Build();
    }

    /// <summary>에디터 확인용 — 판 도중에 띄워 두고 최소 시간 뒤 걷힌다(gameshot call:LoadingScreen.Preview).</summary>
    public static string Preview()
    {
        Show();
        return "로딩 화면을 띄웠다";
    }

    void Build()
    {
        shownAt = Time.realtimeSinceStartup;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 모든 HUD 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();   // 뜬 동안 뒤 UI가 눌리지 않게
        group = gameObject.AddComponent<CanvasGroup>();

        Image black = NewChild<Image>("검정", transform);
        Stretch(black.rectTransform);
        black.color = Color.black;

        RawImage picture = NewChild<RawImage>("그림", transform);
        Stretch(picture.rectTransform);
        picture.texture = Resources.Load<Texture2D>(ImagePath);
        picture.raycastTarget = false;
        AspectRatioFitter fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // 전체 화면(남는 쪽은 잘림)
        fitter.aspectRatio = picture.texture != null ? (float)picture.texture.width / picture.texture.height : ImageAspect;
        if (picture.texture == null) Debug.LogWarning($"LoadingScreen: Resources/{ImagePath} 그림이 없어 검정 화면만 띄웁니다.");

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");

        // 가운데 큰 「구랜디」(사장님 「가운데 글씨로 구랜디」). 그림 위라 굵은 외곽선.
        TMP_Text title = NewLabel("제목", transform, font, 150f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(1200f, 220f);
        title.text = "구랜디";
        title.outlineWidth = 0.3f;

        // 진행 막대 — 화면 맨 아래 가운데(사장님 「밑에 진행률」). 캔버스 기준(1920×1080)이라 창 크기가 바뀌어도 같은 비율.
        Image frame = NewChild<Image>("막대 테두리", transform);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = new Vector2(0.15f, 0f);
        frameRect.anchorMax = new Vector2(0.85f, 0f);
        frameRect.pivot = new Vector2(0.5f, 0f);
        frameRect.anchoredPosition = new Vector2(0f, 48f);
        frameRect.sizeDelta = new Vector2(0f, 28f);
        frame.color = new Color(0f, 0f, 0f, 0.75f);

        Image fill = NewChild<Image>("막대", frameRect);
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = new Vector2(3f, 3f);
        barFill.offsetMax = new Vector2(-3f, -3f);
        fill.color = new Color(0.95f, 0.78f, 0.25f);   // 금색

        caption = NewLabel("글자", transform, font, 26f);
        RectTransform captionRect = caption.rectTransform;
        captionRect.anchorMin = new Vector2(0.15f, 0f);
        captionRect.anchorMax = new Vector2(0.85f, 0f);
        captionRect.pivot = new Vector2(0.5f, 0f);
        captionRect.anchoredPosition = new Vector2(0f, 82f);
        captionRect.sizeDelta = new Vector2(0f, 40f);
        caption.alignment = TextAlignmentOptions.Left;
        caption.text = "불러오는 중…";

        percent = NewLabel("퍼센트", transform, font, 26f);
        RectTransform percentRect = percent.rectTransform;
        percentRect.anchorMin = captionRect.anchorMin;
        percentRect.anchorMax = captionRect.anchorMax;
        percentRect.pivot = captionRect.pivot;
        percentRect.anchoredPosition = captionRect.anchoredPosition;
        percentRect.sizeDelta = captionRect.sizeDelta;
        percent.alignment = TextAlignmentOptions.Right;
        percent.text = "0%";
    }

    static TMP_Text NewLabel(string name, Transform parent, TMP_FontAsset font, float size)
    {
        TMP_Text label = NewChild<TextMeshProUGUI>(name, parent);
        if (font != null) label.font = font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 230);
        label.raycastTarget = false;
        return label;
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        bool gameReady = SceneManager.GetActiveScene().isLoaded && FindFirstObjectByType<RoundManager>() != null;
        readyFrames = gameReady ? readyFrames + 1 : 0;
        bool done = readyFrames >= SettleFrames && now - shownAt >= MinVisibleSeconds;

        // 기다리는 동안은 90%까지 느리게, 준비되면 끝까지.
        float target = done ? 1f : 0.9f;
        fakeProgress = Mathf.MoveTowards(fakeProgress, target, (done ? 2f : 0.3f) * Time.unscaledDeltaTime);
        if (barFill != null) barFill.anchorMax = new Vector2(fakeProgress, 1f);
        if (percent != null) percent.text = $"{Mathf.RoundToInt(fakeProgress * 100f)}%";

        if (!done || fakeProgress < 1f) return;
        if (fadeStartedAt < 0f)
        {
            fadeStartedAt = now;
            if (caption != null) caption.text = "준비 완료";
        }
        float t = (now - fadeStartedAt) / FadeSeconds;
        if (group != null) group.alpha = 1f - Mathf.Clamp01(t);
        if (t >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    static T NewChild<T>(string name, Transform parent) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
