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
    // 2026-09-25 사장님 「진행방향으로 보면서 걷게」 — 초당 이 각도(도)까지 돈다. 모서리에서 확 꺾이지 않게.
    // 모델의 앞(−Y로 만들어 ArtBinder가 +Z로 세운 것)이 transform.forward라서 이동 방향을 forward로 맞추면 된다.
    [SerializeField] private float turnDegreesPerSecond = 540f;

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

    // 이감 배수 하한. 0 이하(완전 정지)로는 떨어지지 않게 막는다 — 정지는
    // EnemyDummy.AddFreeze/RemoveFreeze의 몫이다. EnemyDummy.AddSlow도 이 값으로
    // 클램프한다 — 정의는 여기 한 곳뿐이다.
    public const float MinSlowMultiplier = 0.01f;

    /// <summary>이감 배수를 반영한다.</summary>
    public void SetSlowMultiplier(float multiplier)
    {
        slowMultiplier = Mathf.Clamp(multiplier, MinSlowMultiplier, 1f);
    }

    private void Start()
    {
        if (path == null || path.PointCount == 0) return;

        transform.position = path.GetPoint(0);
        currentIndex = path.PointCount > 1 ? 1 : 0;

        Vector3 first = path.GetPoint(currentIndex) - transform.position;
        first.y = 0f;
        if (first.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(first.normalized, Vector3.up);
    }

    private void Update()
    {
        if (path == null || path.PointCount == 0) return;

        Vector3 target = path.GetPoint(currentIndex);
        Vector3 before = transform.position;
        transform.position = Vector3.MoveTowards(before, target, moveSpeed * slowMultiplier * Time.deltaTime);

        // 진행 방향(수평)을 본다. 멈춰 있으면(빙결 등) 방향을 그대로 둔다.
        Vector3 step = transform.position - before;
        step.y = 0f;
        if (step.sqrMagnitude > 1e-8f)
        {
            Quaternion want = Quaternion.LookRotation(step.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnDegreesPerSecond * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, target) <= arrivalThreshold)
        {
            currentIndex = (currentIndex + 1) % path.PointCount;
        }
    }
}
