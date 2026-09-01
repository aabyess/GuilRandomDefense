using UnityEngine;

// 특정 스토리 대기 구간(예: "백수생활")에만 열리는 포탈을 위한 게이트.
// 포탈이 UnitPortal인지 ResourcePortal인지 몰라도 된다 — 둘 다 OnTriggerEnter로 위습을
// 받으므로, 이 컴포넌트는 Collider를 껐다 켜서 트리거 자체가 안 불리게 막는다.
// 닫혔다는 게 눈에 보여야 하므로 닫힌 동안 색을 바꾼다.
//
// choiceTrackedWispData를 채우면 "셋 중 하나만 고르는" 선택 칸으로도 쓸 수 있다.
// 콜라이더는 여전히 구간 여부로만 결정한다(공유 물리 상태라 "내가 골랐다"로 끄면
// 아직 안 고른 다른 플레이어까지 막힌다) — 대신 로컬 플레이어가 이미 골랐으면 색을
// 다르게 보여준다. 포탈끼리는 서로 모른다. 전부 Wisp.OnConsumed 하나만 구독해서,
// 그 위습이 자기 것과 같은 종류이고 로컬 플레이어 소유면 "이미 선택함"으로 넘어간다.
[RequireComponent(typeof(Collider))]
public class InterludeGate : MonoBehaviour
{
    [SerializeField] string interludeName = "백수생활";
    [SerializeField] float checkInterval = 0.5f;

    [Header("닫혔을 때 색 — MaterialPropertyBlock으로 덮어쓴다(배칭 안 깨짐)")]
    [SerializeField] Color closedColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

    [Header("한 번만 고르는 선택지 — 비우면 이 기능 없이 구간 개폐만 한다")]
    [SerializeField] WispData choiceTrackedWispData;
    [SerializeField] Color alreadyChosenColor = new Color(0.25f, 0.2f, 0.5f, 0.6f);

    enum VisualState { Open, ClosedByInterlude, ClosedByChoice }

    Collider gateCollider;
    Renderer gateRenderer;
    MaterialPropertyBlock propertyBlock;

    bool interludeOpen;
    bool hasInterludeState; // 첫 틱 전에는 콜라이더를 함부로 안 건드리려고 둔다.
    bool localPlayerAlreadyChose;
    VisualState? lastAppliedState;

    float nextCheckTime;

    void Awake()
    {
        gateCollider = GetComponent<Collider>();
        gateRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (choiceTrackedWispData != null)
            Wisp.OnConsumed += HandleWispConsumed;
    }

    void OnDisable()
    {
        Wisp.OnConsumed -= HandleWispConsumed;
    }

    void Update()
    {
        // 구간 여부는 이 주기로만 다시 묻는다. 콜라이더·색 반영은 그중에서도 실제로
        // 바뀌었을 때만 — 도박 포탈 결과 표시와 같은 이유로 SetPropertyBlock을 매 프레임
        // 부르지 않는다.
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        interludeOpen = StoryManager.Instance != null && StoryManager.Instance.IsInterlude(interludeName);
        hasInterludeState = true;

        RefreshVisual();
    }

    // 매 프레임이 아니라 위습이 실제로 소모된 그 순간에만 불린다.
    void HandleWispConsumed(Wisp wisp)
    {
        if (localPlayerAlreadyChose) return;
        if (wisp == null || wisp.Data != choiceTrackedWispData) return;
        if (!wisp.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId != LocalPlayer.LocalPlayerId) return;

        localPlayerAlreadyChose = true;
        RefreshVisual();
    }

    void RefreshVisual()
    {
        // 콜라이더는 구간 여부로만 결정한다 — "이미 골랐다"는 로컬 전용 표시라 물리에는 안 섞는다.
        if (gateCollider != null && hasInterludeState) gateCollider.enabled = interludeOpen;

        VisualState state = localPlayerAlreadyChose
            ? VisualState.ClosedByChoice
            : (hasInterludeState && interludeOpen ? VisualState.Open : VisualState.ClosedByInterlude);

        if (lastAppliedState.HasValue && lastAppliedState.Value == state) return;
        lastAppliedState = state;

        ApplyColor(state);
    }

    void ApplyColor(VisualState state)
    {
        if (gateRenderer == null) return;

        if (state == VisualState.Open)
        {
            // 오버라이드를 지워서 원래 공유 머티리얼 색으로 되돌린다 — 인스턴스 머티리얼을
            // 만들면 포탈끼리 배칭이 깨진다.
            gateRenderer.SetPropertyBlock(null);
            return;
        }

        Color color = state == VisualState.ClosedByChoice ? alreadyChosenColor : closedColor;
        gateRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        gateRenderer.SetPropertyBlock(propertyBlock);
    }
}
