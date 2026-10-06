using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 공격할 때 표적을 바라본다(사장님 10-04). 몸 방향은 NavMeshAgent가 **이동할 때만** 돌려서, 멈춰 서서 치는 유닛은 처음 보던 쪽만 봤다.
/// 순수 겉모습이다 — 표적은 UnitCombat.CurrentTarget(사거리 안 적)을 읽기만 하고, 공격 판정·주기·피해는 UnitAttacker가 그대로 한다.
/// - 루트 Y축만 돈다(피치·롤 0). 정면은 루트 +Z 규약이다(ArtBinder.FacingFixes는 모델 자식에 걸린 보정이라 충돌 없음).
/// - 움직이는 동안(agent 속도)엔 끼어들지 않는다 — 에이전트 회전(angularSpeed 1080°/s)에 맡긴다. 서 있을 땐 에이전트가 회전을 안 만진다.
/// - 호스트(·싱글)에서만 돈다. 멀티 클라 화면은 NetEntity가 호스트 실물의 회전을 실어 나른다 — 클라에서 따로 돌리면 어긋난다.
/// - 표적·에이전트는 그때그때 읽는다(Awake 캐시 금지 — UnitSpawner가 소환 뒤에 붙인다).
/// UnitSpawner.Spawn이 UnitCombat 있는 유닛에만 붙인다 — 조합판 인형·건물(상점)·흔함 칸 안 보관은 이 길을 안 거치거나 표적이 없다.
/// </summary>
public class UnitFacing : MonoBehaviour
{
    const float TurnDegreesPerSecond = 720f;   // 워크3처럼 빠르게
    const float MovingSpeedSqr = 0.25f;        // 이보다 빠르면 이동 중으로 본다(에이전트 회전에 맡김)

    void Update()
    {
        if (!GameAuthority.IsServer) return;
        if (!TryGetComponent(out UnitCombat combat)) return;
        EnemyDummy target = combat.CurrentTarget;
        if (target == null) return;

        if (TryGetComponent(out FlyingMover flyer) && flyer.IsMoving) return;   // 비행 유닛은 FlyingMover가 이동 방향으로 돌린다
        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude > MovingSpeedSqr) return;

        Vector3 toTarget = target.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion goal = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, TurnDegreesPerSecond * Time.deltaTime);
    }
}
