using UnityEngine;

// 지점을 찍는 상점 칸(보물찾기 탐색 등)을 고른 뒤, 커서가 가리키는 땅에 「이만큼 찾는다」를
// 원으로 보여 준다. 워크3의 범위 지정 커서와 같은 역할이다(사장님 10-02 「범위 이펙트도 넣어줘 어느 정도인지」).
// 테두리 고리 + 옅게 채운 원판 + 안쪽으로 번지는 물결 고리. 한 개만 만들어 두고 켰다 껐다 한다.
public class TargetAreaIndicator : MonoBehaviour
{
    const int Segments = 64;
    const float HeightOffset = 0.3f;

    static readonly Color RingColor = new Color(1f, 0.85f, 0.35f, 0.9f);
    static readonly Color FillColor = new Color(1f, 0.85f, 0.35f, 0.16f);
    static readonly Color PulseColor = new Color(1f, 0.95f, 0.6f, 0.55f);

    static TargetAreaIndicator instance;
    static Material sharedMaterial;

    LineRenderer ring;
    LineRenderer pulse;
    MeshRenderer fill;
    float drawnRadius = -1f;

    public static void Show(Vector3 center, float radius)
    {
        if (radius <= 0f) { Hide(); return; }
        if (instance == null)
            instance = new GameObject("TargetAreaIndicator").AddComponent<TargetAreaIndicator>();
        instance.Place(center, radius);
    }

    public static void Hide()
    {
        if (instance != null && instance.gameObject.activeSelf) instance.gameObject.SetActive(false);
    }

    static Material LineMaterial
    {
        get
        {
            if (sharedMaterial != null) return sharedMaterial;
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");
            sharedMaterial = new Material(shader) { name = "TargetAreaIndicator (shared)" };
            return sharedMaterial;
        }
    }

    void Awake()
    {
        ring = CreateLine("Ring", RingColor);
        pulse = CreateLine("Pulse", PulseColor);

        GameObject disc = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
        disc.transform.SetParent(transform, false);
        disc.GetComponent<MeshFilter>().sharedMesh = BuildDisc();
        fill = disc.GetComponent<MeshRenderer>();
        fill.sharedMaterial = LineMaterial;
        fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fill.receiveShadows = false;
    }

    LineRenderer CreateLine(string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(LineRenderer));
        obj.transform.SetParent(transform, false);
        LineRenderer line = obj.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = LineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    // 반지름 1짜리 원판(정점 색으로 칠한다 — Sprites/Default는 정점 색을 곱한다). 배율로 키운다.
    static Mesh BuildDisc()
    {
        Vector3[] vertices = new Vector3[Segments + 1];
        Color[] colors = new Color[Segments + 1];
        int[] triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;
        colors[0] = FillColor;
        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            colors[i + 1] = FillColor;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % Segments + 1;   // 위에서 보이게 시계 방향
            triangles[i * 3 + 2] = i + 1;
        }
        Mesh mesh = new Mesh { name = "TargetAreaDisc", vertices = vertices, colors = colors, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    void Place(Vector3 center, float radius)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.position = center + Vector3.up * HeightOffset;

        if (!Mathf.Approximately(radius, drawnRadius))
        {
            drawnRadius = radius;
            SetCircle(ring, radius);
            ring.widthMultiplier = Mathf.Max(0.3f, radius * 0.025f);
            pulse.widthMultiplier = ring.widthMultiplier * 0.6f;
            fill.transform.localScale = new Vector3(radius, 1f, radius);
        }

        // 물결: 바깥에서 안쪽으로 1.2초마다 한 번 줄어든다 — 「이 안이 찾는 범위」가 눈에 들어오게.
        float t = Mathf.Repeat(Time.unscaledTime / 1.2f, 1f);
        SetCircle(pulse, radius * Mathf.Lerp(1f, 0.15f, t));
        Color c = PulseColor;
        c.a *= 1f - t;
        pulse.startColor = c;
        pulse.endColor = c;
    }

    static void SetCircle(LineRenderer line, float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }
}
