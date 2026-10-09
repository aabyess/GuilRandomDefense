using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 첫 화면 제작진(크레딧) 화면 — 실행당 한 번, 4~5초(사장님 10-06 「G.R.D 제작진」, 10-09 「나머지도 멋있게·애니메이션 OK」).
/// 연출: 어두운 남색 비네트 배경 + 천천히 떠오르는 금가루 · 로고가 살짝 커졌다 자리잡으며 금빛 광택이 좌→우로 한 번 훑고 은은히 맥동 ·
/// 로고 아래 장식 구분선(가운데 마름모에서 양옆으로 그어짐) · 줄마다 0.15초 간격으로 아래에서 위로 떠오르는 목록(역할=작고 흐린 금색 자간 넓게, 이름=명조 크게 밝은 금색) ·
/// 음악 출처는 맨 아래 작게 · 전체가 아주 약하게 확대(켄번스) · 끝에 흐려지며 첫 화면. 클릭·아무 키로 건너뛴다. 에디터에서 게임 씬으로 바로 들어가면 안 뜬다(미리보기: Tools/UI/제작진 화면 미리보기).
/// 문구는 <see cref="Lines"/> 한 곳만 고친다. 시안 둘: 0 = 남색(차가운 금), 1 = 갈색(따뜻한 금).
/// </summary>
public class CreditsSplash : MonoBehaviour
{
    static readonly (string role, string name)[] Lines =
    {
        ("제작", "최상호"),
        ("스폰서", "노무현제단"),
        ("PD", "임장혁"),
        ("도움을 준 사람들", "알두환 · 짬 모"),   // 사장님 10-06 「PD 임장혁 밑에」
        ("후원자", "임장혁 · 강주혁"),            // 사장님 10-09 「도움을 준 사람들 밑에」
    };
    // 배경음악(첫 화면 곡) 출처 — CC BY 4.0은 이름을 밝히는 게 조건이다.
    const string MusicCredit = "Music: \"Tavern Tales\" by Alexander Nakarada (CreatorChords) · CC BY 4.0";

    const float FadeIn = 0.6f, Hold = 3.0f, FadeOut = 0.8f;
    const int TitleSceneIndex = 0;   // NetBoot — 배포판 첫 씬

    static bool shown;
    public static int Variant;          // 시안(0 남색 · 1 갈색) — 미리보기가 바꾼다
    public static bool PreviewMode;     // 미리보기: 씬·한 번만 검사 무시, 끝나도 다시 띄울 수 있게

