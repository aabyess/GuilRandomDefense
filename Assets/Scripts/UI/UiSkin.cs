using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI 그림 슬롯 — 코드는 이름으로만 그림을 부르고(Resources/UI/Skin/<이름>.png), 그림이 없으면 색 칠한 칸으로 물러난다.
// 지금 들어 있는 그림은 Tools/ui/gen_ui_skin.py가 사장님 인게임 사진 모양을 근사해 직접 그린 것이다. 정품 워크3 그림이 생기면
// 같은 이름 파일로 바꿔 끼우면 코드 수정 없이 교체된다(Docs/UI_ORIGINAL_STYLE.md ④).
public static class UiSkin
{
    // ── 하단 UI 시안(사장님 10-07 「하단 UI만 바꿔 보라 · 별로다 다시 · 시안 3개」). 하단 바 밖(우상단 타이머·점수판·상단 바·명령 칸 버튼)은 늘 옛 돌 그림(Resources/UI/Skin) —
    //    여기서 고르는 건 **하단 바 바탕·윗선·콘솔 칸 액자**뿐이다. 기본 Stone = 옛 모양 그대로.
    //    A WoodTrim = 돌 바 + 얇은 나무 테두리 · B BrightWood = 밝은 원목(리벳 없음) · C Metal = 워크3 돌·금속(청동 테두리) · DarkWood = 10-06 첫 선술집(진한 나무·쇠띠·리벳).
    //    그림: A/B/C는 Resources/UI/SkinBar/(Tools/ui/gen_bar_variants.py가 그림 · Editor BarSkinApply가 임포트), DarkWood는 Resources/UI/SkinTavern/(blender).
    public enum BarStyle { Stone, WoodTrim, BrightWood, Metal, DarkWood }

    const string BarStyleKey = "UiBarStyle";
    static BarStyle? barStyle;

    /// <summary>하단 바 시안. 저장(PlayerPrefs)된 값, 없으면 Stone. 바꾸면 다음 판 HUD부터 적용된다.</summary>
    public static BarStyle Bar
    {
        get
        {
            if (!barStyle.HasValue)
            {
                int saved = 0;
                try { saved = PlayerPrefs.GetInt(BarStyleKey, 0); } catch { }
                barStyle = System.Enum.IsDefined(typeof(BarStyle), saved) ? (BarStyle)saved : BarStyle.Stone;
            }
            return barStyle.Value;
        }
        set
        {
            barStyle = value;
            try { PlayerPrefs.SetInt(BarStyleKey, (int)value); } catch { }
        }
    }

    // 옛 이름(구현담당3 F5 서랍이 읽을 수 있게 남김): 선술집 = DarkWood 시안.
    public enum UiTheme { Stone, Tavern }
    public static UiTheme Theme { get => Bar == BarStyle.DarkWood ? UiTheme.Tavern : UiTheme.Stone; set => Bar = value == UiTheme.Tavern ? BarStyle.DarkWood : BarStyle.Stone; }

    /// <summary>🔴 항상 false — 사장님 10-07 「하단 바 밖은 옛 Stone으로」: 상단 바·타이머·점수판·명령 칸 버튼·아이템 칸의 선술집 분기(GameHud의 IsTavern 가지)는 꺼 둔다(죽은 가지, 시안 확정 뒤 정리). 하단 바·콘솔 칸 액자만 Bar 시안을 따른다.</summary>
    public static bool IsTavern => false;

    static readonly Dictionary<string, Sprite> tavernCache = new Dictionary<string, Sprite>();

