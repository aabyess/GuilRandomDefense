using UnityEngine;

/// <summary>
/// 적 출발점 바닥 포탈의 발광 문양(사장님 10-07, blender enemy_portal.fbx) — 돌판 위 문양 평면을 Y축으로 천천히 돌리고 빛을 은은히 숨 쉬게 한다.
/// 장식뿐이다: 콜라이더·NavMesh 영향 없음. 돌판은 가만히 둔다.
/// </summary>
public class EnemyPortalSpin : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 10f;
    [SerializeField, Range(0f, 1f)] float pulseAmount = 0.25f;
    [SerializeField] float pulsePeriod = 4f;
    [SerializeField] float intensity = 2.6f;   // Additive라 1이면 낮의 밝은 땅 위에서 묻힌다 — 문양 빛을 키운다

    Renderer glyphRenderer;
    MaterialPropertyBlock block;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    float phase;

    void Awake()
    {
        glyphRenderer = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        phase = Random.value * Mathf.PI * 2f;   // 네 포탈이 같은 박자로 깜빡이지 않게
    }

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        if (glyphRenderer == null || pulseAmount <= 0f) return;
        float k = intensity * (1f - pulseAmount * 0.5f * (1f + Mathf.Sin(phase + Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, pulsePeriod))));
        glyphRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, new Color(k, k, k, 1f));
        glyphRenderer.SetPropertyBlock(block);
    }
}
