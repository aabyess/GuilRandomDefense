using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitMover : MonoBehaviour
{
    [SerializeField] float destinationSampleRadius = 2f;

    NavMeshAgent agent;
    Camera cam;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        cam = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryMoveToCursor();
        }
    }

    void TryMoveToCursor()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;
        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, destinationSampleRadius, NavMesh.AllAreas)) return;

        agent.SetDestination(navHit.position);
    }
}