    CanvasGroup group;
    float t;
    bool skipping;
    RectTransform root;
    TMP_Text logo, glow;
    RectTransform lineLeft, lineRight, diamond;
    RectTransform[] rowRects; CanvasGroup[] rowGroups;
    CanvasGroup musicGroup;
    RectTransform[] dust; float[] dustSpeed, dustPhase, dustSize;
    Color gold, goldDim, goldBright;
    CreditsConcepts concept;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (shown || SceneManager.GetActiveScene().buildIndex != TitleSceneIndex) return;
        shown = true;
        new GameObject("[CreditsSplash]").AddComponent<CreditsSplash>();
    }

    /// <summary>미리보기(에디터·탐침): 씬과 상관없이 지금 띄운다.</summary>
    public static void ShowPreview(int variant)
    {
        var old = GameObject.Find("[CreditsSplash]"); if (old != null) Destroy(old);
        Variant = variant; PreviewMode = true;
        new GameObject("[CreditsSplash]").AddComponent<CreditsSplash>();
    }

    /// <summary>미리보기: 시각을 고정해 한 장면만 그린다(촬영용) — 0이면 자동 재생.</summary>
    public static float FreezeAt = -1f;

    void Awake()
    {
        bool navy = Variant == 0;
        gold = navy ? new Color(0.88f, 0.78f, 0.52f) : new Color(0.95f, 0.78f, 0.45f);
        goldDim = navy ? new Color(0.60f, 0.55f, 0.42f, 0.9f) : new Color(0.72f, 0.58f, 0.40f, 0.9f);
        goldBright = navy ? new Color(1f, 0.90f, 0.60f) : new Color(1f, 0.86f, 0.52f);

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   // 첫 화면 메뉴·로딩 위
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();   // 뒤의 메뉴 버튼이 눌리지 않게 막는다
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;   // 배경은 처음부터 불투명 — 첫 화면이 비쳐 보이지 않게

        // 켄번스 루트 — 배경·장식·글자 전부 이 안에서 같이 아주 약하게 커진다
        root = NewRect("Root", transform); Stretch(root);

        // 시안 C·D·E(10-09): 구도가 전혀 다른 연출은 CreditsConcepts가 맡는다(건너뛰기·페이드·고정 촬영은 여기 그대로)
        if (Variant >= 2)
        {
            var blackBg = NewRect("Black", root, true); Stretch(blackBg); blackBg.GetComponent<Image>().color = Color.black;
            concept = new CreditsConcepts(Variant, root,
                Resources.Load<TMP_FontAsset>("Fonts/UnifrakturMaguntia SDF"), Resources.Load<TMP_FontAsset>("Fonts/NanumMyeongjo-ExtraBold SDF"), Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF"));
            Apply(0f);
            return;
        }

        // 배경: 가장자리 검정 → 가운데 은은한 남색/갈색(방사 그라데이션 한 장)
        var bg = NewRect("Bg", root, true); Stretch(bg);
        var bgImage = bg.GetComponent<Image>();
        bgImage.sprite = RadialSprite(navy ? new Color(0.07f, 0.09f, 0.17f) : new Color(0.14f, 0.09f, 0.06f), navy ? new Color(0.005f, 0.006f, 0.012f) : new Color(0.012f, 0.008f, 0.005f));
        bgImage.color = Color.white; bgImage.raycastTarget = false;

        TMP_FontAsset gothic = Resources.Load<TMP_FontAsset>("Fonts/UnifrakturMaguntia SDF");
        TMP_FontAsset serif = Resources.Load<TMP_FontAsset>("Fonts/NanumMyeongjo-ExtraBold SDF");
        TMP_FontAsset sans = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF");
        if (serif == null) serif = Resources.Load<TMP_FontAsset>("Fonts/SongMyung SDF");

        // 금가루: 작은 점 30개가 천천히 떠오르며 깜박인다
        BuildDust(root);

        // 로고: 글로우(뒤, 크고 흐림) + 본체(광택)
        glow = NewText("LogoGlow", root, gothic != null ? gothic : sans, 132f, goldBright, TextAlignmentOptions.Center);
        SetRect((RectTransform)glow.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 190f), new Vector2(0f, 250f));
        glow.text = "G.R.D"; glow.fontStyle = FontStyles.Normal;
        logo = NewText("Logo", root, gothic != null ? gothic : sans, 126f, gold, TextAlignmentOptions.Center);
        SetRect((RectTransform)logo.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 190f), new Vector2(0f, 250f));
        logo.text = "G.R.D";

        // 장식 구분선: 가운데 마름모 + 좌우로 그어지는 가는 선
        diamond = NewRect("Diamond", root, true); SetRect(diamond, new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), new Vector2(0f, 140f));
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f); diamond.GetComponent<Image>().color = gold; diamond.GetComponent<Image>().raycastTarget = false;
        lineLeft = BuildLine("LineL", root, true); lineRight = BuildLine("LineR", root, false);

        // 목록: 줄마다 역할(작게) + 이름(크게)
        rowRects = new RectTransform[Lines.Length]; rowGroups = new CanvasGroup[Lines.Length];
        float y = 60f;
        for (int i = 0; i < Lines.Length; i++)
        {
            var row = NewRect("Row" + i, root); SetRect(row, new Vector2(0.5f, 0.5f), new Vector2(1200f, 84f), new Vector2(0f, y));
            var rg = row.gameObject.AddComponent<CanvasGroup>(); rg.alpha = 0f;
            var role = NewText("Role", row, sans, 24f, goldDim, TextAlignmentOptions.Center);
            SetRect((RectTransform)role.transform, new Vector2(0.5f, 1f), new Vector2(1200f, 28f), new Vector2(0f, -14f));
            role.text = "<cspace=8>" + Lines[i].role + "</cspace>";
            var nm = NewText("Name", row, serif != null ? serif : sans, 44f, goldBright, TextAlignmentOptions.Center);
            SetRect((RectTransform)nm.transform, new Vector2(0.5f, 0f), new Vector2(1200f, 56f), new Vector2(0f, 24f));
            nm.text = Lines[i].name;
            rowRects[i] = row; rowGroups[i] = rg;
            y -= 98f;
        }

        // 음악 크레딧: 맨 아래 작게
        var music = NewText("Music", root, sans, 20f, new Color(0.55f, 0.52f, 0.45f, 0.85f), TextAlignmentOptions.Center);
        SetRect((RectTransform)music.transform, new Vector2(0.5f, 0f), new Vector2(1400f, 30f), new Vector2(0f, 46f));
        music.text = MusicCredit;
        musicGroup = music.gameObject.AddComponent<CanvasGroup>(); musicGroup.alpha = 0f;

        Apply(0f);
    }

    // ── 조립 도우미 ─────────────────────────────────────────────
    static RectTransform NewRect(string name, Transform parent, bool image = false)
    {
        var go = image ? new GameObject(name, typeof(RectTransform), typeof(Image)) : new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (image) go.GetComponent<Image>().raycastTarget = false;
        return rt;
    }

    static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

    RectTransform BuildLine(string name, Transform parent, bool left)
    {
        var rt = NewRect(name, parent, true);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(left ? 1f : 0f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 2f); rt.anchoredPosition = new Vector2(left ? -14f : 14f, 140f);
        var img = rt.GetComponent<Image>(); img.color = gold; img.raycastTarget = false;
        return rt;
    }

    static Sprite RadialSprite(Color center, Color edge)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx * 0.8f + dy * dy) / 1.1f);
                tex.SetPixel(x, y, Color.Lerp(center, edge, Mathf.SmoothStep(0f, 1f, d)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite dotSprite;
    static Sprite DotSprite()
    {
        if (dotSprite != null) return dotSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        return dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void BuildDust(Transform parent)
    {
        const int count = 34;
        dust = new RectTransform[count]; dustSpeed = new float[count]; dustPhase = new float[count]; dustSize = new float[count];
        var rng = new System.Random(7);
        for (int i = 0; i < count; i++)
        {
            var rt = NewRect("Dust" + i, parent, true);
            float s = 4f + (float)rng.NextDouble() * 8f;
            dustSize[i] = s;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f); rt.sizeDelta = new Vector2(s, s);
            rt.anchoredPosition = new Vector2((float)rng.NextDouble() * 1920f, (float)rng.NextDouble() * 1080f);
            var img = rt.GetComponent<Image>(); img.sprite = DotSprite(); img.color = new Color(gold.r, gold.g, gold.b, 0f);
            dust[i] = rt; dustSpeed[i] = 14f + (float)rng.NextDouble() * 26f; dustPhase[i] = (float)rng.NextDouble() * 6.28f;
        }
    }

    // ── 한 프레임 그리기(시각 시간 → 모든 연출) ──────────────────
    static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

    void Apply(float time)
    {
        if (concept != null) { concept.Apply(time); return; }
        float total = FadeIn + Hold + FadeOut;
        // 켄번스: 처음부터 끝까지 1.00 → 1.035
        root.localScale = Vector3.one * (1f + 0.035f * Mathf.Clamp01(time / total));
        // 배경 비네트가 드러나는 페이드인(글자는 아래 각자)
        // 금가루
        for (int i = 0; i < dust.Length; i++)
        {
            Vector2 p = dust[i].anchoredPosition;
            p.y += dustSpeed[i] * Time.unscaledDeltaTime * (FreezeAt >= 0f ? 0f : 1f);
            if (p.y > 1090f) p.y = -10f;
            dust[i].anchoredPosition = p;
            float tw = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 1.3f + dustPhase[i]));
            var img = dust[i].GetComponent<Image>(); var c = img.color; c.a = 0.55f * tw * Mathf.Clamp01(time / 0.8f); img.color = c;
        }
        // 로고: 0~0.6 살짝 커진 데서 제자리 + 페이드인
        float lp = EaseOut(time / 0.6f);
        float scale = Mathf.Lerp(1.14f, 1f, lp);
        logo.rectTransform.localScale = Vector3.one * scale; glow.rectTransform.localScale = Vector3.one * (scale * 1.02f);
        logo.alpha = lp;
        // 맥동 글로우
        float pulse = 0.5f + 0.5f * Mathf.Sin((time - 0.6f) * 2.6f);
        glow.alpha = lp * (0.16f + 0.16f * pulse * Mathf.Clamp01((time - 0.4f) / 0.4f));
        // 금빛 광택: 0.7~1.5초에 글자 위로 좌→우 한 번
        Shine(Mathf.Clamp01((time - 0.7f) / 0.8f));
        // 구분선: 1.0~1.6초에 양옆으로 그어지며 나타남
        float dp = EaseOut((time - 0.9f) / 0.6f);
        diamond.GetComponent<Image>().color = new Color(gold.r, gold.g, gold.b, dp);
        diamond.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, dp);
        float len = 380f * dp;
        lineLeft.sizeDelta = new Vector2(len, 2f); lineRight.sizeDelta = new Vector2(len, 2f);
        var lc = new Color(gold.r, gold.g, gold.b, Mathf.Clamp01(dp * 1.5f) * 0.9f);
        lineLeft.GetComponent<Image>().color = lc; lineRight.GetComponent<Image>().color = lc;
        // 목록: 1.2초부터 0.15초 간격으로 아래에서 위로 떠오르며 나타남
        for (int i = 0; i < rowGroups.Length; i++)
        {
            float rp = EaseOut((time - (1.2f + 0.15f * i)) / 0.5f);
            rowGroups[i].alpha = rp;
            Vector2 pos = rowRects[i].anchoredPosition; float baseY = 60f - 98f * i;
            pos.y = baseY - (1f - rp) * 26f; rowRects[i].anchoredPosition = pos;
        }
        musicGroup.alpha = EaseOut((time - (1.2f + 0.15f * rowGroups.Length + 0.2f)) / 0.6f) * 0.9f;
    }

    // 광택: 글자 정점 색을 좌→우로 훑는 밝은 띠로 바꾼다(글자 모양만 빛난다)
    Color32[] baseColors;
    void Shine(float progress)
    {
        logo.ForceMeshUpdate();
        TMP_TextInfo info = logo.textInfo;
        if (info.characterCount == 0) return;
        float minX = info.characterInfo[0].bottomLeft.x, maxX = info.characterInfo[info.characterCount - 1].topRight.x;
        float width = Mathf.Max(1f, maxX - minX);
        float bandX = Mathf.Lerp(minX - width * 0.25f, maxX + width * 0.25f, progress);
        float bandW = width * 0.18f;
        Color baseColor = gold;
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            Color32[] colors = info.meshInfo[m].colors32;
            Vector3[] verts = info.meshInfo[m].vertices;
            for (int c = 0; c < info.characterCount; c++)
            {
                if (!info.characterInfo[c].isVisible || info.characterInfo[c].materialReferenceIndex != m) continue;
                int vi = info.characterInfo[c].vertexIndex;
                for (int k = 0; k < 4; k++)
                {
                    float d = Mathf.Abs(verts[vi + k].x - bandX) / bandW;
                    float f = progress <= 0f || progress >= 1f ? 0f : Mathf.Clamp01(1f - d);
                    Color col = Color.Lerp(baseColor, Color.white, f * f);
                    col.a = logo.alpha;
                    colors[vi + k] = col;
                }
            }
            info.meshInfo[m].mesh.colors32 = colors;
            logo.UpdateGeometry(info.meshInfo[m].mesh, m);
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // 첫 화면을 짓는 동안 한 프레임이 길게 걸려도 건너뛰지 않게(0.3.10 맥 실측: 2.5초에 이미 사라짐)
        if (FreezeAt >= 0f) { t = FreezeAt; Apply(t); return; }
        t += dt;
        if (!skipping && t > 0.2f && ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)))
        {
            skipping = true;
            t = Mathf.Max(t, FadeIn + Hold);   // 바로 흐려지기로
        }
        float total = FadeIn + Hold + FadeOut;
        Apply(Mathf.Min(t, total));
        if (t >= FadeIn + Hold)
        {
            group.alpha = 1f - Mathf.Clamp01((t - FadeIn - Hold) / FadeOut);
            group.blocksRaycasts = false;
        }
        if (t >= total) { if (PreviewMode) PreviewMode = false; Destroy(gameObject); }
    }
}
