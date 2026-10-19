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
    public const string SpellParam = "Spell";   // 원작 스킨 교체(10-09): 스킬 시전 동작
    // 같은 이름 변형 클립(Attack / Attack 2…, Spell / Spell 2…)을 무작위로 고르는 Int 파라미터(원작 워크3 방식, 10-14). 변형이 둘 이상일 때만 컨트롤러에 있다.
    public const string AttackVariantParam = "AttackVariant";
    public const string SpellVariantParam = "SpellVariant";

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
    static readonly int SpellHash = Animator.StringToHash(SpellParam);

    static readonly int AttackVariantHash = Animator.StringToHash(AttackVariantParam);
    static readonly int SpellVariantHash = Animator.StringToHash(SpellVariantParam);

    bool hasSpeed, hasAttack, hasDie, hasSpell, hasAttackVariant, hasSpellVariant;
    int attackVariantCount = 1, spellVariantCount = 1;
    float lastAttackTime = -10f;
    // 공격 모션을 공속에 맞춘다(사장님 10-16): 공속이 빨라 타격 주기가 모션보다 짧으면 매 타격마다 모션을 처음부터 다시 재생하고(끝까지 안 마쳐도 됨),
    // 그 사이 재생 속도를 올려 잘림을 줄인다. 느린 유닛(주기 ≥ 모션)은 지금처럼 한 번 재생 후 대기.
    float attackClipLength;                 // 컨트롤러의 Attack 계열 클립 중 가장 긴 것(초). 0이면 모름
    float attackPlaybackBoost = 1f;
    const float MaxAttackPlaybackSpeed = 2.5f;

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
            else if (parameter.name == SpellParam) hasSpell = true;
            else if (parameter.name == AttackVariantParam) hasAttackVariant = true;
            else if (parameter.name == SpellVariantParam) hasSpellVariant = true;
        }
        // 변형 개수 = 컨트롤러 클립 중 「Attack」「Attack 2」… 꼴(상태 순서는 컨트롤러가 번호순으로 짠다).
        if (hasAttackVariant) attackVariantCount = CountVariants("Attack");
        attackClipLength = 0f;   // 클립 이름은 「mixamo.com」일 수 있어 상태 이름으로 못 찾는다 — 공격 상태에 들어가 보며 Update가 배운다(LearnAttackLength)
        if (hasSpellVariant) spellVariantCount = CountVariants("Spell");
    }

    void Update()
    {
        if (animator == null || !hasSpeed) return;

        float speed = CurrentSpeed();
        animator.SetFloat(SpeedHash, Mathf.Clamp01(speed / Mathf.Max(0.01f, runSpeed)));

        // 걷는 중(Move 상태)일 때만 재생 속도를 맞춘다. 서 있거나 공격할 때는 원래 속도.
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);   // 한 프레임에 두 번 읽던 것 — animator.speed를 바꿔도 같은 프레임 상태 정보는 같다
        LearnAttackLength(stateInfo);
        bool walking = speed / Mathf.Max(0.01f, runSpeed) > 0.15f;
        animator.speed = walking && bodyHeight > 0.01f
            ? Mathf.Clamp(speed / bodyHeight / walkClipHeightsPerSecond, 1f, maxWalkPlaybackSpeed)
            : (attackPlaybackBoost > 1.001f && IsAttackState(stateInfo) ? attackPlaybackBoost : 1f);
    }

    // 공격 상태의 실제 길이(초, 상태 속도 반영)를 처음 보는 순간 배운다 — 첫 타격은 보정 없이 한 번 재생된다.
    static readonly System.Collections.Generic.List<AnimatorClipInfo> clipInfoScratch = new System.Collections.Generic.List<AnimatorClipInfo>(4);

    void LearnAttackLength(AnimatorStateInfo info)
    {
        if (!IsAttackState(info)) return;
        // info.speed = 상태 자체의 속도(Animator.speed 보정은 안 섞인다, 10-16 실측), clip.length = 보정과 무관한 원본 길이.
        animator.GetCurrentAnimatorClipInfo(0, clipInfoScratch);   // 배열 반환형은 부를 때마다 새 배열 — 공격 상태 동안 유닛마다 매 프레임이라 목록 재사용형으로
        if (clipInfoScratch.Count == 0 || clipInfoScratch[0].clip == null) return;
        float stateSpeed = Mathf.Max(0.01f, info.speed);
        float duration = clipInfoScratch[0].clip.length / stateSpeed;
        if (duration > 0.05f && duration < 20f) attackClipLength = duration;
    }

    bool IsAttackState(AnimatorStateInfo info)
    {
        if (info.IsName("Attack")) return true;
        for (int i = 2; i <= attackVariantCount; i++)
            if (info.IsName("Attack " + i)) return true;
        return false;
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

    /// <summary>
    /// 몸(모델)을 통째로 바꾼 뒤 부른다(불멸 「유닛회유」: 회유된 적의 원래 몸을 복제해 붙임) — 새 Animator를 잡고 파라미터·키를 다시 잰다.
    /// </summary>
    public void RebindBody(Animator newAnimator)
    {
        animator = newAnimator;
        hasSpeed = hasAttack = hasDie = hasSpell = hasAttackVariant = hasSpellVariant = false;
        attackVariantCount = spellVariantCount = 1;
        bodyHeight = MeasureBodyHeight();
        CacheParameters();
    }

    /// <summary>공격이 나갈 때 부른다.</summary>
    public void PlayAttack()
    {
        float interval = Time.time - lastAttackTime;   // 이전 타격과의 간격 = 이 유닛의 실제 공격 주기(처음·오랜만이면 크다)
        lastAttackTime = Time.time;
        if (animator != null && hasAttack)
        {
            int variant = hasAttackVariant && attackVariantCount > 1 ? Random.Range(0, attackVariantCount) : 0;
            if (hasAttackVariant && attackVariantCount > 1) animator.SetInteger(AttackVariantHash, variant);

            // 주기가 모션보다 짧으면 재생을 빠르게(상한 있음). 아니면 원래 속도.
            attackPlaybackBoost = attackClipLength > 0.01f && interval > 0.05f && interval < attackClipLength
                ? Mathf.Min(MaxAttackPlaybackSpeed, attackClipLength / interval)
                : 1f;

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = animator.IsInTransition(0);
            AnimatorStateInfo next = inTransition ? animator.GetNextAnimatorStateInfo(0) : current;
            bool attackingNow = IsAttackState(current) || (inTransition && IsAttackState(next));
            if (attackingNow && attackPlaybackBoost > 1.001f)
            {
                // 아직 앞 모션이 안 끝났는데 다음 타격 — 트리거는 canTransitionToSelf=false라 앞 모션이 끝난 뒤에야 먹는다. 바로 처음부터 다시 재생한다.
                animator.ResetTrigger(AttackHash);
                if (attackVariantCount > 1) animator.Play(variant == 0 ? "Attack" : "Attack " + (variant + 1), 0, 0f);
                else animator.Play((inTransition && IsAttackState(next) ? next : current).fullPathHash, 0, 0f);
            }
            else animator.SetTrigger(AttackHash);
        }
        AttackPlayed?.Invoke(); // MP
    }

    // MP: 멀티 호스트의 거울(NetEntity)이 구독해 공격 횟수를 클라로 보낸다 — 클라 겉모습이 PlayAttack을 다시 부른다.
    //     싱글에선 구독자가 없다.
    public event System.Action AttackPlayed;

    /// <summary>스킬을 쏠 때 부른다(UnitAttacker.CastSkillLevel). 컨트롤러에 Spell이 없으면 아무 일도 없다. 평타 직후(0.45초 안)에 터지는 확률·평타 부가 스킬은 평타 동작을 끊지 않게 건너뛴다.</summary>
    public void PlaySpell()
    {
        if (animator == null || !hasSpell || Time.time - lastAttackTime < 0.45f) return;
        if (hasSpellVariant && spellVariantCount > 1) animator.SetInteger(SpellVariantHash, Random.Range(0, spellVariantCount));
        animator.SetTrigger(SpellHash);
    }

    int CountVariants(string word)
    {
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
        {
            string n = c.name;
            if (n == word) names.Add(n);
            else if (n.StartsWith(word + " ") && int.TryParse(n.Substring(word.Length + 1), out _)) names.Add(n);
        }
        return Mathf.Max(1, names.Count);
    }

    /// <summary>죽을 때 부른다. 오브젝트가 바로 파괴되면 재생될 틈이 없으니, 죽음 처리보다 먼저 부를 것.</summary>
    public void PlayDeath()
    {
        if (animator != null && hasDie) animator.SetTrigger(DieHash);
    }
}
