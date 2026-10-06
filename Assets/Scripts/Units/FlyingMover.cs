using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 비행 유닛(movementAbility에 Flying — 이재윤·황준석·불멸 박은석·김용태 「바다이동·전지역이동」 포함)의 직선 이동.
/// 바다 NavMesh(y 0)와 섬 NavMesh(y 8)가 이어져 있지 않아 에이전트로는 섬 밖 바다로 못 나간다 — 이 유닛은 NavMeshAgent를 끄고
/// 목적지 한 점으로 직선으로 난다(설계: Docs/design/FLYINGMOVER_DESIGN_2026-10-06.md).
///  · 속도는 NavMeshAgent.speed 한 곳에서 읽는다(끈 에이전트도 속성은 읽힌다) — UnitSpawner의 moveSpeed와 UnitAttacker의 아군 이속 감소가 그 값을 그대로 쓴다.
///  · 높이(y)는 안 바꾼다 — 스폰 때 NavMesh에 올린 높이(섬 윗면) 그대로.
///  · 호스트(GameAuthority.IsServer)만 움직인다. 위치는 NetEntity가 복제한다(클라 거울에선 NetReplicaBuilder가 이 컴포넌트를 지운다).
/// UnitCombat이 에이전트 대신 이 컴포넌트의 SetDestination·Stop·HasArrived를 부른다.
/// </summary>
[DisallowMultipleComponent]
public class FlyingMover : MonoBehaviour
{
    const float TurnDegreesPerSecond = 1080f;   // UnitSpawner가 에이전트에 주는 회전과 같다
    const float MovingSpeedSqr = 0.25f;         // UnitFacing과 같은 「이동 중」 기준

    NavMeshAgent agent;
    Vector3 destination;
    bool hasDestination;
    Vector3 velocity;

    /// <summary>끈 에이전트에 남은 속도 값. 에이전트가 없으면 0(움직이지 않는다).</summary>
    float Speed => agent != null ? agent.speed : 0f;
    float StopDistance => Mathf.Max(agent != null ? agent.stoppingDistance : 0f, 0.3f);

    public bool IsMoving => velocity.sqrMagnitude > MovingSpeedSqr;
    public Vector3 Velocity => velocity;

    /// <summary>목적지에 닿았거나 목적지가 없으면 true.</summary>
    public bool HasArrived => !hasDestination || FlatDistance(destination) <= StopDistance;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void SetDestination(Vector3 point)
    {
        destination = ClampToWorld(point);
        hasDestination = true;
    }

    public void Stop()
    {
        hasDestination = false;
        velocity = Vector3.zero;
    }

    /// <summary>모으기·정렬(V·C) — 걸어오지 않고 그 자리로 옮겨 세운다(높이는 지금 높이 그대로).</summary>
    public void SnapTo(Vector3 position)
    {
        Vector3 p = ClampToWorld(position);
        transform.position = new Vector3(p.x, transform.position.y, p.z);
        Stop();
    }

    /// <summary>맵 범위(카메라가 갈 수 있는 범위 — MapGenerator가 섬 배치를 실측해 정한 값) 안으로 자른다. 범위를 못 구하면 그대로.</summary>
    public static Vector3 ClampToWorld(Vector3 point)
    {
        if (RtsCameraController.TryGetWorldBounds(out Vector2 min, out Vector2 max))
        {
            point.x = Mathf.Clamp(point.x, min.x, max.x);
            point.z = Mathf.Clamp(point.z, min.y, max.y);
        }
        return point;
    }

    float FlatDistance(Vector3 target)
    {
        Vector3 d = target - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void Update()
    {
        velocity = Vector3.zero;
        if (!GameAuthority.IsServer || !hasDestination) return;

        Vector3 to = destination - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance <= StopDistance) return;

        float step = Mathf.Min(Speed * Time.deltaTime, distance);
        Vector3 direction = to / distance;
        transform.position += direction * step;
        velocity = Time.deltaTime > 0f ? direction * (step / Time.deltaTime) : Vector3.zero;

        Quaternion goal = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, TurnDegreesPerSecond * Time.deltaTime);
    }
}
