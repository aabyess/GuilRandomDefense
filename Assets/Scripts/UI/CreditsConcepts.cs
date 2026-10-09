using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제작진 화면 시안 C·D·E(사장님 10-09 「A/B는 색만 다른 같은 배치」 → 구도가 전혀 다른 셋):
///  C = 해적 보물지도(양피지가 펼쳐지고 잉크가 번지고 밀랍 도장이 찍힘) · D = 영화 엔딩 크레딧(밤바다·달빛 물결·지나가는 배 실루엣·아래에서 위로 흐르는 이름·레터박스) ·
///  E = 현상수배 포스터(WANTED·역할은 현상금 줄·바람에 흔들리는 종이). 그림은 전부 코드로 만든다(외부 에셋 없음).
/// CreditsSplash가 Variant 2/3/4일 때 이 클래스를 만들어 Apply(시각)만 부른다(건너뛰기·페이드·고정 촬영은 CreditsSplash가 그대로).
/// </summary>
public class CreditsConcepts
{
    public static readonly string[] Roles = { "제작", "스폰서", "PD", "도움을 준 사람들", "후원자" };
    public static readonly string[] Names = { "최상호", "노무현제단", "임장혁", "알두환 · 짬 모", "임장혁 · 강주혁" };

    readonly int variant;
    readonly Transform root;
    readonly TMP_FontAsset gothic, serif, sans;
    System.Action<float> apply;

    public CreditsConcepts(int variant, Transform root, TMP_FontAsset gothic, TMP_FontAsset serif, TMP_FontAsset sans)
    {
        this.variant = variant; this.root = root; this.gothic = gothic; this.serif = serif; this.sans = sans;
        if (this.serif == null) this.serif = sans;
        if (this.gothic == null) this.gothic = this.serif;
        switch (variant) { case 2: BuildParchment(); break; case 3: BuildFilm(); break; default: BuildWanted(); break; }
    }

    public void Apply(float t) => apply?.Invoke(t);

