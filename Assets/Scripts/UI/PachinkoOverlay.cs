using System;
using System.Collections;
using System.Collections.Generic;
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
    Image backdrop, flash, dim, ringImage, pushBtnImage, gaugeFill, bandImage, cutBand;
    RectTransform machineRect, frameGroup, pushGroup, bandRect, cutRect, spSlotPanel, raysRect, ticker;
    TMP_Text pauseText, tickerText, meterText, pushCount, pushKeyText, bandText, cutText, spText, yokokuSmall, chanceText;
    Image[] holdOrbs;
    Image logoReach, logoKeizoku, logoGekiatsu, logoFukkatsu, logoOoatari, logoAtari, logoZannen;
    Reel[] reels;
    readonly List<Image> particlePool = new List<Image>();
    TMP_FontAsset font;
    Sprite circle;
    AudioSource audioSource;

    Coroutine running;
    bool standalone;          // 컨트롤러 없이 시험(Preview) — PUSH를 스스로 닫는다
    int pushedWho = int.MinValue;   // 내 PUSH 결과(누름 ≥0 / 자동 -1). int.MinValue = 아직
    float bgPulse, shakeUntil;
    Color bgTint = new Color(0.05f, 0.02f, 0.09f, 0.94f), bgTarget;
    bool pushFlashing;
    bool win;

    // ───────── 훅 연결 ─────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        if (!Enabled) return;
        PachinkoController.PlayHandler = plan => Ensure().Begin(plan, false);
        PachinkoController.PushedHandler = who => { if (instance != null) instance.pushedWho = who; };
    }

    /// <summary>컨트롤러 없이 한 판 보기(개발·촬영): PUSH 5초 뒤 스스로 결과로 간다. Space·클릭도 먹는다.</summary>
    public static void Preview(PachinkoPlan plan) => Ensure().Begin(plan, true);

    public static bool IsPlaying => instance != null && instance.running != null;

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

        // 위 띠(흐르는 글자)
        ticker = Rt("ticker", stage); Place(ticker, 0, 100, 1920, 64);
        var tickBg = ticker.gameObject.AddComponent<Image>(); tickBg.color = new Color(0.78f, 0.1f, 0.45f, 0.92f); tickBg.raycastTarget = false;
        ticker.gameObject.AddComponent<RectMask2D>();
        tickerText = Label("tickerText", ticker, "★ G.R.D 大海賊フィーバー ★ BOSS DOWN! 運命のルーレット ★ 揃えば大当たり ★ G.R.D 大海賊フィーバー ★ BOSS DOWN! 運命のルーレット ★ 揃えば大当たり ★", 36f, Color.white, TextAlignmentOptions.MidlineLeft, 0.18f);
        tickerText.rectTransform.anchorMin = new Vector2(0, 0); tickerText.rectTransform.anchorMax = new Vector2(0, 1);
        tickerText.rectTransform.pivot = new Vector2(0, 0.5f); tickerText.rectTransform.sizeDelta = new Vector2(4600, 0);

        // 정지 안내
        pauseText = Label("pause", stage, "⏸ 全員一時停止中", 34f, Color.white, TextAlignmentOptions.Center, 0.15f);
        Place(pauseText.rectTransform, 660, 30, 600, 56);

        // 기계 = 바깥 장식 테두리(금 틀 바깥만) + 큰 숫자 세 줄(창 없이 화면 가운데를 크게 차지, 사장님 10-14 「실제 파칭코처럼」)
        machineRect = Rt("machine", stage); Place(machineRect, 0, 0, 1920, 1080);
        frameGroup = Rt("frameGroup", machineRect); Place(frameGroup, 0, 0, 1920, 1080);
        frameGroup.localScale = Vector3.one * 1.6f; frameGroup.anchoredPosition += new Vector2(0f, 60f);   // 틀 바깥 테두리가 화면 가장자리에 오게 키운다
        Image frame = Img("frame", frameGroup, Spr("machine_border") ?? Spr("machine_frame"), Color.white);
        Place(frame.rectTransform, 0, 0, 1920, 1080);
        var marquee = Label("marquee", frameGroup, "大海賊 フィーバー", 46f, new Color(1f, 0.93f, 0.55f), TextAlignmentOptions.Center, 0.2f);
        Place(marquee.rectTransform, layout.marquee.x, layout.marquee.y, layout.marquee.w, layout.marquee.h);
        holdOrbs = new Image[layout.holds.Length];
        for (int i = 0; i < holdOrbs.Length; i++)
        {
            holdOrbs[i] = Img("hold" + i, frameGroup, circle, new Color(0.62f, 0.66f, 0.74f, 0.9f));
            Place(holdOrbs[i].rectTransform, layout.holds[i].x - layout.holds[i].r, layout.holds[i].y - layout.holds[i].r, layout.holds[i].r * 2f, layout.holds[i].r * 2f);
        }
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
        pushKeyText = Label("pushKey", pushGroup, "を押せ！", 72f, Color.white, TextAlignmentOptions.MidlineLeft, 0.22f); Place(pushKeyText.rectTransform, 470, 590, 380, 100);
        pushCount = Label("pushCount", pushGroup, "5", 150f, Color.white, TextAlignmentOptions.Center, 0.25f); Place(pushCount.rectTransform, 300, -40, 200, 160);
        Image gaugeBg = Img("gaugeBg", pushGroup, null, new Color(0f, 0f, 0f, 0.55f)); Place(gaugeBg.rectTransform, 150, 730, 500, 22);
        gaugeFill = Img("gaugeFill", gaugeBg.transform, null, new Color(1f, 0.55f, 0.15f));
        gaugeFill.rectTransform.anchorMin = Vector2.zero; gaugeFill.rectTransform.anchorMax = Vector2.one; gaugeFill.rectTransform.offsetMin = gaugeFill.rectTransform.offsetMax = Vector2.zero;
        gaugeFill.type = Image.Type.Filled; gaugeFill.fillMethod = Image.FillMethod.Horizontal; gaugeFill.fillAmount = 1f;
        dim = Img("pushDim", stage, null, new Color(0f, 0f, 0f, 0f)); Stretch(dim.rectTransform); dim.rectTransform.SetSiblingIndex(pushGroup.GetSiblingIndex());
        dim.rectTransform.anchorMin = new Vector2(0.5f, 0.5f); dim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); dim.rectTransform.sizeDelta = new Vector2(2600, 1500);
        pushGroup.SetAsLastSibling();
        pushGroup.localScale = Vector3.one * 1.3f;   // 화면 가운데 크게
        pushGroup.gameObject.SetActive(false);

        // 보상 띠
        bandRect = Rt("reward", stage); Place(bandRect, 192, 860, 1536, 150);
        bandImage = bandRect.gameObject.AddComponent<Image>(); bandImage.sprite = Spr("UI/reward_band"); bandImage.raycastTarget = false;
        bandText = Label("bandText", bandRect, "", 56f, Color.white, TextAlignmentOptions.Center, 0.2f); Stretch(bandText.rectTransform);
        bandRect.gameObject.SetActive(false);

        meterText = Label("meter", stage, "期待度 –", 34f, Color.white, TextAlignmentOptions.MidlineRight, 0.15f);
        Place(meterText.rectTransform, 1420, 990, 480, 60);

        flash = Img("flash", root, null, new Color(1f, 1f, 1f, 0f)); Stretch(flash.rectTransform);

        root.gameObject.SetActive(false);
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
    const float NumberSize = 640f, NumberSpacing = 1100f, ColumnWidth = 560f;
    static readonly float[] BigX = { 420f, 960f, 1500f };
    const float BigY = 600f, SmallY = 150f, SmallScale = 0.3f;
    static readonly float[] SmallX = { 1480f, 1620f, 1760f };
    class Reel
    {
        public float scroll;       // 셀 단위 위치. 정수 = 그 번호가 정가운데
        public bool spinning;
        readonly Image[] cells = new Image[3];
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
            }
            Refresh();
        }
        public static int SymbolAt(int index) => ((index % 9) + 9) % 9 + 1;
        public void Refresh()
        {
            int baseIndex = Mathf.FloorToInt(scroll);
            float frac = scroll - baseIndex;
            for (int k = 0; k < 3; k++)
            {
                int idx = baseIndex + (k - 1);   // k=1이 가운데 칸
                cells[k].sprite = Spr($"Symbols/symbol_{SymbolAt(idx)}" + (spinning ? "_blur" : ""));
                cells[k].rectTransform.anchoredPosition = new Vector2(0f, (frac - (k - 1)) * NumberSpacing);
            }
        }
        /// <summary>t = 0(큰 숫자 가운데) → 1(작게 오른쪽 위로 — 연출이 화면을 쓸 때).</summary>
        public void SetSmall(float t)
        {
            float x = Mathf.Lerp(BigX[index], SmallX[index], t), y = Mathf.Lerp(BigY, SmallY, t);
            window.anchoredPosition = new Vector2(x, -y);
            window.localScale = Vector3.one * Mathf.Lerp(1f, SmallScale, t);
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
                t += Time.unscaledDeltaTime;
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

    // ───────── 연출 ─────────
    static readonly Color Gold = new Color(1f, 0.82f, 0.25f), RedC = new Color(1f, 0.18f, 0.29f), BlueC = new Color(0.16f, 0.65f, 1f), GreenC = new Color(0.23f, 1f, 0.42f), WhiteC = new Color(0.62f, 0.66f, 0.74f);

    void Begin(PachinkoPlan plan, bool isStandalone)
    {
        if (running != null) StopCoroutine(running);
        standalone = isStandalone;
        pushedWho = int.MinValue;
        win = plan.win;
        root.gameObject.SetActive(true);
        ResetVisuals();
        duckTarget = BgmDuck;
        running = StartCoroutine(Sequence(plan));
    }

    void ResetVisuals()
    {
        bgTarget = new Color(0.05f, 0.02f, 0.09f, 0.94f); bgTint = bgTarget; backdrop.color = bgTint;
        foreach (Image l in new[] { logoReach, logoKeizoku, logoGekiatsu, logoFukkatsu, logoOoatari, logoAtari, logoZannen }) l.gameObject.SetActive(false);
        yokokuSmall.gameObject.SetActive(false); chanceText.gameObject.SetActive(false);
        spSlotPanel.gameObject.SetActive(false); cutRect.gameObject.SetActive(false); pushGroup.gameObject.SetActive(false); bandRect.gameObject.SetActive(false);
        raysRect.gameObject.SetActive(false);
        dim.color = new Color(0f, 0f, 0f, 0f); flash.color = new Color(1f, 1f, 1f, 0f);
        foreach (Image o in holdOrbs) { o.color = WhiteC; o.rectTransform.localScale = Vector3.one; }
        foreach (Reel r in reels) { r.spinning = false; r.Highlight(Color.white); r.Refresh(); }
        numbersSmallT = numbersSmallTarget = 0f;
        foreach (Image p in particlePool) p.gameObject.SetActive(false);
        machineRect.localScale = Vector3.one * 0.5f; machineRect.localRotation = Quaternion.Euler(0, 0, -6f);
        meterText.text = "期待度 –";
        bgPulse = 0f;
        pushFlashing = false;
        stage.localPosition = Vector3.zero;
    }

    static string[] HoldNames = { "white", "blue", "green", "red", "gold" };
    int expect;   // 기대도(표시용 대략값)
    void SetExpect(int v) { expect = v; meterText.text = $"期待度 {v}%"; }

    IEnumerator Sequence(PachinkoPlan p)
    {
        float t0 = Time.unscaledTime;
        Play(Slot.Open);
        // 등장
        float u = 0f;
        while (u < 0.5f)
        {
            u += Time.unscaledDeltaTime;
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
        foreach (Reel r in reels) r.spinning = true;
        Play(Slot.ReelSpin);
        StartCoroutine(SpinLoop(speed));
        yield return Wait(0.03f);
        Coroutine leftStop = StartCoroutine(StopReel(0, p.left, 1.57f, speed));
        Coroutine rightStop = StartCoroutine(StopReel(2, p.right, 2.37f, speed));
        yield return Wait(1.6f); Play(Slot.ReelStop);
        yield return Wait(0.8f); Play(Slot.ReelStop);

        if (!p.reach) yield return Wait(0.4f);
        else
        {
            foreach (Image o in holdOrbs) o.rectTransform.localScale = Vector3.one;
            reels[1].Highlight(new Color(1f, 0.78f, 0.8f));
            // 3) リーチ — 1.3초
            Play(Slot.Reach);
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
                    chanceText.text = chance[k]; chanceText.color = chanceColor[k]; chanceText.gameObject.SetActive(true);
                    if (k == 1) Play(Slot.SpStage);
                    SetExpect(Mathf.Max(expect, pct[k]));
                    yield return Wait(0.75f);
                }
                chanceText.gameObject.SetActive(false);
                if (p.bg != PachinkoBg.Green)
                {
                    yield return SpReach(p.bg);
                }
            }
            // 6) 최종 컷인 — 1.6초
            if (p.cut != PachinkoCut.None)
            {
                yield return Cutin(p.cut);
            }
        }

        // PUSH 창이 열리는 시각까지 맞춘다
        float target = t0 + p.PreludeSeconds();
        while (Time.unscaledTime < target) yield return null;

        // 7) PUSH — PushSeconds 동안
        yield return PushWindow();
        if (pushedWho == int.MinValue)
        {
            // 컨트롤러 알림을 기다린다(최대 4초). 시험용이면 바로 간다.
            float wait = standalone ? 0f : 4f;
            while (pushedWho == int.MinValue && wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
            if (pushedWho == int.MinValue) pushedWho = -1;
        }
        pushGroup.gameObject.SetActive(false); dim.color = new Color(0, 0, 0, 0);
        float resultStart = Time.unscaledTime;

        // 8) 가운데 줄 정지 — 숫자가 다시 큼직하게
        Stop(Slot.PushOpen); Stop(Slot.ReelSpin);
        numbersSmallTarget = 0f;
        reels[1].Highlight(Color.white);
        int shown = p.revive ? (p.left % 9) + 1 : p.center;
        float stopDur = p.reach ? 0.9f : 0.6f;
        StopAllSpin();
        yield return StopReel(1, shown, stopDur, speed);
        Play(Slot.ReelStop);
        if (p.revive)
        {
            yield return Wait(1.0f);
            bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f);
            Play(Slot.Revive);
            StartCoroutine(Flash(0.7f));
            yield return Pop(logoFukkatsu, 0.9f, 3f);
            reels[1].spinning = true;
            yield return StopReel(1, p.center, 0.55f, 14f);
        }
        yield return Wait(0.3f);

        // 9) 결과
        if (p.win)
        {
            StartCoroutine(Flash(1f)); shakeUntil = Time.unscaledTime + 0.55f;
            SetExpect(100);
            if (p.jackpot)
            {
                bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f); bgPulse = 3f;
                raysRect.gameObject.SetActive(true);
                Play(Slot.Ooatari);
                StartCoroutine(Confetti(5.5f));
                StartCoroutine(Pop(logoOoatari, 6f, 5f, keep: true));
                ShowBand("大当たり報酬：ランダムユニット ×1");
            }
            else
            {
                bgTarget = new Color(0.45f, 0.3f, 0f, 0.92f);
                StartCoroutine(PlayFor(Slot.Atari, 6f));
                StartCoroutine(Pop(logoAtari, 6f, 4f, keep: true));
                ShowBand("当たり報酬：伝説・ヒドゥンの魂 ×1");
            }
        }
        else
        {
            Play(Slot.Zannen);
            StartCoroutine(Pop(logoZannen, 4f, 3f, keep: true));
            ShowBand(p.reach ? "残念賞：希少の魂 ×1" : "残念賞：特別の魂 ×1");
        }

        // 컨트롤러가 정한 결과 시간만큼 보여 주고 닫는다
        float until = resultStart + p.ResultSeconds();
        while (Time.unscaledTime < until) yield return null;
        yield return Fade(0.25f);
        while (VoiceActive()) yield return null;   // 대사는 끝까지
        StopAllSfx();
        duckTarget = 1f; GameBgm.Duck = 1f;
        root.gameObject.SetActive(false);
        running = null;
    }

    IEnumerator Wait(float s) { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }

    Color HoldColor(PachinkoHold h) { switch (h) { case PachinkoHold.Blue: return BlueC; case PachinkoHold.Green: return GreenC; case PachinkoHold.Red: return RedC; case PachinkoHold.Gold: return Gold; default: return Color.white; } }

    IEnumerator SpinLoop(float speed)
    {
        bool any = true;
        while (any)
        {
            any = false;
            foreach (Reel r in reels) if (r.spinning && !stopping.Contains(r)) { r.scroll += speed * Time.unscaledDeltaTime; r.Refresh(); any = true; }
            // 가운데 줄이 PUSH 전까지 계속 돈다 — 정지 코루틴이 시작되면 spinning을 직접 내린다
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

    IEnumerator Thud(Reel r) { yield return null; }

    IEnumerator Pop(Image logo, float seconds, float overshoot, bool keep = false)
    {
        logo.gameObject.SetActive(true);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / 0.25f);
            float s = Mathf.Lerp(overshoot, 1f, 1f - Mathf.Pow(1f - u, 3f));
            if (t > 0.25f) s = 1f + 0.04f * Mathf.Sin((t - 0.25f) * 14f);
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
        Play(cut == PachinkoCut.Gold ? Slot.CutinGold : Slot.Cutin);
        if (cut == PachinkoCut.Gold) { SetExpect(100); } else if (cut == PachinkoCut.Red) SetExpect(Mathf.Max(expect, 56));
        float t = 0f; const float total = 1.6f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
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
        pushGroup.gameObject.SetActive(true);
        pushBtnImage.sprite = Spr("UI/push_button_normal");
        pushBtnImage.rectTransform.localScale = Vector3.one;
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        Play(Slot.PushOpen);
        Play(Slot.PushVoice);   // 「間違っているのは僕じゃない…」 — 창이 열리는 순간부터 끝까지(PUSH를 눌러도 안 끊는다)
        float total = PachinkoSettings.PushSeconds;
        float t0 = Time.unscaledTime;
        int lastSec = -1;
        pushFlashing = true;
        while (true)
        {
            float elapsed = Time.unscaledTime - t0;
            float left = Mathf.Max(0f, total - elapsed);
            gaugeFill.fillAmount = left / total;
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
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / 0.4f);
            bandRect.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, 1f - Mathf.Pow(1f - u, 3f));
            yield return null;
        }
        while (bandRect.gameObject.activeInHierarchy)
        {
            bandRect.anchoredPosition = new Vector2(bandRect.anchoredPosition.x, -(860f + 75f) + Mathf.Sin(Time.unscaledTime * 6f) * 6f);
            yield return null;
        }
    }

    IEnumerator Flash(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float u = t / seconds;
            flash.color = new Color(1f, u < 0.5f ? 0.85f : 0.4f, u < 0.5f ? 0.3f : 0.4f, 1f - u);
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    IEnumerator Fade(float seconds)
    {
        float t = 0f;
        CanvasGroup g = root.GetComponent<CanvasGroup>() ?? root.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 1f;
        while (t < seconds) { t += Time.unscaledDeltaTime; g.alpha = 1f - t / seconds; yield return null; }
        g.alpha = 1f;
    }

    IEnumerator Confetti(float seconds)
    {
        Color[] cols = { RedC, Gold, BlueC, GreenC, new Color(0.6f, 0.3f, 1f), Color.white };
        var live = new List<(Image img, float x, float y, float vy, float rot, float rv, float life)>();
        float t = 0f, spawnAcc = 0f;
        while (t < seconds)
        {
            float dt = Time.unscaledDeltaTime; t += dt; spawnAcc += dt * 70f;
            while (spawnAcc >= 1f && t < seconds - 1.5f)
            {
                spawnAcc -= 1f;
                Image img = GetParticle();
                bool coin = UnityEngine.Random.value < 0.35f;
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
    void Update()
    {
        if (!root.gameObject.activeSelf) return;
        // 무대 크기 맞추기
        Rect r = root.rect;
        if (r.width > 1f && r.height > 1f)
        {
            float s = Mathf.Min(r.width / 1920f, r.height / 1080f);
            stage.localScale = new Vector3(s, s, 1f);
        }
        bgTint = Color.Lerp(bgTint, bgTarget, Time.unscaledDeltaTime * 6f);
        float pulse = bgPulse >= 1f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (bgPulse >= 3f ? 18f : 8f)) : 0f;
        backdrop.color = Color.Lerp(bgTint, Color.white * 0.2f + bgTint * 0.8f, pulse * 0.4f);
        GameBgm.Duck = Mathf.MoveTowards(GameBgm.Duck, duckTarget, Time.unscaledDeltaTime * 2f);
        numbersSmallT = Mathf.MoveTowards(numbersSmallT, numbersSmallTarget, Time.unscaledDeltaTime * 3f);
        if (reels != null) foreach (Reel rl in reels) rl.SetSmall(numbersSmallT);
        if (raysRect.gameObject.activeSelf) raysRect.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 40f);
        if (tickerText != null)
        {
            float x = -(Time.unscaledTime * 160f % 2300f);
            tickerText.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
        if (Time.unscaledTime < shakeUntil) stage.localPosition = new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-8f, 8f), 0f);
        else if (stage.localPosition != Vector3.zero) stage.localPosition = Vector3.zero;
        // 보류 구슬 박동
        if (holdOrbs != null)
            foreach (Image o in holdOrbs) if (o.color == RedC || o.color == Gold) o.rectTransform.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(Time.unscaledTime * 20f));
        // PUSH 버튼 번쩍
        if (pushFlashing) pushBtnImage.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.7f), Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f)));
        else pushBtnImage.color = Color.white;
    }

    // ───────── 소리 슬롯(사장님 10-14: 기준 영상 e도쿄구울에서 잘라 둔 것을 기본값으로, 슬롯마다 교체 가능 — Sfx[슬롯]=null이면 무음) ─────────
    public enum Slot { Open, Hold, ReelSpin, ReelStop, Reach, Yokoku, SpStage, SpLoop, Cutin, CutinGold, PushOpen, PushTick, PushPress, Revive, Atari, Ooatari, Zannen, PushVoice }
    /// <summary>슬롯별 소리. 코드에서 PachinkoOverlay.Sfx[(int)Slot.Reach] = clip 으로 바꾼다.</summary>
    public static readonly AudioClip[] Sfx = new AudioClip[Enum.GetValues(typeof(Slot)).Length];
    /// <summary>슬롯별 음량(폰 촬영이라 가게 소음이 섞여 있어 슬롯마다 따로 맞춘다).</summary>
    public static readonly float[] SfxGain = { 1f, 1f, 0.5f, 1f, 0.9f, 1f, 0.8f, 0.5f, 0.8f, 0.8f, 0.15f, 1f, 1f, 1f, 0.9f, 0.9f, 0.9f, 1f };
    static readonly bool[] SfxLoop = { false, false, true, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false };
    /// <summary>대사가 나오는 동안 다른 효과음을 줄이는 배율.</summary>
    public static float VoiceDuckOthers = 0.3f;
    /// <summary>파칭코 중 게임 배경음을 줄이는 양(−12dB ≈ 0.25). 끝나면 복원.</summary>
    public static float BgmDuck = 0.25f;

    static bool sfxLoaded;
    static void LoadDefaultSfx()
    {
        if (sfxLoaded) return; sfxLoaded = true;
        void Set(Slot s, string file) { if (Sfx[(int)s] == null) Sfx[(int)s] = Resources.Load<AudioClip>(Folder + "Sfx/" + file); }
        Set(Slot.ReelSpin, "spin"); Set(Slot.Reach, "reach"); Set(Slot.SpStage, "strong"); Set(Slot.SpLoop, "sp_loop");
        Set(Slot.Cutin, "cut_blue"); Set(Slot.CutinGold, "cut_red"); Set(Slot.PushOpen, "push");
        Set(Slot.PushVoice, "voice_push"); Set(Slot.Revive, "revive"); Set(Slot.Atari, "atari"); Set(Slot.Ooatari, "ooatari"); Set(Slot.Zannen, "zannen");
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
        if (slot != Slot.PushVoice && VoiceActive()) vol *= VoiceDuckOthers;   // 대사 우선
        src.volume = Mathf.Clamp01(vol);
        src.Play();
    }

    bool VoiceActive() => slotSources.TryGetValue(Slot.PushVoice, out AudioSource v) && v != null && v.isPlaying;

    void Stop(Slot slot) { if (slotSources.TryGetValue(slot, out AudioSource src) && src != null) src.Stop(); }
    void StopAllSfx() { foreach (AudioSource src in slotSources.Values) if (src != null) src.Stop(); }
    IEnumerator PlayFor(Slot slot, float seconds) { Play(slot); yield return Wait(seconds); Stop(slot); }
}
