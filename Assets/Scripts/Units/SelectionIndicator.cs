using UnityEngine;

// 선택된 유닛 발밑에 뜨는 원형 표시. 프리팹에 미리 붙여둘 필요 없이, Selectable이 필요할 때
// 자식으로 직접 만든다. LineRenderer로 원을 그려서 스프라이트/메시 에셋이 필요 없다.
[RequireComponent(typeof(LineRenderer))]
public class SelectionIndicator : MonoBehaviour
{
    const int Segments = 32;
    const float LineWidth = 0.1f;
    const float HeightOffset = 0.05f; // 바닥에 딱 붙이면 z-fighting이 날 수 있어 살짝 띄운다.

    static readonly Color OwnColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);   // 내 유닛 — 초록
    static readonly Color EnemyColor = new Color(0.9f, 0.2f, 0.2f, 0.9f); // 남의 유닛 — 빨강

    // 표시마다 머티리얼을 새로 만들면 유닛 수만큼 드로우콜이 늘고, 만든 머티리얼은
    // 해제되지도 않는다. 색은 LineRenderer의 정점 색으로 주므로 한 장을 모두가 함께 쓴다.
    static Material sharedLineMaterial;

    static Material LineMaterial
    {
        get
        {
            if (sharedLineMaterial != null) return sharedLineMaterial;

            // Sprites/Default는 빌드에 포함되지 않을 수 있다. URP 기본 셰이더로 이어서 찾는다.
            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");

            sharedLineMaterial = new Material(shader) { name = "SelectionIndicator (shared)" };
            return sharedLineMaterial;
        }
    }

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = LineWidth;
        line.sharedMaterial = LineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        gameObject.SetActive(false);
    }

    // 생성 직후 딱 한 번만 호출한다 — 반지름·색은 유닛 생애 동안 안 바뀐다.
    public void Configure(float radius, int ownerId)
    {
        BuildCircle(radius);

        Color color = ownerId == LocalPlayer.LocalPlayerId ? OwnColor : EnemyColor;
        line.startColor = color;
        line.endColor = color;
    }

    void BuildCircle(float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, HeightOffset, Mathf.Sin(angle) * radius));
        }
    }
}
