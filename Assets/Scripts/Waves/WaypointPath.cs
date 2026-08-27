using UnityEngine;

/// <summary>
/// 씬에 배치된 빈 오브젝트에 붙여, 자식 또는 참조된 Transform들을 순서대로 잇는 경로를 정의한다.
/// WaypointMover가 이 경로를 따라 이동한다.
/// </summary>
public class WaypointPath : MonoBehaviour
{
    [SerializeField] private Transform[] points;

    public int PointCount => points != null ? points.Length : 0;

    public Vector3 GetPoint(int index)
    {
        if (points == null || index < 0 || index >= points.Length || points[index] == null)
        {
            Debug.LogWarning($"WaypointPath: 인덱스 {index}에 유효한 포인트가 없습니다.", this);
            return transform.position;
        }

        return points[index].position;
    }

    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;

            Transform nextTransform = points[(i + 1) % points.Length];
            if (nextTransform == null) continue;

            Gizmos.DrawSphere(points[i].position, 0.2f);
            Gizmos.DrawLine(points[i].position, nextTransform.position);
        }
    }
}
