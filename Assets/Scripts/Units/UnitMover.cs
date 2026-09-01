using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitMover : MonoBehaviour
{
    [SerializeField] float destinationSampleRadius = 2f;

    NavMeshAgent agent;
    Camera cam;
    OwnedByPlayer owner;
    Selectable selectable;
    UnitCombat combat;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        cam = Camera.main;
        owner = GetComponent<OwnedByPlayer>();
        selectable = GetComponent<Selectable>();
        combat = GetComponent<UnitCombat>();
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;
        if (owner != null && owner.OwnerId != LocalPlayer.LocalPlayerId) return;
        // 선택된 유닛만 움직인다. 이 검사가 없으면 우클릭 한 번에 내 유닛 전부가 같이 이동해서
        // 선택 자체가 의미를 잃는다.
        if (selectable != null && !selectable.IsSelected) return;
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryMoveToCursor();
        }
    }

    void TryMoveToCursor()
    {
        // 이동 명령이 조용히 실패하면 "클릭이 안 먹는다"로만 보인다. 막힌 지점을 말하게 한다.
        if (!WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            Debug.Log($"[이동] {name}: 커서 아래에 아무것도 없습니다(콜라이더 없음).", this);
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.Log($"[이동] {name}: NavMesh 위에 서 있지 않아 움직일 수 없습니다 " +
                      $"(위치 {transform.position}, 에이전트 켜짐 {agent.isActiveAndEnabled}).", this);
            return;
        }

        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, destinationSampleRadius, agent.areaMask))
        {
            Debug.Log($"[이동] {name}: 클릭한 곳({hit.collider.name} {hit.point})에서 " +
                      $"{destinationSampleRadius} 안에 걸어갈 수 있는 자리가 없습니다.", this);
            return;
        }

        // UnitCombat이 있으면 그쪽에 명령을 넘겨서, 도착할 때까지 자동 추적이 끼어들지 않게 한다.
        if (combat != null)
            combat.IssueMoveCommand(navHit.position);
        else if (!agent.SetDestination(navHit.position))
            Debug.Log($"[이동] {name}: 목적지 {navHit.position} 로 가는 길을 못 찾았습니다.", this);
    }
}
