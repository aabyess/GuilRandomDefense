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

    // 왕복 모드(광폭화 유닛, 2026-10-07) — 경로의 한 변(점 shuttleLo ↔ shuttleHi)만 오간다. 끝까지 안 간다.
    private int shuttleLo = -1, shuttleHi = -1;
    private Vector3 shuttleOffset;
    Vector3 Pt(int i) => path.GetPoint(i) + (shuttleLo >= 0 && (i == shuttleLo || i == shuttleHi) ? shuttleOffset : Vector3.zero);
    public bool Shuttling => shuttleLo >= 0;

    /// <summary>경로에서 가장 왼쪽(x 최소) 세로 변 — 이웃한 두 점의 x가 가장 작고 서로 같은 쪽 — 만 오가게 한다. 경로가 한 변뿐이면 무시.</summary>
    public bool SetShuttleLeftEdge(float outwardOffset = 0f)
    {
        if (path == null || path.PointCount < 2) return false;
        int n = path.PointCount, best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 a = path.GetPoint(i), b = path.GetPoint(j);
            if (Mathf.Abs(a.x - b.x) > Mathf.Abs(a.z - b.z)) continue;   // 세로(z 방향) 변만
            float score = (a.x + b.x) * 0.5f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        if (best < 0) return false;
        shuttleLo = best; shuttleHi = (best + 1) % n;
        shuttleOffset = new Vector3(-outwardOffset, 0f, 0f);   // 왼쪽 변 바깥(−x)으로 비켜 일반 적 줄과 겹치지 않게
        if (currentIndex != shuttleHi && currentIndex != shuttleLo) currentIndex = shuttleLo;   // 아직 변 위가 아니면 변의 시작점으로
        return true;
    }
    public Vector3 ShuttleA => path != null && shuttleLo >= 0 ? Pt(shuttleLo) : Vector3.zero;
    public Vector3 ShuttleB => path != null && shuttleHi >= 0 ? Pt(shuttleHi) : Vector3.zero;

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
    // 이속을 **올리는** 배수의 상한(2026-09-30) — 원작 손해 오라(야마토 A12P: 적 이속 +15%)를 담으려고 1 초과를 받는다.
    public const float MaxSpeedMultiplier = 2f;

    /// <summary>이감 배수를 반영한다.</summary>
    public void SetSlowMultiplier(float multiplier)
    {
        slowMultiplier = Mathf.Clamp(multiplier, MinSlowMultiplier, MaxSpeedMultiplier);
    }

    /// <summary>넉백(초월 김건 「힘의작용과반작용」) — 경로를 따라 distance(세계 거리)만큼 뒤로 되돌린다. 시작점 아래로는 안 내려간다.</summary>
    public void PushBack(float distance)
    {
        if (path == null || path.PointCount == 0 || distance <= 0f) return;
        Vector3 pos = transform.position;
        while (distance > 0f)
        {
            int prev = currentIndex - 1;
            if (prev < 0) break;
            Vector3 p = path.GetPoint(prev);
            float d = Vector3.Distance(pos, p);
            if (d > distance) { pos = Vector3.MoveTowards(pos, p, distance); distance = 0f; break; }
            pos = p;
            distance -= d;
            currentIndex = prev;
            if (currentIndex == 0) { currentIndex = path.PointCount > 1 ? 1 : 0; break; }
        }
        transform.position = pos;
    }

    private void Start()
    {
        if (path == null || path.PointCount == 0) return;

        transform.position = Pt(0);
        currentIndex = path.PointCount > 1 ? 1 : 0;

        Vector3 first = Pt(currentIndex) - transform.position;
        first.y = 0f;
        if (first.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(first.normalized, Vector3.up);
    }

    private void Update()
    {
        if (path == null || path.PointCount == 0) return;

        Vector3 target = Pt(currentIndex);
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
            if (shuttleLo >= 0 && (currentIndex == shuttleLo || currentIndex == shuttleHi))
                currentIndex = currentIndex == shuttleHi ? shuttleLo : shuttleHi;   // 변의 한쪽 끝에 닿으면 반대 끝으로
            else
                currentIndex = (currentIndex + 1) % path.PointCount;
        }
    }
}
