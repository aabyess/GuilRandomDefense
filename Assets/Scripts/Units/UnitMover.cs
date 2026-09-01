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
        if (!WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit)) return;
        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, destinationSampleRadius, agent.areaMask)) return;

        // UnitCombat이 있으면 그쪽에 명령을 넘겨서, 도착할 때까지 자동 추적이 끼어들지 않게 한다.
        if (combat != null)
            combat.IssueMoveCommand(navHit.position);
        else
            agent.SetDestination(navHit.position);
    }
}
