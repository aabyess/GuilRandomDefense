using UnityEngine;

// 선택한 유닛의 공격 범위를 발밑에 원으로 그린다. 선택했을 때만 보인다 —
// 항상 켜두면 유닛 수십 기가 선 레인이 원으로 뒤덮여 아무것도 안 보인다.
//
// 발밑 고리(SelectionIndicator)와는 다른 것이다. 그쪽은 몸집만 한 작은 원으로 등급을
// 말하고, 이쪽은 사거리만 한 큰 원으로 "어디까지 때리는가"를 말한다.
[RequireComponent(typeof(LineRenderer))]
public class AttackRangeIndicator : MonoBehaviour
{
    const int Segments = 48;          // 사거리 원은 크다. 32이면 각져 보인다.
    const float LineWidth = 0.35f;
    const float HeightOffset = 0.08f; // 발밑 고리(0.05)보다 살짝 위 — 겹칠 때 이쪽이 보인다.

    static readonly Color RangeColor = new Color(1f, 1f, 1f, 0.35f);

    // SelectionIndicator와 같은 이유로 한 장을 모두가 함께 쓴다 — 유닛마다 머티리얼을
    // 만들면 드로우콜이 그만큼 늘고 해제되지도 않는다.
    static Material sharedMaterial;

    static Material LineMaterial
    {
        get
        {
            if (sharedMaterial != null) return sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color");

            sharedMaterial = new Material(shader) { name = "AttackRangeIndicator (shared)" };
            return sharedMaterial;
        }
    }

    LineRenderer line;
    UnitAttacker attacker;
    float drawnRadius = -1f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = LineMaterial;
        line.widthMultiplier = LineWidth;
        line.startColor = RangeColor;
        line.endColor = RangeColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        attacker = GetComponentInParent<UnitAttacker>();
    }

    public void SetVisible(bool visible)
    {
        line.enabled = visible;
        if (visible) Rebuild();
    }

    // 사거리는 강화·버프로 바뀐다. 켤 때마다 지금 값으로 다시 그린다 —
    // 한 번 그리고 캐시하면 강화한 뒤에도 옛 원이 남는다.
    void Rebuild()
    {
        float radius = attacker != null ? attacker.AttackRange : 0f;
        if (radius <= 0f)
        {
            line.enabled = false;
            return;
        }

        if (Mathf.Approximately(radius, drawnRadius)) return;
        drawnRadius = radius;

        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius, HeightOffset, Mathf.Sin(angle) * radius));
        }
    }
}
