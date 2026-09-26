using UnityEngine;
using UnityEngine.AI;

// 유닛의 전투 상태 기계: 대기(Idle) → 추적(Chasing) → 복귀(Returning),
// 그리고 플레이어가 우클릭으로 이동을 명령한 동안은 자동 추적을 완전히 멈추는 PlayerMoving.
// 이 스크립트는 "누구를 쫓을지"와 "어디로 움직일지"만 결정한다 — 실제 데미지는 UnitAttacker가 처리하고,
// UnitAttacker는 이 스크립트가 고른 CurrentTarget을 그대로 쓴다(중복 탐색 방지).
[RequireComponent(typeof(NavMeshAgent))]
public class UnitCombat : MonoBehaviour
{
    // Holding은 플레이어가 H로 걸어두는 상태다. 적이 눈앞에 와도 자리를 뜨지 않는다 —
    // 문 앞이나 길목을 지켜야 할 때 유닛이 적을 쫓아 흩어지는 걸 막는 용도다.
    // ✅ 2026-09-26 베타 피드백 「H가 홀드가 아니라 정지」: 예전 홀드는 **공격도 안 했다.** 이제 워크3 홀드처럼
    //    자리는 안 뜨되 **사거리 안의 적은 친다.** 정지(S)는 따로 있다(Stop).
    // AttackMoving은 A+땅 클릭(공격 이동) — 목적지로 가면서 만나는 적을 치고, 다 치면 다시 목적지로 간다.
    enum CombatState { Idle, Chasing, Returning, PlayerMoving, Holding, AttackMoving }

    // ⚠️ 지금은 사실상 안 쓰인다(2026-09-05 확인). SearchRange()가 max(aggroRange, attackRange)를
    // 쓰는데, 전투 유닛 최소 사거리가 30(로스터 239종 실측, 0인 건 초월위습 재료 1종뿐 — 안 싸움)
    // 이라 이 값이 이긴 적이 없다. 18을 40으로 올려도 아무 일도 안 일어난다 — 사거리보다 넓은
    // 감지 범위가 필요해지면(예: 사거리 짧은 유닛도 멀리서 미리 알아채게) 30보다 큰 값을 넣어야
    // 그때부터 의미가 생긴다. 지우지 말 것 — 다음에 그 자리로 쓸 수 있다.
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

    // A+적 클릭으로 찍은 표적. 이 적은 탐색 범위를 벗어나도 끝까지 쫓는다(죽거나 다른 명령이 올 때까지).
    EnemyDummy forcedTarget;
    // A+땅 클릭(공격 이동) 중인가, 그 목적지.
    bool attackMoving;
    Vector3 attackMoveDestination;

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

    public bool IsHolding => state == CombatState.Holding;

    /// <summary>H 키. 켜면 그 자리에 못박히고(사거리 안의 적은 친다), 끄면 원래대로 적을 쫓는다.</summary>
    public void SetHold(bool hold)
    {
        if (hold)
        {
            ClearOrders();
            currentTarget = null;
            commandedPosition = transform.position;
            state = CombatState.Holding;

            // 가던 길을 즉시 끊는다. ResetPath만 부르면 남은 속도로 미끄러진다.
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            hasDestination = false;
            return;
        }

        if (state == CombatState.Holding) state = CombatState.Idle;
    }

    /// <summary>모으기(V). 걸어오지 않고 그 자리로 옮겨 세운다 — 원작이 그렇게 동작한다.
    /// NavMesh에 못 올리면(자리 없음) 아무것도 안 바꾸고 false를 돌려준다 — 호출부가 실패를
    /// 알아야 할 때(예: StoryReturnPortal) 쓴다. 기존 호출부(UnitCommands)는 반환값을
    /// 그냥 무시해도 기존 동작 그대로다.</summary>
    public bool SnapTo(Vector3 position)
    {
        // NavMesh 위로 끌어다 놓는다. 좌표를 그대로 믿고 Warp하면, 그 자리에 길이 안 깔려
        // 있을 때 에이전트가 NavMesh에서 떨어져 나가고 그 뒤로는 이동 명령이 조용히 무시된다.
        // 모으기(V)·정렬(C)이 우리 뒷줄처럼 가장자리 자리를 지목할 수 있어 실제로 닿는 위험이다.
        if (!NavPlacement.Place(agent, position))
        {
            // 못 올렸으면 옮기지 않는다. 억지로 옮기면 움직일 수 없는 유닛이 되는데,
            // 그건 제자리에 남는 것보다 나쁘다 — 선택은 되는데 명령만 안 먹는다.
            Debug.Log($"[명령] {name}: {position} 근처에 설 자리가 없어 옮기지 않았습니다.", this);
            return false;
        }

        // 옮겨놓기만 하면 복귀 지점이 예전 자리로 남아, 적을 쫓고 나서 다시 흩어진다.
        ClearOrders();
        commandedPosition = agent.transform.position;
        currentTarget = null;
        state = CombatState.Idle;
        hasDestination = false;
        return true;
    }

