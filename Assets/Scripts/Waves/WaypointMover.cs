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

    // 이감(EnemyDummy.AddSlow) 배수. 1이면 원속도, 낮을수록 느려진다. moveSpeed 자체는
    // 건드리지 않는다 — 배수를 걷어내(1로 되돌리)면 대입 없이도 원속도로 자동 복귀한다.
    private float slowMultiplier = 1f;

    public void SetPath(WaypointPath newPath)
    {
        path = newPath;
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    /// <summary>이감 배수를 반영한다. EnemyDummy.MinSlowMultiplier(0.01)와 같은 하한으로
    /// 0 이하(완전 정지)로는 떨어지지 않게 막는다 — 정지는 AddFreeze/RemoveFreeze의 몫이다.</summary>
    public void SetSlowMultiplier(float multiplier)
    {
        slowMultiplier = Mathf.Clamp(multiplier, 0.01f, 1f);
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
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * slowMultiplier * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= arrivalThreshold)
        {
            currentIndex = (currentIndex + 1) % path.PointCount;
        }
    }
}
