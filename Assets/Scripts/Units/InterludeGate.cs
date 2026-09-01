using UnityEngine;

// 특정 스토리 대기 구간(예: "백수생활")에만 열리는 포탈을 위한 게이트.
// 포탈이 UnitPortal인지 ResourcePortal인지 몰라도 된다 — 둘 다 OnTriggerEnter로 위습을
// 받으므로, 이 컴포넌트는 Collider를 껐다 켜서 트리거 자체가 안 불리게 막는다.
// 닫혔다는 게 눈에 보여야 하므로 닫힌 동안 색을 바꾼다.
[RequireComponent(typeof(Collider))]
public class InterludeGate : MonoBehaviour
{
    [SerializeField] string interludeName = "백수생활";
    [SerializeField] float checkInterval = 0.5f;

    [Header("닫혔을 때 색 — MaterialPropertyBlock으로 덮어쓴다(배칭 안 깨짐)")]
    [SerializeField] Color closedColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

    Collider gateCollider;
    Renderer gateRenderer;
    MaterialPropertyBlock propertyBlock;

    bool? lastOpen; // null = 아직 한 번도 반영 안 함 — 첫 틱에 무조건 상태를 적용한다.
    float nextCheckTime;

    void Awake()
    {
        gateCollider = GetComponent<Collider>();
        gateRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        // 상태 자체는 이 주기로만 다시 묻는다. 콜라이더·색 반영은 그중에서도 실제로
        // 바뀌었을 때만 — 도박 포탈 결과 표시와 같은 이유로 SetPropertyBlock을 매 프레임
        // 부르지 않는다.
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        bool open = StoryManager.Instance != null && StoryManager.Instance.IsInterlude(interludeName);
        if (lastOpen.HasValue && lastOpen.Value == open) return;

        lastOpen = open;
        Apply(open);
    }

    void Apply(bool open)
    {
        if (gateCollider != null) gateCollider.enabled = open;

        if (gateRenderer == null) return;

        if (open)
        {
            // 오버라이드를 지워서 원래 공유 머티리얼 색으로 되돌린다 — 인스턴스 머티리얼을
            // 만들면 포탈끼리 배칭이 깨진다.
            gateRenderer.SetPropertyBlock(null);
        }
        else
        {
            gateRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", closedColor);
            gateRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