    // ── 공통 도우미 ──────────────────────────────────────────
    static float Ease(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
    static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(0.0001f, b - a));

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Sprite sprite = null, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>(); img.sprite = sprite; img.color = color ?? Color.white; img.raycastTarget = false;
        return rt;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, string text, Vector2 size2, Vector2 pos, TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2? anchor = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size2; rt.anchoredPosition = pos;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; t.text = text;
        return t;
    }

    static Sprite MakeSprite(int w, int h, System.Func<float, float, Color> pixel, bool repeat = false)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Hash(float x, float y) { float s = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    static float Noise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
    }
    static float Fbm(float x, float y) => Noise(x, y) * 0.5f + Noise(x * 2.1f, y * 2.1f) * 0.3f + Noise(x * 4.3f, y * 4.3f) * 0.2f;

    static Sprite Disc(int n = 64, float hard = 0f) => MakeSprite(n, n, (u, v) => { float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f; float a = Mathf.Clamp01((1f - d) / Mathf.Max(0.02f, 1f - hard)); return new Color(1, 1, 1, a); });
    static Sprite Ring(int n, float inner) => MakeSprite(n, n, (u, v) => { float d = Mathf.Sqrt((u - .5f) * (u - .5f) + (v - .5f) * (v - .5f)) * 2f; float a = Mathf.Clamp01((1f - d) * n * .5f) * Mathf.Clamp01((d - inner) * n * .5f); return new Color(1, 1, 1, a); });
    static Sprite Radial(Color c, Color e) => MakeSprite(128, 128, (u, v) => { float d = Mathf.Clamp01(Mathf.Sqrt((u - .5f) * (u - .5f) * 0.8f + (v - .5f) * (v - .5f)) * 2f / 1.1f); return Color.Lerp(c, e, Mathf.SmoothStep(0, 1, d)); });
    static Sprite VGradient(Color top, Color bottom) => MakeSprite(4, 128, (u, v) => Color.Lerp(bottom, top, v));

    static Sprite Parchment(int w, int h, bool torn)
    {
        return MakeSprite(w, h, (u, v) =>
        {
            float n = Fbm(u * 6f, v * 6f), n2 = Fbm(u * 24f + 9f, v * 24f);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 1.2f, Mathf.Min(v, 1f - v));
            float burn = Mathf.Clamp01(1f - edge * 7f);
            Color c = Color.Lerp(new Color(0.86f, 0.74f, 0.50f), new Color(0.70f, 0.55f, 0.33f), n * 0.9f);
            c = Color.Lerp(c, new Color(0.45f, 0.30f, 0.14f), burn * (0.55f + 0.4f * n2));
            c *= 0.94f + 0.1f * n2;
            float a = 1f;
            if (torn) { float ragged = Fbm(u * 30f, v * 30f) * 0.04f + Hash(Mathf.Floor(u * 120f), Mathf.Floor(v * 160f)) * 0.006f; a = Mathf.Clamp01((edge - ragged) * 90f); }
            return new Color(c.r, c.g, c.b, a);
        });
    }

    static Sprite Poly(int w, int h, params Vector2[][] polys)
    {
        return MakeSprite(w, h, (u, v) =>
        {
            foreach (var p in polys)
            {
                bool inside = false;
                for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                    if ((p[i].y > v) != (p[j].y > v) && u < (p[j].x - p[i].x) * (v - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
                if (inside) return Color.white;
            }
            return new Color(1, 1, 1, 0);
        });
    }

    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

    // ── C: 해적 보물지도 ───────────────────────────────────────
    void BuildParchment()
    {
        var bg = Rect("Bg", root, Vector2.one * .5f, new Vector2(2400, 1400), Vector2.zero, Radial(new Color(0.20f, 0.12f, 0.07f), new Color(0.03f, 0.02f, 0.015f)));
        Sprite paperSprite = Parchment(512, 330, true), disc = Disc(), ring = Ring(128, 0.9f);
        const float W = 1280f, H = 800f;
        // 마스크 컨테이너: 높이가 자라며 양피지가 펼쳐진다
        var maskGo = new GameObject("Unroll", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(root, false);
        var mask = (RectTransform)maskGo.transform; mask.anchorMin = mask.anchorMax = Vector2.one * .5f; mask.sizeDelta = new Vector2(W, H);
        var paper = Rect("Paper", mask, Vector2.one * .5f, new Vector2(W, H), Vector2.zero, paperSprite);
        // 지도 장식: 해안선 점선·X 표시·나침반
        var dots = new RectTransform[26];
        for (int i = 0; i < dots.Length; i++)
        {
            float f = i / (dots.Length - 1f);
            Vector2 p = new Vector2(Mathf.Lerp(-470f, 430f, f), -250f + Mathf.Sin(f * 5.2f) * 40f + f * 40f);
            dots[i] = Rect("Dot" + i, paper, Vector2.one * .5f, Vector2.one * 8f, p, disc, new Color(0.35f, 0.2f, 0.08f, 0f));
        }
        var xa = Rect("X1", paper, Vector2.one * .5f, new Vector2(46, 7), new Vector2(430, -210), null, new Color(0.62f, 0.1f, 0.06f, 0)); xa.localRotation = Quaternion.Euler(0, 0, 45);
        var xb = Rect("X2", paper, Vector2.one * .5f, new Vector2(46, 7), new Vector2(430, -210), null, new Color(0.62f, 0.1f, 0.06f, 0)); xb.localRotation = Quaternion.Euler(0, 0, -45);
        var compass = Rect("Compass", paper, Vector2.one * .5f, Vector2.one * 150f, new Vector2(-480, 250), ring, new Color(0.3f, 0.18f, 0.07f, 0));
        var spikes = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            spikes[i] = Rect("Spike" + i, compass, Vector2.one * .5f, new Vector2(14, 120), Vector2.zero, Poly(16, 128, new[] { new Vector2(.5f, 1f), new Vector2(1f, .5f), new Vector2(.5f, 0f), new Vector2(0f, .5f) }), new Color(0.3f, 0.18f, 0.07f, 0));
            spikes[i].localRotation = Quaternion.Euler(0, 0, i * 90f);
        }
        var cornerR = Rect("CornerR", paper, Vector2.one * .5f, Vector2.one * 130f, new Vector2(500, 290), ring, new Color(0.3f, 0.18f, 0.07f, 0));
        // 글자: 잉크가 번지듯 자간이 좁아지며 짙어진다
        var title = Text("Title", paper, gothic, 108f, new Color(0.24f, 0.13f, 0.05f, 0), "G.R.D", new Vector2(900, 150), new Vector2(0, 250));
        var line = Rect("Line", paper, Vector2.one * .5f, new Vector2(0, 3), new Vector2(0, 160), null, new Color(0.3f, 0.17f, 0.07f, 0f));
        var roleT = new TMP_Text[5]; var nameT = new TMP_Text[5];
        for (int i = 0; i < 5; i++)
        {
            float y = 80f - i * 92f;
            roleT[i] = Text("Role" + i, paper, sans, 24f, new Color(0.40f, 0.26f, 0.12f, 0), Roles[i], new Vector2(900, 30), new Vector2(0, y + 26));
            nameT[i] = Text("Name" + i, paper, serif, 46f, new Color(0.20f, 0.10f, 0.04f, 0), Names[i], new Vector2(900, 60), new Vector2(0, y - 14));
        }
        // 막대(위·아래 말린 축)
        Sprite rodSprite = MakeSprite(8, 64, (u, v) => { float s = 0.45f + 0.55f * Mathf.Sin(v * Mathf.PI); return new Color(0.42f * s + 0.12f, 0.26f * s + 0.07f, 0.12f * s + 0.04f, 1f); });
        var rodTop = Rect("RodTop", root, Vector2.one * .5f, new Vector2(W + 60, 36), Vector2.zero, rodSprite);
        var rodBot = Rect("RodBot", root, Vector2.one * .5f, new Vector2(W + 60, 36), Vector2.zero, rodSprite);
        var knobs = new RectTransform[4];
        for (int i = 0; i < 4; i++) knobs[i] = Rect("Knob" + i, root, Vector2.one * .5f, Vector2.one * 52f, Vector2.zero, disc, new Color(0.30f, 0.18f, 0.08f));
        // 밀랍 도장
        var seal = Rect("Seal", root, Vector2.one * .5f, Vector2.one * 190f, new Vector2(470, -245), disc, new Color(0.62f, 0.07f, 0.07f, 0));
        var sealRing = Rect("SealRing", seal, Vector2.one * .5f, Vector2.one * 160f, Vector2.zero, ring, new Color(0.40f, 0.03f, 0.03f, 0));
        var sealText = Text("SealText", seal, gothic, 54f, new Color(0.35f, 0.03f, 0.03f, 0), "G.R.D", new Vector2(190, 70), Vector2.zero);
        Color ink = new Color(0.24f, 0.13f, 0.05f);

        apply = t =>
        {
            float open = Ease(Seg(t, 0.15f, 1.35f));
            float h = Mathf.Lerp(40f, H, open);
            mask.sizeDelta = new Vector2(W, h);
            paper.anchoredPosition = Vector2.zero;
            rodTop.anchoredPosition = new Vector2(0, h * .5f + 6f); rodBot.anchoredPosition = new Vector2(0, -h * .5f - 6f);
            float rodA = Seg(t, 0f, 0.2f);
            SetAlpha(rodTop.GetComponent<Image>(), rodA); SetAlpha(rodBot.GetComponent<Image>(), rodA);
            for (int i = 0; i < 4; i++) { knobs[i].anchoredPosition = new Vector2((i % 2 == 0 ? -1 : 1) * (W * .5f + 34f), (i < 2 ? 1 : -1) * (h * .5f + 6f)); SetAlpha(knobs[i].GetComponent<Image>(), rodA); }
            bg.localScale = Vector3.one * (1f + 0.03f * Mathf.Clamp01(t / 4.4f));
            float ornament = Ease(Seg(t, 1.2f, 2.0f));
            for (int i = 0; i < dots.Length; i++) SetAlpha(dots[i].GetComponent<Image>(), Mathf.Clamp01(Seg(t, 1.3f, 3.0f) * dots.Length - i) * 0.7f);
            Color xc = new Color(0.62f, 0.1f, 0.06f, Ease(Seg(t, 2.9f, 3.2f))); xa.GetComponent<Image>().color = xc; xb.GetComponent<Image>().color = xc;
            var oc = new Color(0.3f, 0.18f, 0.07f, ornament * 0.8f);
            compass.GetComponent<Image>().color = oc; cornerR.GetComponent<Image>().color = oc;
            foreach (var s in spikes) s.GetComponent<Image>().color = oc;
            compass.localRotation = Quaternion.Euler(0, 0, -25f * (1f - ornament) + Mathf.Sin(t * 1.5f) * 2f);
            float ta = Ease(Seg(t, 1.5f, 2.2f));
            title.color = new Color(ink.r, ink.g, ink.b, ta); title.characterSpacing = Mathf.Lerp(40f, 0f, ta);
            line.sizeDelta = new Vector2(Mathf.Lerp(0f, 760f, Ease(Seg(t, 1.8f, 2.4f))), 3f); SetAlpha(line.GetComponent<Image>(), 0.8f);
            for (int i = 0; i < 5; i++)
            {
                float p = Ease(Seg(t, 2.0f + i * 0.25f, 2.0f + i * 0.25f + 0.7f));
                roleT[i].color = new Color(0.40f, 0.26f, 0.12f, p); roleT[i].characterSpacing = Mathf.Lerp(30f, 8f, p);
                nameT[i].color = new Color(0.20f, 0.10f, 0.04f, p); nameT[i].characterSpacing = Mathf.Lerp(24f, 0f, p);
            }
            float st = Seg(t, 3.0f, 3.25f);
            float sealScale = Mathf.Lerp(2.6f, 1f, Ease(st)); float sa = st > 0 ? 1f : 0f;
            seal.localScale = Vector3.one * sealScale;
            float shake = t > 3.25f && t < 3.5f ? Mathf.Sin(t * 90f) * 3f * (3.5f - t) * 4f : 0f;
            seal.anchoredPosition = new Vector2(470, -245 + shake);
            seal.GetComponent<Image>().color = new Color(0.62f, 0.07f, 0.07f, sa); sealRing.GetComponent<Image>().color = new Color(0.40f, 0.03f, 0.03f, sa); sealText.color = new Color(0.35f, 0.03f, 0.03f, sa * 0.9f);
        };
    }

    // ── D: 영화 엔딩 크레딧 ────────────────────────────────────
    void BuildFilm()
    {
        var sky = Rect("Sky", root, Vector2.one * .5f, new Vector2(2100, 1200), Vector2.zero, VGradient(new Color(0.01f, 0.02f, 0.06f), new Color(0.10f, 0.17f, 0.28f)));
        float horizon = -40f;
        sky.sizeDelta = new Vector2(2100, 1200); sky.anchoredPosition = new Vector2(0, 600f + horizon - 600f + 600f);
        var star = new RectTransform[40]; var rng = new System.Random(3); Sprite disc = Disc();
        for (int i = 0; i < star.Length; i++) star[i] = Rect("Star" + i, root, Vector2.one * .5f, Vector2.one * (2f + (float)rng.NextDouble() * 3f), new Vector2((float)rng.NextDouble() * 1900f - 950f, 60f + (float)rng.NextDouble() * 460f), disc, new Color(1, 1, 1, 0));
        var moonGlow = Rect("MoonGlow", root, Vector2.one * .5f, Vector2.one * 520f, new Vector2(-560, 250), disc, new Color(0.7f, 0.8f, 1f, 0.16f));
        var moon = Rect("Moon", root, Vector2.one * .5f, Vector2.one * 120f, new Vector2(-560, 250), Disc(128, 0.97f), new Color(0.95f, 0.97f, 1f));
        var sea = Rect("Sea", root, Vector2.one * .5f, new Vector2(2100, 700), new Vector2(0, horizon - 350f), VGradient(new Color(0.05f, 0.09f, 0.16f), new Color(0.01f, 0.02f, 0.05f)));
        // 달빛 길: 가로 줄이 깜박이며 아래로 퍼진다
        var glints = new RectTransform[26];
        for (int i = 0; i < glints.Length; i++)
        {
            float f = i / (glints.Length - 1f);
            glints[i] = Rect("Glint" + i, root, Vector2.one * .5f, new Vector2(30f + f * 230f, 4f), new Vector2(-560f + f * 150f, horizon - 6f - f * 330f), null, new Color(0.8f, 0.88f, 1f, 0.5f));
        }
        // 물결: 얇은 줄이 천천히 좌우로 밀린다
        var waves = new RectTransform[16]; var wavePhase = new float[16];
        for (int i = 0; i < waves.Length; i++)
        {
            float f = i / (waves.Length - 1f);
            waves[i] = Rect("Wave" + i, root, Vector2.one * .5f, new Vector2(220f + f * 600f, 3f + f * 3f), new Vector2((float)rng.NextDouble() * 1800f - 900f, horizon - 20f - f * f * 420f), null, new Color(0.35f, 0.5f, 0.7f, 0.12f + 0.16f * f));
            wavePhase[i] = (float)rng.NextDouble() * 6.28f;
        }
        // 배 실루엣
        Sprite shipSprite = Poly(256, 160,
            new[] { new Vector2(.05f, .30f), new Vector2(.95f, .30f), new Vector2(.84f, .12f), new Vector2(.14f, .12f) },
            new[] { new Vector2(.48f, .30f), new Vector2(.52f, .30f), new Vector2(.52f, .95f), new Vector2(.48f, .95f) },
            new[] { new Vector2(.22f, .30f), new Vector2(.25f, .30f), new Vector2(.25f, .72f), new Vector2(.22f, .72f) },
            new[] { new Vector2(.74f, .30f), new Vector2(.77f, .30f), new Vector2(.77f, .70f), new Vector2(.74f, .70f) },
            new[] { new Vector2(.54f, .36f), new Vector2(.54f, .90f), new Vector2(.80f, .45f) },
            new[] { new Vector2(.46f, .38f), new Vector2(.46f, .88f), new Vector2(.28f, .46f) },
            new[] { new Vector2(.27f, .76f), new Vector2(.27f, .38f), new Vector2(.40f, .40f) },
            new[] { new Vector2(.95f, .30f), new Vector2(1f, .36f), new Vector2(.84f, .30f) });
        var ship = Rect("Ship", root, Vector2.one * .5f, new Vector2(320, 200), new Vector2(-1000, horizon + 60f), shipSprite, new Color(0.01f, 0.01f, 0.02f));
        // 크레딧 목록(아래→위): 로고 + 줄
        var scroll = new GameObject("Scroll", typeof(RectTransform)); scroll.transform.SetParent(root, false);
        var sr = (RectTransform)scroll.transform; sr.anchorMin = sr.anchorMax = Vector2.one * .5f; sr.sizeDelta = new Vector2(900, 100);
        var logo = Text("Logo", sr, gothic, 120f, new Color(0.95f, 0.93f, 0.85f), "G.R.D", new Vector2(900, 160), new Vector2(0, 0));
        var items = new TMP_Text[10];
        for (int i = 0; i < 5; i++)
        {
            float y = -230f - i * 120f;
            items[i * 2] = Text("Role" + i, sr, sans, 24f, new Color(0.7f, 0.74f, 0.82f), "<cspace=10>" + Roles[i] + "</cspace>", new Vector2(900, 30), new Vector2(0, y + 28));
            items[i * 2 + 1] = Text("Name" + i, sr, serif, 50f, new Color(0.96f, 0.95f, 0.9f), Names[i], new Vector2(900, 64), new Vector2(0, y - 16));
        }
        // 레터박스
        var barT = Rect("BarTop", root, Vector2.one * .5f, new Vector2(2100, 400), new Vector2(0, 540 + 200), null, Color.black);
        var barB = Rect("BarBot", root, Vector2.one * .5f, new Vector2(2100, 400), new Vector2(0, -540 - 200), null, Color.black);
        var fade = Rect("Fade", root, Vector2.one * .5f, new Vector2(2200, 1300), Vector2.zero, null, new Color(0, 0, 0, 1));
        apply = t =>
        {
            float bars = Ease(Seg(t, 0f, 0.7f));
            barT.anchoredPosition = new Vector2(0, 540f + 200f - 150f * bars); barB.anchoredPosition = new Vector2(0, -540f - 200f + 150f * bars);
            SetAlpha(fade.GetComponent<Image>(), 1f - Ease(Seg(t, 0f, 0.9f)));
            for (int i = 0; i < star.Length; i++) SetAlpha(star[i].GetComponent<Image>(), (0.35f + 0.5f * Mathf.Abs(Mathf.Sin(t * 1.4f + i))) * Seg(t, 0.2f, 1f));
            moon.anchoredPosition = moonGlow.anchoredPosition = new Vector2(-560f + t * 6f, 250f);
            SetAlpha(moonGlow.GetComponent<Image>(), 0.13f + 0.04f * Mathf.Sin(t * 1.7f));
            for (int i = 0; i < glints.Length; i++) { var g = glints[i]; var p = g.anchoredPosition; p.x = -560f + t * 6f + (i / (glints.Length - 1f)) * 150f + Mathf.Sin(t * 2.2f + i * 1.7f) * 22f; g.anchoredPosition = p; SetAlpha(g.GetComponent<Image>(), 0.18f + 0.45f * Mathf.Abs(Mathf.Sin(t * 3f + i * 2.3f))); }
            for (int i = 0; i < waves.Length; i++) { var p = waves[i].anchoredPosition; p.x = Mathf.Repeat(p.x + 950f + Mathf.Sin(t * 0.9f + wavePhase[i]) * 0.9f + (i % 2 == 0 ? 0.3f : -0.3f), 1900f) - 950f; waves[i].anchoredPosition = p; }
            float shipP = Seg(t, 0.4f, 4.4f);
            ship.anchoredPosition = new Vector2(Mathf.Lerp(-1050f, 1050f, shipP), horizon + 58f + Mathf.Sin(t * 1.8f) * 6f);
            ship.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.4f) * 2.2f);
            float flow = Seg(t, 0.5f, 4.4f);
            sr.anchoredPosition = new Vector2(220f, Mathf.Lerp(-640f, 1500f, flow) - 100f + 200f);
            sea.localScale = Vector3.one;
        };
    }

    // ── E: 현상수배 포스터 ─────────────────────────────────────
    void BuildWanted()
    {
        var bg = Rect("Bg", root, Vector2.one * .5f, new Vector2(2400, 1400), Vector2.zero, Radial(new Color(0.22f, 0.17f, 0.12f), new Color(0.04f, 0.03f, 0.02f)));
        Sprite plank = MakeSprite(256, 64, (u, v) => { float g = 0.5f + 0.5f * Fbm(u * 3f, v * 40f); return new Color(0.28f * g + 0.08f, 0.19f * g + 0.05f, 0.11f * g + 0.03f, 0.55f); }, true);
        for (int i = 0; i < 6; i++) Rect("Plank" + i, root, Vector2.one * .5f, new Vector2(2400, 190), new Vector2(0, -475 + i * 190), plank);
        const float PW = 820f, PH = 1010f;
        var holder = new GameObject("Poster", typeof(RectTransform)); holder.transform.SetParent(root, false);
        var hr = (RectTransform)holder.transform; hr.anchorMin = hr.anchorMax = new Vector2(.5f, .5f); hr.pivot = new Vector2(.5f, 1f); hr.sizeDelta = new Vector2(PW, PH); hr.anchoredPosition = new Vector2(0, 520);
        var paper = Rect("Paper", hr, new Vector2(.5f, .5f), new Vector2(PW, PH), new Vector2(0, -PH * .5f), Parchment(256, 316, true), new Color(1f, 0.97f, 0.88f));
        var pr = paper;
        Color ink = new Color(0.20f, 0.11f, 0.05f);
        var wanted = Text("Wanted", pr, serif, 150f, ink, "WANTED", new Vector2(760, 190), new Vector2(0, 380));
        var photoBack = Rect("Photo", pr, Vector2.one * .5f, new Vector2(560, 400), new Vector2(0, 110), null, new Color(0.18f, 0.12f, 0.07f));
        var photoIn = Rect("PhotoIn", pr, Vector2.one * .5f, new Vector2(530, 370), new Vector2(0, 110), Radial(new Color(0.84f, 0.70f, 0.46f), new Color(0.58f, 0.43f, 0.24f)));
        var logo = Text("Logo", pr, gothic, 130f, ink, "G.R.D", new Vector2(520, 170), new Vector2(0, 118));
        var doa = Text("DOA", pr, serif, 52f, ink, "<cspace=6>DEAD OR ALIVE</cspace>", new Vector2(760, 70), new Vector2(0, -130));
        var rows = new RectTransform[5]; var rowGroup = new CanvasGroup[5];
        for (int i = 0; i < 5; i++)
        {
            float y = -218f - i * 88f;
            var row = new GameObject("Bounty" + i, typeof(RectTransform), typeof(CanvasGroup)); row.transform.SetParent(pr, false);
            var rr = (RectTransform)row.transform; rr.anchorMin = rr.anchorMax = Vector2.one * .5f; rr.sizeDelta = new Vector2(740, 80); rr.anchoredPosition = new Vector2(0, y);
            Text("Role", rr, sans, 24f, new Color(0.38f, 0.24f, 0.12f), Roles[i], new Vector2(250, 40), new Vector2(-250, 0), TextAlignmentOptions.Left);
            Text("Name", rr, serif, 44f, ink, Names[i], new Vector2(380, 60), new Vector2(10, 0), TextAlignmentOptions.Center);
            Text("Reward", rr, serif, 34f, new Color(0.55f, 0.1f, 0.06f), "∞ 베리", new Vector2(200, 50), new Vector2(300, -2), TextAlignmentOptions.Right);
            rows[i] = rr; rowGroup[i] = row.GetComponent<CanvasGroup>();
            Rect("Rule", rr, Vector2.one * .5f, new Vector2(700, 2), new Vector2(0, -38), null, new Color(ink.r, ink.g, ink.b, 0.35f));
        }
        var nail = Rect("Nail", root, Vector2.one * .5f, Vector2.one * 28f, new Vector2(0, 520 - 6), Disc(32, 0.8f), new Color(0.2f, 0.18f, 0.16f));
        var stamp = Text("Stamp", pr, serif, 60f, new Color(0.65f, 0.08f, 0.05f, 0), "<cspace=4>제 작 진</cspace>", new Vector2(420, 80), new Vector2(210, -660));
        apply = t =>
        {
            float drop = Ease(Seg(t, 0f, 0.55f));
            hr.anchoredPosition = new Vector2(0, Mathf.Lerp(1400f, 520f, drop));
            float damp = Mathf.Exp(-Mathf.Max(0f, t - 0.5f) * 0.45f);
            float sway = (t < 0.55f ? 6f * (1f - drop) : 0f) + Mathf.Sin(t * 2.1f) * 2.8f * damp * Seg(t, 0.4f, 0.9f) + Mathf.Sin(t * 5.3f) * 0.5f;
            hr.localRotation = Quaternion.Euler(0, 0, sway);
            SetAlpha(nail.GetComponent<Image>(), Seg(t, 0.4f, 0.6f));
            bg.localScale = Vector3.one * (1f + 0.025f * Mathf.Clamp01(t / 4.4f));
            wanted.alpha = Ease(Seg(t, 0.5f, 0.9f)); wanted.characterSpacing = Mathf.Lerp(30f, 0f, wanted.alpha);
            photoBack.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.07f, Ease(Seg(t, 0.7f, 1.0f))); photoIn.GetComponent<Image>().color = new Color(1, 1, 1, Ease(Seg(t, 0.7f, 1.0f)));
            float lp = Ease(Seg(t, 1.0f, 1.5f)); logo.alpha = lp; logo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, lp);
            doa.alpha = Ease(Seg(t, 1.4f, 1.8f));
            for (int i = 0; i < 5; i++)
            {
                float p = Ease(Seg(t, 1.8f + i * 0.28f, 1.8f + i * 0.28f + 0.3f));
                rowGroup[i].alpha = p; rows[i].localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, p);
            }
            float sp = Ease(Seg(t, 3.5f, 3.75f)); stamp.color = new Color(0.65f, 0.08f, 0.05f, sp * 0.85f); stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, sp); stamp.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);
        };
    }
}
