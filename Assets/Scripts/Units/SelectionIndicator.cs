using UnityEngine;

// 선택된 유닛 발밑에 뜨는 원형 표시. 프리팹에 미리 붙여둘 필요 없이, Selectable이 필요할 때
// 자식으로 직접 만든다. LineRenderer로 원을 그려서 스프라이트/메시 에셋이 필요 없다.
[RequireComponent(typeof(LineRenderer))]
public class SelectionIndicator : MonoBehaviour
{
    const int Segments = 32;
    // 반지름에 비례해야 한다. 고정값이면 키 20짜리 유닛 발밑에서 머리카락처럼 보인다.
    const float LineWidthRatio = 0.06f;
    // 선택했을 때 더 굵고 밝게. 색은 주인을 가리키므로 선택 여부로 바꾸면 안 된다.
    const float SelectedWidthBoost = 2.2f;
    const float HeightOffset = 0.05f; // 바닥에 딱 붙이면 z-fighting이 날 수 있어 살짝 띄운다.

    // 색은 주인을 가리킨다(PlayerColors). 예전엔 "내 것 초록 / 남의 것 빨강"이었는데,
    // 그러면 넷이 붙었을 때 남 셋이 전부 같은 색이라 누구 유닛인지 구분이 안 된다.

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
    float baseWidth;
    Color teamColor = Color.white;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = LineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    // 생성 직후 딱 한 번만 호출한다 — 반지름·색은 유닛 생애 동안 안 바뀐다.
    public void Configure(float radius, int ownerId)
    {
        BuildCircle(radius);

        baseWidth = Mathf.Max(0.05f, radius * LineWidthRatio);
        teamColor = PlayerColors.Get(ownerId);
        SetSelected(false);
    }

    // 항상 보인다. 이 고리는 "선택했다"가 아니라 "누구 것이다"를 말한다 —
    // 넷이 붙었을 때 발밑 색으로 소속을 읽는 게 워크·스타의 방식이다.
    public void SetSelected(bool selected)
    {
        line.widthMultiplier = selected ? baseWidth * SelectedWidthBoost : baseWidth;

        Color color = selected ? teamColor : new Color(teamColor.r, teamColor.g, teamColor.b, 0.55f);
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
