using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 모델에 붙은 Animator를 게임 상태에 맞춰 돌린다. 아군·적 양쪽에 같은 컴포넌트를 쓴다 —
/// 둘 다 "얼마나 빨리 움직이나 / 방금 때렸나 / 죽었나" 셋으로 설명되고,
/// 이동 방식만 NavMeshAgent냐 WaypointMover냐로 갈린다.
///
/// Animator가 없으면(지금의 큐브 프리팹) 아무 일도 하지 않는다. 모델을 붙이는 순간부터 동작한다.
/// </summary>
public class CharacterAnimator : MonoBehaviour
{
    // Animator 파라미터 이름. Mixamo에서 받은 클립을 이 이름의 상태에 연결한다.
    public const string SpeedParam = "Speed";
    public const string AttackParam = "Attack";
    public const string DieParam = "Die";

    [SerializeField] Animator animator;
    // 이동 속도를 0~1로 접어 넣는 기준. 이보다 빠르면 전력 이동으로 본다.
    [SerializeField] float runSpeed = 6f;
    // 프레임마다 값이 튀면 걷기와 대기를 오간다. 조금 눅여서 넘긴다.
    [SerializeField] float speedSmoothing = 10f;

    // 2026-09-26 베타 피드백 「빙판 미끄러지듯」 — 걷기 클립 하나(Mixamo walk, 사람 1.75m가 초속 약 1.4m =
    // 초당 키의 0.8배를 간다)로 모두 걷는데, 실제 이동은 초당 키의 3~6배라 **발이 땅 위를 미끄러졌다.**
    // 걷는 동안만 재생 속도를 「실제 속도 ÷ 걷기 속도」로 올려 발과 땅을 맞춘다. 키는 몸 경계로 잰다 —
    // 거인은 보폭이 길어서 같은 속도라도 천천히 걷는다. 너무 빠르면 발놀림이 우스워지니 상한을 둔다.
    [SerializeField] float walkClipHeightsPerSecond = 0.8f;
    [SerializeField] float maxWalkPlaybackSpeed = 3f;
    float bodyHeight;

    NavMeshAgent agent;
    WaypointMover mover;
    Vector3 lastPosition;
    float smoothedSpeed;

    static readonly int SpeedHash = Animator.StringToHash(SpeedParam);
    static readonly int AttackHash = Animator.StringToHash(AttackParam);
    static readonly int DieHash = Animator.StringToHash(DieParam);

    bool hasSpeed, hasAttack, hasDie;

    void Awake()
    {
        // 모델은 자식으로 붙으므로 자식까지 뒤진다.
        if (animator == null) animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();
        mover = GetComponent<WaypointMover>();
        lastPosition = transform.position;
        bodyHeight = MeasureBodyHeight();

        CacheParameters();
    }

    // 없는 파라미터에 값을 쓰면 Animator가 매번 경고를 뱉는다. 컨트롤러가 무엇을 받는지 미리 본다.
    void CacheParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == SpeedParam) hasSpeed = true;
            else if (parameter.name == AttackParam) hasAttack = true;
            else if (parameter.name == DieParam) hasDie = true;
        }
    }

    void Update()
    {
        if (animator == null || !hasSpeed) return;

        float speed = CurrentSpeed();
        animator.SetFloat(SpeedHash, Mathf.Clamp01(speed / Mathf.Max(0.01f, runSpeed)));

        // 걷는 중(Move 상태)일 때만 재생 속도를 맞춘다. 서 있거나 공격할 때는 원래 속도.
        bool walking = speed / Mathf.Max(0.01f, runSpeed) > 0.15f;
        animator.speed = walking && bodyHeight > 0.01f
            ? Mathf.Clamp(speed / bodyHeight / walkClipHeightsPerSecond, 1f, maxWalkPlaybackSpeed)
            : 1f;
    }

    // 몸 키(월드 단위). 스폰 직후 T자·대기 자세 경계라 조금 오차가 있어도 재생 속도에는 충분하다.
    float MeasureBodyHeight()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer r in renderers)
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any ? bounds.size.y : 0f;
    }

    float CurrentSpeed()
    {
        // NavMeshAgent는 자기 속도를 알고 있다. WaypointMover는 transform을 직접 옮기므로
        // 실제로 얼마나 움직였는지 재는 수밖에 없다.
        float raw;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            raw = agent.velocity.magnitude;
        }
        else
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            raw = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        }

        smoothedSpeed = Mathf.Lerp(smoothedSpeed, raw, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));
        return smoothedSpeed;
    }

    /// <summary>공격이 나갈 때 부른다.</summary>
    public void PlayAttack()
    {
        if (animator != null && hasAttack) animator.SetTrigger(AttackHash);
    }

    /// <summary>죽을 때 부른다. 오브젝트가 바로 파괴되면 재생될 틈이 없으니, 죽음 처리보다 먼저 부를 것.</summary>
    public void PlayDeath()
    {
        if (animator != null && hasDie) animator.SetTrigger(DieHash);
    }
}
