using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 첫 화면 선술집 소품(사장님 10-06 「오크통·맥주 그릇·접시 같은 거 클릭하면 달그락 달그락 하는 재미있는 기능」).
/// blender가 배경 조명에 맞춰 따로 그린 소품 PNG(Resources/UI/TitleProps)를 배경 사진 위·메뉴 아래에 얹는다.
/// 소품마다 종류가 있다: 누르면 종류에 맞게 흔들리고(통은 쿵 튀고, 잔은 달그락 기울고, 간판은 그네처럼 크게 흔들림)
/// 소리(Kenney CC0 Resources/Sfx/title_props)와 작은 입자(거품·맥주 방울)가 난다. 가만히 있어도 간판은 살랑, 등불은 깜빡인다.
/// 그림이 없으면 아무것도 안 얹는다(첫 화면은 그대로).
/// </summary>
public static class TitleProps
{
    public enum Kind { Barrel, Mug, Table, Sign, Lantern, Cat }

    // 자리(1920×1080 기준, 왼쪽 아래 원점 · 소품 바닥 가운데) · 높이(px) · 그림 이름 · 종류. blender README 권장값으로 맞춘다.
    // blender README(1920×1080 좌상단·크기)를 바닥 가운데 기준으로 옮긴 값. 간판 기둥은 오른쪽 메뉴 판과 겹쳐 왼쪽으로 당겼다.
    //   pivotTop = 위쪽 가운데가 축(사슬에 매달린 간판 판).
    static readonly (string sprite, Vector2 foot, float height, Kind kind, bool pivotTop)[] Layout =
    {
        ("prop_barrels",   new Vector2(191f, 34f),  386f, Kind.Barrel,  false),
        ("prop_table",     new Vector2(566f, 50f),  300f, Kind.Table,   false),
        ("prop_mug_floor", new Vector2(827f, 30f),   60f, Kind.Mug,     false),
        ("prop_cat",       new Vector2(961f, 14f),   91f, Kind.Cat,     false),
        ("prop_signpost",  new Vector2(1157f, 27f), 413f, Kind.Lantern, false),
        ("prop_signboard", new Vector2(1157f, 27f), 413f, Kind.Sign,    true),
    };

    /// <summary>NetLobbyUi가 배경을 깐 바로 뒤에 부른다(메뉴·제목보다 뒤).</summary>
    public static void Build(RectTransform root)
    {
        var layer = new GameObject("TitleProps", typeof(RectTransform));
        layer.transform.SetParent(root, false);
        var lr = (RectTransform)layer.transform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        foreach ((string name, Vector2 foot, float height, Kind kind, bool pivotTop) in Layout)
        {
            Sprite sprite = Resources.Load<Sprite>("UI/TitleProps/" + name);
            if (sprite == null) continue;
            Sprite shadow = Resources.Load<Sprite>("UI/TitleProps/" + name + "_shadow");
            if (shadow != null) Place(lr, name + "_shadow", shadow, foot, height, false, false);
            Image img = Place(lr, name, sprite, foot, height, true, pivotTop);
            img.gameObject.AddComponent<TitlePropFx>().kind = kind;
        }
    }

    static Image Place(RectTransform parent, string name, Sprite sprite, Vector2 foot, float height, bool clickable, bool pivotTop)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, pivotTop ? 1f : 0f);   // 바닥 가운데(또는 위 가운데) — 흔들림·튐의 축
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        r.sizeDelta = new Vector2(height * aspect, height);
        r.anchoredPosition = pivotTop ? foot + new Vector2(0f, height) : foot;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = clickable;
        return img;
    }
}

