using UnityEngine;

/// <summary>
/// 광폭화 유닛의 겉모습(2026-10-07) — 몸 붉은 틴트 + 발밑 붉은 원판 + 방어 오라 반경 붉은 고리. 호스트의 실물(BerserkMob)과 MP 클라의 겉모습(NetEntity.Berserk) 둘 다에 붙는다.
/// 머티리얼은 인스턴스(renderer.materials)에 색만 섞는다 — 광폭화는 드물어 드로우콜 부담이 없다.
/// </summary>
public class BerserkLook : MonoBehaviour
{
    static readonly Color TintColor = new Color(1f, 0.28f, 0.22f, 1f);
    const float TintAmount = 0.55f;
    const float DiscWorldRadius = 16f;
    static Texture2D discTexture;
    LineRenderer ring;

    public static float DefenseRadiusWorld => 850f / WorldScale.Value;   // 원작 A125 반경 850

    void Start()
    {
        Tint();
        BuildDisc();
        BuildRing();
    }

    void Tint()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is ParticleSystemRenderer || r is TrailRenderer) continue;
            foreach (Material m in r.materials)
                foreach (string prop in new[] { "_BaseColor", "_Color" })
                    if (m.HasProperty(prop)) m.SetColor(prop, Color.Lerp(m.GetColor(prop), TintColor, TintAmount));
        }
    }

    static Texture2D DiscTexture()
    {
        if (discTexture != null) return discTexture;
        const int n = 64;
        discTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = d >= 1f ? 0f : Mathf.Lerp(0.75f, 0.25f, d);
                discTexture.SetPixel(x, y, new Color(1f, 0.1f, 0.05f, a));
            }
        discTexture.Apply();
        return discTexture;
    }

    static Shader FlatShader() => Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

    void BuildDisc()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "BerserkDisc";
        quad.transform.SetParent(transform, false);
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
        quad.transform.localPosition = new Vector3(0f, 0.4f / parentScale, 0f);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = Vector3.one * (DiscWorldRadius * 2f / parentScale);
        var rend = quad.GetComponent<Renderer>();
        rend.sharedMaterial = new Material(FlatShader()) { mainTexture = DiscTexture(), name = "BerserkDisc" };
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    void BuildRing()
    {
        GameObject go = new GameObject("BerserkRing");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.sharedMaterial = new Material(FlatShader()) { name = "BerserkRing" };
        Color c = new Color(1f, 0.15f, 0.1f, 0.7f);
        ring.startColor = c; ring.endColor = c;
        ring.widthMultiplier = Mathf.Max(1.5f, DefenseRadiusWorld * 0.015f);
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (ring == null) return;
        Vector3 c = transform.position;
        float r = DefenseRadiusWorld;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y + 0.3f, c.z + Mathf.Sin(a) * r));
        }
    }
}
