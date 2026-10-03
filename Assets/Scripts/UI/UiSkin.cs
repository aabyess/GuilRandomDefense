using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI 그림 슬롯 — 코드는 이름으로만 그림을 부르고(Resources/UI/Skin/<이름>.png), 그림이 없으면 색 칠한 칸으로 물러난다.
// 지금 들어 있는 그림은 Tools/ui/gen_ui_skin.py가 사장님 인게임 사진 모양을 근사해 직접 그린 것이다. 정품 워크3 그림이 생기면
// 같은 이름 파일로 바꿔 끼우면 코드 수정 없이 교체된다(Docs/UI_ORIGINAL_STYLE.md ④).
public static class UiSkin
{
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