/// <summary>소품 하나의 반응 — 누름·마우스 올림·가만히 있을 때.</summary>
public class TitlePropFx : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public TitleProps.Kind kind;
    RectTransform rect;
    Image image;
    float t = 99f;            // 누른 뒤 지난 시간
    float swing;              // 간판 흔들림 세기
    bool hover;
    AudioSource source;
    static readonly Dictionary<TitleProps.Kind, string[]> Sounds = new Dictionary<TitleProps.Kind, string[]>
    {
        { TitleProps.Kind.Barrel,  new[] { "impactWood_medium_000", "impactWood_medium_001", "impactWood_medium_002" } },
        { TitleProps.Kind.Mug,     new[] { "impactGlass_light_000", "impactGlass_light_001", "impactGlass_light_002" } },
        { TitleProps.Kind.Table,   new[] { "impactPlate_light_000", "impactPlate_light_001", "impactPlate_light_002", "impactGlass_light_001" } },
        { TitleProps.Kind.Sign,    new[] { "creak1", "creak2" } },
        { TitleProps.Kind.Lantern, new[] { "impactGlass_light_002" } },
        { TitleProps.Kind.Cat,     new[] { "impactSoft_medium_000", "impactSoft_medium_001" } },
    };

    void Awake()
    {
        rect = (RectTransform)transform;
        image = GetComponent<Image>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        swing = kind == TitleProps.Kind.Sign ? 1.5f : 0f;
    }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) => hover = false;

    public void OnPointerClick(PointerEventData e)
    {
        t = 0f;
        if (kind == TitleProps.Kind.Sign) swing = 14f;
        if (GameSound.Enabled && Sounds.TryGetValue(kind, out string[] names))
        {
            AudioClip clip = Resources.Load<AudioClip>("Sfx/title_props/" + names[Random.Range(0, names.Length)]);
            if (clip != null) { source.pitch = Random.Range(0.92f, 1.08f); source.PlayOneShot(clip, 0.8f); }
        }
        if (kind == TitleProps.Kind.Mug || kind == TitleProps.Kind.Table) Burst("foam_particle", 7, new Color(1f, 0.97f, 0.88f), 0.85f);
        if (kind == TitleProps.Kind.Barrel) Burst("drop", 5, new Color(0.95f, 0.66f, 0.18f), 0.75f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        float k = Mathf.Max(0f, 1f - t / 0.6f);   // 누른 뒤 0.6초 동안 잦아듦
        float angle = 0f, sx = 1f, sy = 1f, lift = 0f;
        switch (kind)
        {
            case TitleProps.Kind.Barrel:   // 쿵 — 눌렸다 튀어오름
                lift = 18f * k * Mathf.Abs(Mathf.Sin(t * 18f));
                sy = 1f - 0.08f * k * Mathf.Cos(t * 18f); sx = 2f - sy;
                break;
            case TitleProps.Kind.Mug:
            case TitleProps.Kind.Table:   // 달그락 — 빠르게 좌우로 기울며 살짝 뜀
                angle = 7f * k * Mathf.Sin(t * 42f);
                lift = 6f * k * Mathf.Abs(Mathf.Sin(t * 42f));
                break;
            case TitleProps.Kind.Sign:    // 그네 — 늘 살랑, 누르면 크게(천천히 잦아듦)
                swing = Mathf.Lerp(swing, 1.5f, dt * 0.8f);
                angle = swing * Mathf.Sin(Time.unscaledTime * 2.1f);
                break;
            case TitleProps.Kind.Lantern: // 늘 깜빡, 누르면 번쩍
                float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.3f) + 0.3f * k;
                image.color = new Color(flicker, flicker * 0.96f, flicker * 0.9f, 1f);
                angle = 3f * k * Mathf.Sin(t * 20f);
                break;
            case TitleProps.Kind.Cat:     // 화들짝 — 위로 폴짝
                lift = 30f * Mathf.Max(0f, Mathf.Sin(Mathf.Min(t, 0.45f) / 0.45f * Mathf.PI)) * (t < 0.45f ? 1f : 0f);
                sy = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 1.3f) * 0.3f;   // 숨쉬기
                break;
        }
        float h = hover ? 1.04f : 1f;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        rect.localScale = new Vector3(sx * h, sy * h, 1f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, BaseY + lift);
        if (kind != TitleProps.Kind.Lantern) image.color = hover ? new Color(1.08f, 1.04f, 0.96f, 1f) : Color.white;
    }

    float baseY = float.NaN;
    float BaseY { get { if (float.IsNaN(baseY)) baseY = rect.anchoredPosition.y; return baseY; } }

    // 작은 입자 몇 개가 위로 튀었다 떨어진다(UI Image, 0.8초)
    void Burst(string sprite, int count, Color color, float life)
    {
        Sprite s = Resources.Load<Sprite>("UI/TitleProps/" + sprite);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("p", typeof(RectTransform), typeof(Image), typeof(TitlePropParticle));
            go.transform.SetParent(transform.parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.sizeDelta = Vector2.one * Random.Range(10f, 18f);
            r.anchoredPosition = rect.anchoredPosition + new Vector2(Random.Range(-0.2f, 0.2f) * rect.sizeDelta.x, rect.sizeDelta.y * Random.Range(0.6f, 0.95f));
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.color = color;
            img.raycastTarget = false;
            go.GetComponent<TitlePropParticle>().Init(new Vector2(Random.Range(-90f, 90f), Random.Range(160f, 280f)), life);
        }
    }
}

public class TitlePropParticle : MonoBehaviour
{
    Vector2 velocity; float life, age; Image image; RectTransform rect;
    public void Init(Vector2 v, float l) { velocity = v; life = l; image = GetComponent<Image>(); rect = (RectTransform)transform; }
    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        velocity.y -= 600f * dt;
        rect.anchoredPosition += velocity * dt;
        Color c = image.color; c.a = Mathf.Clamp01(1f - age / life); image.color = c;
        if (age >= life) Destroy(gameObject);
    }
}
