using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 문 전환(사장님 10-06 「혼자 하기·방 만들기·시작 때 문이 열리듯, 되돌아올 때는 반대로 닫히게」).
/// 닫힘(쿵) → 할 일(씬 전환·방 만들기) → 준비되면 열림 순서. 효과음은 Kenney CC0(Resources/Sfx/door).
/// 방식은 MenuTheme.DoorStyle(상수 한 줄)로 고른다:
///  · Classic — 선술집 쌍여닫이 문(Resources/UI/Door). 가속하며 닫혀 쿵, 경첩 축으로 안쪽으로 젖혀지며 열린다(종전 그대로).
///  · Doors — 성 테마 쇠문 쌍여닫이(Resources/UI/DoorCastle/door_left·right): 묵직한 이징(느리게 시작→쿵)·닫힐 때 살짝 튕김·화면 흔들림·먼지·
///    열릴 때 틈 빛 번쩍 + 게임 화면 페이드인.
///  · Portcullis — 성 테마 성문(gate_frame·gate_grate): 돌 아치 틀 뒤로 쇠창살이 내려와 쿵, 열릴 땐 창살이 올라가고 틀이 다가오며 사라진다.
/// 씬을 넘어 산다(DontDestroyOnLoad). 로딩 화면(정렬 32000)이 문(31000) 위에 뜨므로, 로딩이 걷힌 뒤에 닫힌 문이 보이고 그때 열린다.
/// 타이밍 API(CloseThen·SlamForSceneChange·after 콜백)는 방식과 무관하게 같다.
/// </summary>
public class DoorTransition : MonoBehaviour
{
    const float HoldAfterReady = 0.2f, NoSceneWait = 0.35f;
    const int SortingOrder = 31000;
    const float GateImageW = 1920f, GateImageH = 1080f, GatePivotY = 0.352f;   // 아치 열린 곳 가운데(그림 y≈700) — 다가오는 확대의 중심
    const float GrateH = 1040f, GrateTravel = 1120f;

    static MenuTheme.DoorKind Kind => MenuTheme.DoorStyle;
    static float CloseSeconds => Kind == MenuTheme.DoorKind.Classic ? 0.45f : Kind == MenuTheme.DoorKind.Doors ? 0.55f : 0.65f;

    /// <summary>촬영 전용(에디터 DoorPreview): 1보다 작으면 전환 전체가 그만큼 느려져 gameshot snap 간격으로 연속 프레임을 뽑을 수 있다. 게임에선 늘 1.</summary>
    public static float PreviewTimeScale = 1f;

    static DoorTransition active;
    public static bool Busy => active != null;

    MenuTheme.DoorKind style;
    RectTransform left, right;
    RawImage leftImage, rightImage, gap;
    Image block, flash;
    RectTransform gateRoot, zoom, grate;
    RawImage frameImage, grateImage;
    RectTransform dustRoot;
    AudioSource audioSource;
    enum Phase { Closing, Waiting, Opening }
    Phase phase;
    float t;
    Action after;
    bool expectScene, sceneArrived;
    float readyAt = -1f;
    bool impacted, openStarted;
    float shakeT = 99f, shakeAmp;

    float OpenSeconds => style == MenuTheme.DoorKind.Classic ? 0.95f : style == MenuTheme.DoorKind.Doors ? 0.8f : 1.0f;
    float ImpactAt => style == MenuTheme.DoorKind.Doors ? 0.78f : 0.75f;

    /// <summary>문을 닫고 다 닫히면 after를 부른다. expectScene이면 다음 씬이 올라오고(로딩 화면까지 걷힌 뒤) 열린다, 아니면 잠깐 뒤 열린다.</summary>
    public static void CloseThen(Action after, bool expectScene)
    {
        if (active != null) { after?.Invoke(); return; }   // 이미 전환 중이면 문은 그대로 두고 일만
        active = Create();
        active.after = after;
        active.expectScene = expectScene;
        active.Begin(CloseSeconds);
    }

    /// <summary>바깥(네트 씬 전환 등)이 이미 씬을 바꾸는 중일 때 — 문을 빨리 닫아 두고 씬이 오면 연다.</summary>
    public static void SlamForSceneChange()
    {
        if (active != null) { active.expectScene = true; return; }
        active = Create();
        active.expectScene = true;
        active.Begin(0.18f);
    }