    /// <summary>S 키(정지). 하던 이동·추적을 끊고 그 자리에 선다. 홀드와 달리 적이 오면 다시 쫓는다.</summary>
    public void Stop()
    {
        ClearOrders();
        currentTarget = null;
        commandedPosition = transform.position;
        state = CombatState.Idle;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
        hasDestination = false;
        nextScanTime = Time.time + scanInterval;   // 선 그 프레임에 바로 다시 뛰어나가지 않게 한 박자 쉰다
    }

    /// <summary>A + 적 클릭(또는 적에게 우클릭). 이 적을 끝까지 쫓아 친다.</summary>
    public void AttackTarget(EnemyDummy enemy)
    {
        if (enemy == null) return;

        ClearOrders();
        forcedTarget = enemy;
        currentTarget = enemy;
        state = CombatState.Chasing;

        float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
        hasDestination = false;
        SetDestination(sqrDistance <= AttackRangeSqr() ? transform.position : enemy.transform.position);
        nextScanTime = Time.time + scanInterval;
    }

    /// <summary>A + 땅 클릭(공격 이동). 목적지로 가면서 만나는 적을 치고, 끝나면 다시 목적지로 간다.</summary>
    public void AttackMove(Vector3 destination)
    {
        ClearOrders();
        currentTarget = null;
        attackMoving = true;
        attackMoveDestination = destination;
        commandedPosition = destination;
        state = CombatState.AttackMoving;
        hasDestination = false;
        SetDestination(destination);
        nextScanTime = 0f;
    }

    void ClearOrders()
    {
        forcedTarget = null;
        attackMoving = false;
    }

    // UnitMover가 우클릭 이동 명령을 받으면 이걸 부른다. 도착할 때까지 자동 추적을 멈춘다.
    public void IssueMoveCommand(Vector3 destination)
    {
        ClearOrders();
        commandedPosition = destination;
        currentTarget = null;
        state = CombatState.PlayerMoving;
        SetDestination(destination);
    }

