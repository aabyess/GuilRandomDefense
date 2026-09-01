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

        animator.SetFloat(SpeedHash, Mathf.Clamp01(CurrentSpeed() / Mathf.Max(0.01f, runSpeed)));
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