    static DoorTransition Create()
    {
        var go = new GameObject("[DoorTransition]");
        DontDestroyOnLoad(go);
        return go.AddComponent<DoorTransition>();
    }

    float closeDuration;
    void Begin(float duration)
    {
        closeDuration = duration;
        phase = Phase.Closing;
        t = 0f;
        if (style == MenuTheme.DoorKind.Classic) Play("Sfx/door/close_doorClose_1", 0.55f);
        else Play("Sfx/door/open_creak2", 0.5f);   // 묵직한 쇠문이 움직이기 시작하는 끼익
    }

    void Awake()
    {
        style = Kind;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // 세로 기준 — 두 짝이 늘 화면 높이를 다 덮는다
        gameObject.AddComponent<GraphicRaycaster>();   // 전환 중 클릭 막기

        var blockGo = new GameObject("Block", typeof(RectTransform), typeof(Image));
        blockGo.transform.SetParent(transform, false);
        var br = (RectTransform)blockGo.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        block = blockGo.GetComponent<Image>();
        block.color = new Color(0f, 0f, 0f, 0f);

        if (style == MenuTheme.DoorKind.Portcullis && !BuildGate()) style = MenuTheme.DoorKind.Doors;   // 성문 그림이 없으면 쇠문으로
        if (style != MenuTheme.DoorKind.Portcullis)
        {
            leftImage = Leaf("Left", "door_left", new Vector2(0f, 0.5f), out left);
            rightImage = Leaf("Right", "door_right", new Vector2(1f, 0.5f), out right);

            var g = new GameObject("GapLight", typeof(RectTransform), typeof(RawImage));
            g.transform.SetParent(transform, false);
            var gr = (RectTransform)g.transform; gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
            gap = g.GetComponent<RawImage>();
            gap.texture = MenuTheme.DoorTexture("door_gap_light");
            gap.raycastTarget = false;
            gap.color = new Color(MenuTheme.DoorGapLight.r, MenuTheme.DoorGapLight.g, MenuTheme.DoorGapLight.b, 0f);
        }

        if (style != MenuTheme.DoorKind.Classic)
        {
            // 열릴 때 번쩍이는 빛(문 틈/창살 사이로 쏟아지는 차가운 빛) — 전체 화면
            var fl = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            fl.transform.SetParent(transform, false);
            var fr = (RectTransform)fl.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            flash = fl.GetComponent<Image>();
            flash.raycastTarget = false;
            flash.color = new Color(MenuTheme.DoorGapLight.r, MenuTheme.DoorGapLight.g, MenuTheme.DoorGapLight.b, 0f);

            var dr = new GameObject("Dust", typeof(RectTransform));
            dr.transform.SetParent(transform, false);
            dustRoot = (RectTransform)dr.transform; dustRoot.anchorMin = Vector2.zero; dustRoot.anchorMax = Vector2.one; dustRoot.offsetMin = dustRoot.offsetMax = Vector2.zero;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (style == MenuTheme.DoorKind.Portcullis) SetGate(GrateTravel, 1.5f, 0f, 0f);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (active == this) active = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneArrived = true;

    // 성문: 가운데 기준 1920×1080 판(화면이 더 넓으면 키운다) 안에 [확대 뿌리 → 쇠창살(뒤) · 돌 아치 틀(앞)].
    bool BuildGate()
    {
        Texture2D frameTex = MenuTheme.DoorTexture("gate_frame"), grateTex = MenuTheme.DoorTexture("gate_grate");
        if (frameTex == null || grateTex == null) return false;
        var root = new GameObject("Gate", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        gateRoot = (RectTransform)root.transform;
        gateRoot.anchorMin = gateRoot.anchorMax = new Vector2(0.5f, 0.5f);
        gateRoot.sizeDelta = new Vector2(GateImageW, GateImageH);

        var z = new GameObject("Zoom", typeof(RectTransform));
        z.transform.SetParent(gateRoot, false);
        zoom = (RectTransform)z.transform;
        zoom.anchorMin = zoom.anchorMax = new Vector2(0.5f, 0.5f);
        zoom.sizeDelta = new Vector2(GateImageW, GateImageH);
        zoom.pivot = new Vector2(0.5f, GatePivotY);
        zoom.anchoredPosition = new Vector2(0f, (0.5f - GatePivotY) * GateImageH);   // 그림은 제자리, 확대 중심만 아치 가운데로

        var gr = new GameObject("Grate", typeof(RectTransform), typeof(RawImage));
        gr.transform.SetParent(zoom, false);
        grate = (RectTransform)gr.transform;
        grate.anchorMin = grate.anchorMax = new Vector2(0.5f, 1f);
        grate.pivot = new Vector2(0.5f, 1f);
        grate.sizeDelta = new Vector2(800f, GrateH);
        grateImage = gr.GetComponent<RawImage>();
        grateImage.texture = grateTex;
        grateImage.raycastTarget = false;

        var fr = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
        fr.transform.SetParent(zoom, false);
        var frr = (RectTransform)fr.transform; frr.anchorMin = Vector2.zero; frr.anchorMax = Vector2.one; frr.offsetMin = frr.offsetMax = Vector2.zero;
        frameImage = fr.GetComponent<RawImage>();
        frameImage.texture = frameTex;
        frameImage.raycastTarget = false;
        return true;
    }

    // gy: 창살이 올라간 높이(0 = 닫힘), zs: 확대, fa: 틀 불투명도, ba: 뒤 어둠 불투명도
    void SetGate(float gy, float zs, float fa, float ba)
    {
        grate.anchoredPosition = new Vector2(0f, gy);
        zoom.localScale = new Vector3(zs, zs, 1f);
        frameImage.color = new Color(1f, 1f, 1f, fa);
        grateImage.color = new Color(1f, 1f, 1f, fa);
        block.color = new Color(0.04f, 0.045f, 0.06f, ba);
    }

    // 한 짝: 화면 절반(바깥 끝이 경첩). 그림 가로세로비(1280×1440 ≈ 0.889)로 반 화면(960×1080)을 덮게 높이 기준으로 맞춘다.
    RawImage Leaf(string name, string path, Vector2 hinge, out RectTransform rect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = hinge;
        rect.pivot = hinge;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 1080f);
        var img = go.GetComponent<RawImage>();
        img.texture = MenuTheme.DoorTexture(path);
        img.raycastTarget = false;
        return img;
    }

    // ───────────── 이징 ─────────────
    static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
    static float EaseIn(float k, float p) => Mathf.Pow(Mathf.Clamp01(k), p);
    static float EaseOutCubic(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    static float EaseInOutCubic(float k) { k = Mathf.Clamp01(k); return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; }
    static float Bounce(float u) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) * (1f - Mathf.Clamp01(u));   // 0→살짝 튕겨 올랐다→0

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // 씬 로드 중 긴 프레임에 애니가 건너뛰지 않게
        dt *= PreviewTimeScale;
        t += dt;
        // 화면 가로 절반(캔버스 단위) — 세로 1080 기준이라 넓은 화면이면 절반이 960보다 크다
        float canvasW = ((RectTransform)transform).rect.width;
        float half = canvasW * 0.5f + 2f;
        if (gateRoot != null) { float s = Mathf.Max(1f, canvasW / GateImageW); gateRoot.localScale = new Vector3(s, s, 1f); }

        switch (phase)
        {
            case Phase.Closing:
            {
                float k = Mathf.Clamp01(t / closeDuration);
                if (style == MenuTheme.DoorKind.Classic)
                {
                    float e = k * k;                                    // 가속하며 닫힌다(쾅)
                    SetOpenAmount(1f - e, half);
                    if (k >= 1f)
                    {
                        Play("Sfx/door/close_thud_impactWood_heavy_001", 0.6f);
                        FinishClosing();
                    }
                    break;
                }
                float ia = ImpactAt;
                if (style == MenuTheme.DoorKind.Doors)
                {
                    float open = k < ia ? 1f - EaseIn(k / ia, 2.2f)         // 느리게 시작해 갈수록 빨라진다(쇠문의 무게)
                                        : 0.055f * Bounce((k - ia) / (1f - ia));   // 맞닿은 뒤 살짝 튕겨 열렸다 닫힌다
                    SetOpenAmount(open, half);
                }
                else
                {
                    float gy = k < 0.2f ? GrateTravel : k < ia ? GrateTravel * (1f - EaseIn(Seg(k, 0.2f, ia), 2f)) : 34f * Bounce((k - ia) / (1f - ia));
                    float z = Mathf.Lerp(1.5f, 1f, EaseOutCubic(Seg(k, 0f, 0.5f)));
                    SetGate(gy, z, Seg(k, 0f, 0.3f), Seg(k, 0f, 0.35f));
                }
                if (k >= ia && !impacted) Impact();
                if (k >= 1f) FinishClosing();
                break;
            }
            case Phase.Waiting:
            {
                if (style == MenuTheme.DoorKind.Portcullis) SetGate(0f, 1f, 1f, 1f); else SetOpenAmount(0f, half);
                bool ready = expectScene ? sceneArrived && !LoadingScreen.IsShowing : t >= NoSceneWait;
                if (ready && readyAt < 0f) readyAt = t;
                if (readyAt >= 0f && t - readyAt >= HoldAfterReady)
                {
                    phase = Phase.Opening;
                    t = 0f;
                    if (style == MenuTheme.DoorKind.Classic) Play("Sfx/door/open_doorOpen_1", 0.6f);
                    else { Play("Sfx/door/metalLatch", 0.6f); Play("Sfx/door/open_creak2", 0.55f); }   // 걸쇠 풀리는 소리 + 끼익
                }
                if (t > 30f) Destroy(gameObject);   // 안전장치 — 무엇이 막혀도 문이 영영 남지 않게
                break;
            }
            case Phase.Opening:
            {
                float k = Mathf.Clamp01(t / OpenSeconds);
                if (style == MenuTheme.DoorKind.Classic)
                {
                    float e = EaseOutCubic(k);   // 확 열리다 천천히 멈춤
                    SetOpenAmount(e, half);
                    gap.color = new Color(MenuTheme.DoorGapLight.r, MenuTheme.DoorGapLight.g, MenuTheme.DoorGapLight.b, Mathf.Sin(Mathf.Clamp01(k * 2.2f) * Mathf.PI) * 0.9f);   // 틈이 벌어질 때 불빛
                }
                else if (style == MenuTheme.DoorKind.Doors)
                {
                    float rattle = k < 0.12f ? 0.012f * Mathf.Sin(k / 0.12f * Mathf.PI * 3f) : 0f;   // 걸쇠가 풀리며 덜컹
                    float e = k < 0.12f ? Mathf.Max(0f, rattle) : Mathf.Lerp(0.0f, 1f, EaseInOutCubic(Seg(k, 0.1f, 1f)));   // 묵직하게 시작 → 가속 → 천천히 멈춤
                    SetOpenAmount(e, half);
                    float pulse = Mathf.Sin(Seg(k, 0.08f, 0.5f) * Mathf.PI);   // 틈에서 번쩍
                    gap.color = new Color(MenuTheme.DoorGapLight.r, MenuTheme.DoorGapLight.g, MenuTheme.DoorGapLight.b, Mathf.Clamp01(pulse * 1.1f));
                    gap.rectTransform.localScale = new Vector3(1f + 2.2f * Seg(k, 0.08f, 0.5f), 1f, 1f);   // 열릴수록 빛이 옆으로 퍼진다
                    SetFlash(0.32f * pulse);
                    block.color = new Color(0f, 0f, 0f, 0.5f * (1f - EaseOutCubic(k)));            // 게임 화면이 어둠에서 서서히 밝아진다
                }
                else
                {
                    float rattle = k < 0.1f ? 7f * Mathf.Sin(k / 0.1f * Mathf.PI * 4f) : 0f;       // 사슬이 팽팽해지며 덜컹
                    float gy = k < 0.1f ? Mathf.Max(0f, rattle) : GrateTravel * EaseIn(Seg(k, 0.1f, 0.62f), 2.2f);   // 천천히 떨며 올라가다 빨라진다
                    float zs = Mathf.Lerp(1f, 1.7f, EaseIn(Seg(k, 0.45f, 1f), 2f));
                    float fa = 1f - Seg(k, 0.55f, 1f);
                    float ba = 1f - Seg(k, 0.4f, 0.9f);
                    SetGate(gy, zs, fa, ba);
                    SetFlash(0.4f * Mathf.Sin(Seg(k, 0.12f, 0.6f) * Mathf.PI));
                }
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
        if (style != MenuTheme.DoorKind.Classic) { UpdateShake(dt); UpdateDust(dt); }
    }

    void FinishClosing()
    {
        phase = Phase.Waiting;
        t = 0f;
        Action a = after; after = null;
        try { a?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
    }

    // 문이 맞닿는 순간: 쿵(나무) + 쇠 부딪힘, 화면 흔들림, 먼지.
    void Impact()
    {
        impacted = true;
        Play("Sfx/door/close_thud_impactWood_heavy_001", 0.55f);
        Play("Sfx/door/impactMetal_heavy_000", 0.7f);
        shakeT = 0f;
        shakeAmp = style == MenuTheme.DoorKind.Doors ? 10f : 8f;
        SpawnDust();
    }

    void SetFlash(float a)
    {
        if (flash == null) return;
        Color c = flash.color; c.a = Mathf.Clamp01(a); flash.color = c;
    }

    // 0 = 닫힘(두 짝이 가운데서 만남), 1 = 다 열림(경첩 쪽으로 젖혀져 가로 0). 젖혀질수록 어둡게·살짝 커지게(안쪽으로 다가오는 원근).
    void SetOpenAmount(float open, float half)
    {
        float width = half * Mathf.Cos(open * Mathf.PI * 0.5f);
        float shade = Mathf.Lerp(1f, 0.35f, open);
        float grow = 1f + 0.08f * Mathf.Sin(open * Mathf.PI);
        foreach ((RectTransform r, RawImage img) in new[] { (left, leftImage), (right, rightImage) })
        {
            r.sizeDelta = new Vector2(width, 1080f * grow);
            img.color = new Color(shade, shade, shade, 1f);
            r.gameObject.SetActive(width > 0.5f);
        }
    }

    // ───────────── 흔들림 · 먼지 ─────────────
    void UpdateShake(float dt)
    {
        shakeT += dt;
        float a = shakeAmp * Mathf.Exp(-shakeT * 9f);
        Vector2 off = a < 0.05f ? Vector2.zero : new Vector2(Mathf.Sin(shakeT * 95f), Mathf.Cos(shakeT * 83f)) * a;
        if (left != null) { left.anchoredPosition = off; right.anchoredPosition = off; }
        if (gateRoot != null) gateRoot.anchoredPosition = off;
    }

    class Dust { public RectTransform rect; public Image image; public Vector2 vel; public float life, max, baseAlpha, grow; }
    readonly List<Dust> dust = new List<Dust>();
    static Sprite softSprite;

    static Sprite Soft()
    {
        if (softSprite != null) return softSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        softSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return softSprite;
    }

    void SpawnDust()
    {
        if (dustRoot == null) return;
        bool gateStyle = style == MenuTheme.DoorKind.Portcullis;
        int count = gateStyle ? 26 : 22;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("d", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(dustRoot, false);
            var img = go.GetComponent<Image>();
            img.sprite = Soft(); img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = UnityEngine.Random.Range(24f, 70f);
            rt.sizeDelta = new Vector2(size, size);
            float baseAlpha = UnityEngine.Random.Range(0.18f, 0.38f);
            img.color = new Color(0.72f, 0.74f, 0.80f, baseAlpha);
            Vector2 pos, vel;
            if (gateStyle)
            {   // 문턱(화면 아래 가운데 폭 760)에서 먼지가 옆으로 퍼지며 피어오른다
                pos = new Vector2(UnityEngine.Random.Range(-380f, 380f), -470f + UnityEngine.Random.Range(0f, 40f));
                vel = new Vector2(UnityEngine.Random.Range(-140f, 140f), UnityEngine.Random.Range(30f, 130f));
            }
            else
            {   // 두 짝이 만나는 가운데 줄에서 위쪽 먼지가 털려 내려온다
                pos = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-100f, 480f));
                vel = new Vector2(UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(-90f, -20f));
            }
            rt.anchoredPosition = pos;
            dust.Add(new Dust { rect = rt, image = img, vel = vel, life = 0f, max = UnityEngine.Random.Range(0.7f, 1.2f), baseAlpha = baseAlpha, grow = UnityEngine.Random.Range(0.8f, 1.8f) });
        }
    }

    void UpdateDust(float dt)
    {
        for (int i = dust.Count - 1; i >= 0; i--)
        {
            Dust d = dust[i];
            d.life += dt;
            if (d.life >= d.max) { Destroy(d.rect.gameObject); dust.RemoveAt(i); continue; }
            float k = d.life / d.max;
            d.rect.anchoredPosition += d.vel * dt;
            d.vel *= Mathf.Exp(-1.6f * dt);
            d.rect.localScale = Vector3.one * (1f + d.grow * k);
            Color c = d.image.color; c.a = d.baseAlpha * (1f - k); d.image.color = c;
        }
    }

    void Play(string path, float volume)
    {
        if (!GameSound.Enabled) return;
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip != null) audioSource.PlayOneShot(clip, volume * AudioPrefs.SfxVolume);
    }
}
