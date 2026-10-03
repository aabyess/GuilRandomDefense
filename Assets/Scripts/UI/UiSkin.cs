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
}