    /// <summary>선술집(DarkWood) 그림을 파일 이름(확장자 없이)으로 직접 부른다. 없으면 null.</summary>
    public static Sprite Tavern(string file)
    {
        if (tavernCache.TryGetValue(file, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>("UI/SkinTavern/" + file);
        tavernCache[file] = sprite;
        return sprite;
    }

    static readonly Dictionary<string, Sprite> barCache = new Dictionary<string, Sprite>();
    static Sprite SkinBar(string file)
    {
        if (barCache.TryGetValue(file, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>("UI/SkinBar/" + file);
        barCache[file] = sprite;
        return sprite;
    }

    static string BarPrefix => Bar == BarStyle.WoodTrim ? "a_" : Bar == BarStyle.BrightWood ? "b_" : Bar == BarStyle.Metal ? "c_" : null;

    /// <summary>하단 바 바탕 그림과 타일로 깔지(true) 늘려 붙일지(false). 그림이 없으면 옛 돌 타일로 물러난다.</summary>
    public static Sprite BarBackground(out bool tiled)
    {
        tiled = true;
        if (Bar == BarStyle.DarkWood) { Sprite dark = Tavern("bar_bottom_9slice"); if (dark != null) { tiled = false; return dark; } }
        string prefix = BarPrefix;
        Sprite sprite = prefix != null && prefix != "a_" ? SkinBar(prefix + "bar") : null;
        return sprite != null ? sprite : Get("stone_tile");   // Stone·A는 옛 돌 타일
    }

    /// <summary>바 윗선 띠(가로 타일). Stone·DarkWood는 null(옛 밝은 선 / 그림에 쇠띠가 있다).</summary>
    public static Sprite BarEdge()
    {
        string prefix = BarPrefix;
        return prefix != null ? SkinBar(prefix + "edge") : null;
    }

    /// <summary>콘솔 칸(미니맵·초상·정보·아이템·명령 틀) 액자 그림과 테두리 줄임 배율. Stone이면 null(옛 둥근 금테 고리).</summary>
    public static Sprite BarCell(out float borderShrink)
    {
        borderShrink = 1f;
        if (Bar == BarStyle.DarkWood) { borderShrink = 2.4f; return Tavern("cell_big_9slice"); }
        string prefix = BarPrefix;
        return prefix != null ? SkinBar(prefix + "cell") : null;
    }

    static Sprite whiteSprite;
    /// <summary>색만 칠하는 Image.Type.Filled 막대용 흰 스프라이트. 🔴 Filled는 스프라이트가 없으면 채움 비율이 안 먹고 늘 가득 찬 사각형으로 그려진다 —
    /// 적 체력바·사이드보스 게이지·마나 게이지가 그래서 닳아도 줄지 않았다(친구 베타 10-06 「닳는 게 안 보인다」).</summary>
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                tex.Apply();
                whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
            }
            return whiteSprite;
        }
    }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (cache.TryGetValue(name, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>("UI/Skin/" + name);
        cache[name] = sprite;   // 없어도 기억한다(매 프레임 Resources 조회 방지)
        return sprite;
    }

    /// <summary>이미지에 그림을 입힌다. 9-slice 테두리가 있으면 늘려 붙이고, 없으면 그대로. 그림이 없으면 fallback 색(없으면 그대로 둠).</summary>
    public static bool Apply(Image image, string name, Color? fallback = null)
    {
        Sprite sprite = Get(name);
        if (sprite == null)
        {
            if (fallback.HasValue) image.color = fallback.Value;
            return false;
        }
        image.sprite = sprite;
        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
        return true;
    }

    // ---- 둥근 사각형(사장님 10-03 「UI가 너무 각져 있다」) ----
    // 코드로 한 번 그려 캐시하는 9-slice 스프라이트: 채움(Fill)·테두리 고리(Ring). 정품 그림 슬롯(Resources/UI/Skin)과 별개의 기본 모양이라
    // 그림 파일이 없는 칸·테두리 띠가 이걸 쓴다. 모서리 반지름은 1080p 기준 px(캔버스가 비례 확대).
    public const float DefaultRadius = 8f;
    static readonly Dictionary<string, Sprite> roundCache = new Dictionary<string, Sprite>();

    public static Sprite RoundFill(float radius = DefaultRadius) => RoundShape(radius, 0f);

    /// <summary>속이 빈 둥근 테두리 고리(두께 thickness px).</summary>
    public static Sprite RoundRing(float radius, float thickness) => RoundShape(radius, Mathf.Max(0.5f, thickness));

    static Sprite RoundShape(float radius, float ring)
    {
        string key = radius.ToString("0.#") + "/" + ring.ToString("0.#");
        if (roundCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        int r = Mathf.Max(1, Mathf.CeilToInt(radius));
        int size = r * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 둥근 사각형의 부호 거리(안쪽이 음수) — 한 픽셀 폭으로 가장자리를 부드럽게 한다.
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d);
                if (ring > 0f) a -= Mathf.Clamp01(0.5f - (d + ring));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        float border = r + 1;
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        roundCache[key] = sprite;
        return sprite;
    }
}
