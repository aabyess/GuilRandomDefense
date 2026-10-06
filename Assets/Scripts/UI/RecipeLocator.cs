using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조합 검색에서 식을 고르면 카메라를 조합판(·뽑기섬 다른세계 줄)의 그 식으로 옮기고 그 식의 인형들 발밑에 금빛 고리를 몇 초 깜빡인다(시안 C 혼합).
/// 위치는 조합판 인형(RecipeDollSpawner가 실행 때 세운 DollInfo 인형)에서 찾는다 — 이름 규칙은 MapGenerator.PlaceRecipeRow:
/// 결과 인형 「결과_{결과 별명}_{결과 별명}」, 재료 인형 「재료_{결과 별명}_{재료 이름}」. 동명 유닛이 있어 DollInfo.Unit까지 맞춰 본다.
/// 순수 겉모습 — 게임 상태·판정은 안 건드린다. 호스트·클라 모두 로컬에서만.
/// </summary>
public static class RecipeLocator
{
    const float FlyHeight = 300f;        // 식 한 줄(재료 몇 개 + 결과)이 화면에 들어오는 높이
    const float SameRowRadius = 260f;    // 결과 인형 둘레에서 이 안의 재료 인형만 같은 식으로 본다
    const float RingDuration = 5f;
    const float RingDiameter = 15f;
    static Sprite ringSprite;
    static readonly List<GameObject> rings = new List<GameObject>();

    /// <summary>성공하면 true. 인형이 없으면 false(호출부가 안내).</summary>
    public static bool Locate(CombineRecipe recipe)
    {
        if (recipe == null || recipe.result == null) return false;
        string label = recipe.result.unitName;

        DollInfo resultDoll = null;
        var ingredientDolls = new List<DollInfo>();
        var wanted = new HashSet<UnitData>();
        if (recipe.ingredients != null)
            foreach (RecipeIngredient ing in recipe.ingredients)
                if (ing != null && ing.kind == IngredientKind.SpecificUnit && ing.unit != null) { wanted.Add(ing.unit); if (ing.alternativeUnit != null) wanted.Add(ing.alternativeUnit); }

        foreach (DollInfo doll in Object.FindObjectsByType<DollInfo>(FindObjectsSortMode.None))
        {
            if (doll == null || doll.Unit == null) continue;
            string n = doll.name;
            if (resultDoll == null && doll.Unit == recipe.result && n.StartsWith("결과_" + label)) resultDoll = doll;
            else if (wanted.Contains(doll.Unit) && n.StartsWith("재료_" + label + "_")) ingredientDolls.Add(doll);
        }
        if (resultDoll == null) return false;

        ClearRings();
        Vector3 anchor = resultDoll.transform.position;
        AddRing(resultDoll.transform.position);
        foreach (DollInfo doll in ingredientDolls)
            if ((doll.transform.position - anchor).sqrMagnitude <= SameRowRadius * SameRowRadius)
            {
                AddRing(doll.transform.position);
                anchor = Vector3.Lerp(anchor, doll.transform.position, 0.5f);   // 식 가운데쪽으로 조금 — 결과와 재료 줄이 같이 보이게
            }

        RtsCameraController camera = Object.FindFirstObjectByType<RtsCameraController>();
        if (camera != null) camera.FlyTo(anchor, FlyHeight);
        return true;
    }

    static void ClearRings()
    {
        foreach (GameObject ring in rings) if (ring != null) Object.Destroy(ring);
        rings.Clear();
    }

    static void AddRing(Vector3 at)
    {
        if (ringSprite == null) ringSprite = BuildRingSprite();
        GameObject go = new GameObject("조합검색_고리");
        go.transform.position = new Vector3(at.x, at.y + 0.6f, at.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = Vector3.one * (RingDiameter / (ringSprite.rect.width / ringSprite.pixelsPerUnit));
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ringSprite;
        renderer.color = new Color(1f, 0.86f, 0.35f, 0.9f);
        go.AddComponent<Pulse>();
        rings.Add(go);
    }

    class Pulse : MonoBehaviour
    {
        float age;
        SpriteRenderer renderer;
        void Awake() => renderer = GetComponent<SpriteRenderer>();
        void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age >= RingDuration) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01((RingDuration - age) / 1.2f);   // 끝 1.2초는 사라진다
            float blink = 0.55f + 0.45f * Mathf.Sin(age * 7f);
            Color c = renderer.color;
            c.a = 0.95f * blink * fade;
            renderer.color = c;
            transform.localScale = Vector3.one * (RingDiameter / (renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit)) * (1f + 0.08f * Mathf.Sin(age * 7f));
        }
    }

    static Sprite BuildRingSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                float outer = Mathf.Clamp01(half - 1f - d + 0.5f);
                float inner = Mathf.Clamp01(d - (half - 14f) + 0.5f);
                float glow = Mathf.Clamp01(1f - (half - 1f - d) / 24f) * 0.0f;
                byte a = (byte)(255f * Mathf.Clamp01(outer * inner + glow));
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
