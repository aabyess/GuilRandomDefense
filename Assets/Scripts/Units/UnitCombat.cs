using UnityEngine;
using UnityEngine.AI;

// 유닛의 전투 상태 기계: 대기(Idle) → 추적(Chasing) → 복귀(Returning),
// 그리고 플레이어가 우클릭으로 이동을 명령한 동안은 자동 추적을 완전히 멈추는 PlayerMoving.
// 이 스크립트는 "누구를 쫓을지"와 "어디로 움직일지"만 결정한다 — 실제 데미지는 UnitAttacker가 처리하고,
// UnitAttacker는 이 스크립트가 고른 CurrentTarget을 그대로 쓴다(중복 탐색 방지).
[RequireComponent(typeof(NavMeshAgent))]
public class UnitCombat : MonoBehaviour
{
    enum CombatState { Idle, Chasing, Returning, PlayerMoving }

    [SerializeField] float aggroRange = 18f;
    [SerializeField] float scanInterval = 0.25f;
    [SerializeField] float arrivalThreshold = 0.3f;

    NavMeshAgent agent;
    UnitAttacker attacker;

    CombatState state = CombatState.Idle;
    EnemyDummy currentTarget;

    // 마지막 이동 명령 지점. 명령이 없었으면 스폰 위치(Awake 시점 위치)가 그대로 남는다.
    Vector3 commandedPosition;

    Vector3 lastSetDestination;
    bool hasDestination;
    float nextScanTime;

    // 사거리 안에 있을 때만 넘겨준다 — UnitAttacker가 "때릴 수 있는 대상"만 받도록.
    public EnemyDummy CurrentTarget
    {
        get
        {
            if (currentTarget == null) return null;

            float sqrDistance = (currentTarget.transform.position - transform.position).sqrMagnitude;
            float attackRangeSqr = AttackRangeSqr();
            return sqrDistance <= attackRangeSqr ? currentTarget : null;
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        attacker = GetComponent<UnitAttacker>();
        commandedPosition = transform.position;

        // 한 프레임에 모든 유닛이 몰려서 스캔하지 않도록 시작 시점을 흩어둔다.
        nextScanTime = Time.time + Random.Range(0f, scanInterval);
    }

    // UnitMover가 우클릭 이동 명령을 받으면 이걸 부른다. 도착할 때까지 자동 추적을 멈춘다.
    public void IssueMoveCommand(Vector3 destination)
    {
        commandedPosition = destination;
        currentTarget = null;
        state = CombatState.PlayerMoving;
        SetDestination(destination);
    }

    void Update()
    {
        switch (state)
        {
            case CombatState.PlayerMoving:
                if (HasArrived()) state = CombatState.Idle;
                break;

            case CombatState.Idle:
                TryScan();
                break;

            case CombatState.Chasing:
                UpdateChasing();
                break;

            case CombatState.Returning:
                // 복귀 중에도 계속 찾는다. 안 그러면 목표가 죽을 때마다 원위치까지 갔다가
                // 다시 나오기를 반복해서, 적이 줄줄이 오는 레인에서 유닛이 왕복만 한다.
                TryScan();
                if (state == CombatState.Returning && HasArrived()) state = CombatState.Idle;
                break;
        }
    }

    void TryScan()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;

        EnemyDummy target = FindClosestEnemyInAggro();
        if (target == null) return;

        currentTarget = target;
        state = CombatState.Chasing;

        float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
        SetDestination(sqrDistance <= AttackRangeSqr() ? transform.position : target.transform.position);
    }

    void UpdateChasing()
    {
        // 살아있는지·사거리 안인지도 스캔 주기에 맞춰서만 재확인한다 — 매 프레임 검사하지 않는다.
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;

        if (currentTarget == null)
        {
            BeginReturning();
            return;
        }

        float sqrDistance = (currentTarget.transform.position - transform.position).sqrMagnitude;
        if (sqrDistance > aggroRange * aggroRange)
        {
            currentTarget = null;
            BeginReturning();
            return;
        }

        float attackRangeSqr = AttackRangeSqr();
        SetDestination(sqrDistance <= attackRangeSqr ? transform.position : currentTarget.transform.position);
    }

    void BeginReturning()
    {
        state = CombatState.Returning;
        SetDestination(commandedPosition);
    }

    float AttackRangeSqr()
    {
        float range = attacker != null ? attacker.AttackRange : 0f;
        return range * range;
    }

    bool HasArrived()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arrivalThreshold);
    }

    // 목적지가 실제로 바뀌었을 때만 SetDestination을 부른다 — 매번 부르면 경로를 계속 다시 계산한다.
    void SetDestination(Vector3 destination)
    {
        if (hasDestination && (destination - lastSetDestination).sqrMagnitude < 0.01f) return;

        lastSetDestination = destination;
        hasDestination = true;
        agent.SetDestination(destination);
    }

    EnemyDummy FindClosestEnemyInAggro()
    {
        EnemyDummy closest = null;
        float closestSqrDistance = aggroRange * aggroRange;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;

            float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = enemy;
        }

        return closest;
    }
}
