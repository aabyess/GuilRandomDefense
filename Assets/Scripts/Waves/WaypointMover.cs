using UnityEngine;

/// <summary>
/// WaypointPath가 정의한 포인트들을 순서대로 직선 이동하며,
/// 마지막 포인트에 도달하면 다시 첫 포인트부터 순환한다.
/// </summary>
public class WaypointMover : MonoBehaviour
{
    [SerializeField] private WaypointPath path;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float arrivalThreshold = 0.05f;

    private int currentIndex;

    public void SetPath(WaypointPath newPath)
    {
        path = newPath;
    }

    private void Start()
    {
        if (path == null || path.PointCount == 0) return;

        transform.position = path.GetPoint(0);
        currentIndex = path.PointCount > 1 ? 1 : 0;
    }

    private void Update()
    {
        if (path == null || path.PointCount == 0) return;

        Vector3 target = path.GetPoint(currentIndex);
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= arrivalThreshold)
        {
            currentIndex = (currentIndex + 1) % path.PointCount;
        }
    }
}