    void Update()
    {
        switch (state)
        {
            case CombatState.Holding:
                UpdateHolding();   // 자리는 안 뜨고, 사거리 안의 적만 친다
                break;

            case CombatState.AttackMoving:
                TryScan();         // 적을 찾으면 Chasing으로 바뀐다(공격 이동은 표시로 남는다)
                if (state == CombatState.AttackMoving && HasArrived())
                {
                    attackMoving = false;
                    state = CombatState.Idle;
                }
                break;

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

    // 표적을 새로 잡았으면 true.
    bool TryScan()
    {
        if (Time.time < nextScanTime) return false;
        nextScanTime = Time.time + scanInterval;

        EnemyDummy target = FindClosestEnemyInAggro();
        if (target == null) return false;

        currentTarget = target;
        state = CombatState.Chasing;

        float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
        SetDestination(sqrDistance <= AttackRangeSqr() ? transform.position : target.transform.position);
        return true;
    }

    // 홀드: 움직이지 않는다. 지금 표적이 사거리 밖으로 나가면 놓고, 주기마다 사거리 안에서 가장 가까운 적을 잡는다.
    void UpdateHolding()
    {
        if (currentTarget != null)
        {
            float sqrToTarget = (currentTarget.transform.position - transform.position).sqrMagnitude;
            if (sqrToTarget <= AttackRangeSqr()) return;
            currentTarget = null;
            nextScanTime = 0f;
        }

        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;
        currentTarget = FindClosestEnemyWithin(attacker != null ? attacker.AttackRange : 0f);
    }

    void UpdateChasing()
    {
        // 🔴 2026-09-24 — **표적을 잃었는지는 매 프레임 본다. 스캔 주기를 기다리지 않는다.**
        //    예전엔 이 검사까지 0.25초마다 했다. 그래서 적이 죽을 때마다:
        //      0.25초(죽은 걸 알아채기) + 0.25초(다음 표적 찾기) = **0.5초를 논다.**
        //    실측(09-24, outbox 2111): R2에서 **사거리 안에 적이 평균 8.4마리 있는데
        //    표적 없는 시간이 40초**였고 그동안 상태가 **Idle 50% · Chasing 49%**였다.
        //    적이 줄줄이 오는 게임에서 1초에 한 마리를 잡으면 **가동률의 절반을 여기서 잃는다.**
        //
        //    비싼 것과 싼 것을 갈랐다:
        //      싼 것(매 프레임)  — 내 표적이 죽었나 · 사거리 밖으로 나갔나. null 검사 + 거리 하나
        //      비싼 것(0.25초)   — 적 전체를 훑어 새 표적 찾기(FindClosestEnemyInAggro)
        //    표적을 잃은 **그 프레임에** 다음 스캔이 돌도록 nextScanTime을 푼다 —
        //    잃은 직후가 바로 다시 찾아야 하는 순간이고, 거기서 기다리면 안 된다.
        bool lostTarget = currentTarget == null;
        if (!lostTarget)
        {
            float sqrToTarget = (currentTarget.transform.position - transform.position).sqrMagnitude;
            // A로 찍은 표적은 탐색 범위를 벗어나도 놓지 않는다 — 그걸 치라고 찍은 것이다.
            bool forced = forcedTarget != null && currentTarget == forcedTarget;
            if (!forced && sqrToTarget > SearchRange() * SearchRange())
            {
                currentTarget = null;
                lostTarget = true;
            }
            else
            {
                // 표적이 살아 있고 사거리 판정이 필요한 동안에도 경로 갱신은 주기대로만 한다 —
                // 매 프레임 SetDestination을 부르면 경로를 계속 다시 계산한다.
                if (Time.time < nextScanTime) return;
                nextScanTime = Time.time + scanInterval;

                SetDestination(sqrToTarget <= AttackRangeSqr() ? transform.position : currentTarget.transform.position);
                return;
            }
        }

        // 잃은 **그 프레임에** 다시 찾는다. 찾으면 복귀조차 안 한다 —
        // 복귀를 먼저 시키면 제자리로 한 걸음 갔다가 다시 나오는 왕복이 생긴다.
        // ⚠️ 2026-09-26: 예전엔 `TryScan(); if (state == Chasing) return;`이었는데, 이 함수 안에서는 state가
        //    **이미 Chasing**이라 못 찾아도 늘 return했다 — 유닛이 복귀하지 않고 매 프레임 전체 탐색만 돌았다.
        //    이제 TryScan이 「새로 잡았나」를 돌려준다.
        forcedTarget = null;
        nextScanTime = 0f;
        if (TryScan()) return;

        if (attackMoving)
        {
            // 공격 이동 중이었으면 다시 목적지로 간다.
            state = CombatState.AttackMoving;
            hasDestination = false;
            SetDestination(attackMoveDestination);
            return;
        }

        BeginReturning();
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

    // 적을 찾고 쫓는 범위. 사거리보다 좁으면 안 된다 — 좁으면 쏠 수 있는 거리의 적을
    // 아예 못 보고, 이미 잡은 표적도 "멀어졌다"며 버리고 제자리로 돌아간다.
    // 사거리는 유닛마다 다르고(흔함 30 ~ 초월 47.5) 강화로 더 늘어나므로 고정값을 쓸 수 없다.
    float SearchRange()
    {
        return attacker != null ? Mathf.Max(aggroRange, attacker.AttackRange) : aggroRange;
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

    /// <summary>
    /// ⚠️ **레인으로 거르지 않는다. 무적으로도 거르지 않는다. 일부러 그렇다.**
    ///
    /// 2026-09-24에 「유닛이 스토리 건물을 물고 레인 적을 안 본다」를 결함으로 고칠 뻔했다.
    /// 그런데 `StoryManager.TransformIntoBoss`가 이렇게 말한다:
    ///     「체력은 건드리지 않는다 — **지금까지 깎아둔 만큼이 그대로 보스 체력이 된다**」
    ///     「나온 그 라운드에 바로 변신하지는 않는다 — 때릴 틈도 없이 보스가 되면
    ///      **『미리 깎아둔다』는 구조가 성립하지 않는다**」
    /// **미리 깎아두는 것이 설계다.** 무적(변신 전)이라 1까지만 깎이고, 그 1이 보스 체력이 된다.
    /// 그러니 스토리 건물을 자동으로 때리는 건 고쳐야 할 것이 아니라 **쓰라고 만든 것**이다.
    ///
    /// 실측 거리(구현담당1, 레인1 모서리 배치 기준):
    /// ```
    /// 스토리 건물   1,759.9   ← 스토리존 섬이다. **레인 유닛은 못 닿는다**
    /// 해적단 미니보스  312.2   ← 레인 **한가운데**에 선다(PirateQuestManager:109). 사거리 320+만 닿음
    /// 물범 2,172 · 거대 해왕류 1,545  ← 어떤 사거리로도 안 닿는다
    /// ```
    /// 즉 유닛이 스토리 건물을 무는 건 **플레이어가 거기까지 걸어갔을 때뿐**이고, 그건 선택이다
    /// (그동안 레인이 빈다 — 그게 대가다).
    ///
    /// 🔴 한 가지만 기억할 것: **레인 한가운데에 세운 유닛은 미니보스와 거리 0**이다.
    /// 조합 결과가 나오는 자리가 거기다. 미니보스가 있는 동안 그 유닛들은 레인 적을 안 본다.
    /// 미니보스는 체력 12,000이고 **죽는다** — 영영 물고 있지는 않다.
    /// </summary>
    EnemyDummy FindClosestEnemyInAggro() => FindClosestEnemyWithin(SearchRange());

    EnemyDummy FindClosestEnemyWithin(float search)
    {
        EnemyDummy closest = null;
        float closestSqrDistance = search * search;

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
