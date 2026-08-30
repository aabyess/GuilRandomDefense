using UnityEngine;
using UnityEngine.UI;

// 메인 카메라의 현재 시야를 미니맵 위에 테두리로 그린다. 카메라가 기울어져 있어 지면에 비치는
// 영역이 사각형이 아니라 사다리꼴이라, Image를 여러 개 붙이는 대신 OnPopulateMesh로 직접 그린다.
public class MinimapViewportIndicator : MaskableGraphic
{
    [SerializeField] MinimapCamera minimapCamera;

    // Awake 시점에는 아직 부모가 없다 — new GameObject(...)는 SetParent보다 먼저 Awake를 돌린다.
    // 그래서 부모 탐색은 실제로 쓰는 시점으로 미룬다.
    MinimapCamera Minimap => minimapCamera != null
        ? minimapCamera
        : minimapCamera = GetComponentInParent<MinimapCamera>();
    [SerializeField] Camera mainCamera;
    [SerializeField] float lineThickness = 2f;
    [SerializeField] float groundHeight = 1f; // 섬 윗면 높이(y = 1 평면)와 교차시킨다.

    static readonly Vector2[] ViewportCorners =
    {
        new Vector2(0f, 0f),
        new Vector2(1f, 0f),
        new Vector2(1f, 1f),
        new Vector2(0f, 1f),
    };

    readonly Vector3[] groundCorners = new Vector3[4];
    readonly Vector2[] localCorners = new Vector2[4];

    Vector3 lastCameraPosition;
    Quaternion lastCameraRotation;
    bool hasDrawnOnce;
    bool loggedFailure;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // 미니맵 클릭 이동을 가리면 안 된다.
        color = new Color(1f, 1f, 1f, 0.8f);

        if (mainCamera == null) mainCamera = Camera.main;
    }

    public void SetMinimap(MinimapCamera camera)
    {
        minimapCamera = camera;
    }

    void Update()
    {
        if (mainCamera == null) return;

        // SetVerticesDirty()는 이 그래픽만이 아니라 소속 Canvas 전체를 다시 만들게 한다.
        // HUD가 같은 Canvas에 있으므로, 카메라가 실제로 움직였을 때만 갱신한다.
        Vector3 position = mainCamera.transform.position;
        Quaternion rotation = mainCamera.transform.rotation;

        if (hasDrawnOnce && position == lastCameraPosition && rotation == lastCameraRotation) return;

        lastCameraPosition = position;
        lastCameraRotation = rotation;
        hasDrawnOnce = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // 안 그려지는 이유가 여러 가지라, 첫 실패 한 번만 원인을 남긴다.
        MinimapCamera minimap = Minimap;
        if (minimap == null || mainCamera == null)
        {
            Warn($"참조 없음 — minimapCamera={(minimap != null)}, mainCamera={(mainCamera != null)}");
            return;
        }

        if (!TryGetGroundCorners())
        {
            Warn("카메라 시야가 지면 평면과 만나지 않습니다 (수평이거나 위를 봄).");
            return;
        }

        for (int i = 0; i < 4; i++)
            localCorners[i] = minimap.WorldToMinimapLocal(groundCorners[i]);

        // 실제 시야는 카메라가 기울어져 사다리꼴이지만, 원작 미니맵처럼 평면으로 읽히도록
        // 네 점을 감싸는 축 정렬 사각형으로 바꿔 그린다. 덮는 범위는 그대로다.
        MakeAxisAlignedBox(localCorners);

        if (!loggedFailure)
        {
            loggedFailure = true;
            Debug.Log($"[미니맵 시야] 지면 네 귀퉁이 {groundCorners[0]} / {groundCorners[2]}  →  " +
                      $"미니맵 좌표 {localCorners[0]} / {localCorners[2]}  (rect {((RectTransform)transform).rect})");
        }

        for (int i = 0; i < 4; i++)
            AddLineSegment(vh, localCorners[i], localCorners[(i + 1) % 4]);
    }

    static void MakeAxisAlignedBox(Vector2[] corners)
    {
        float xMin = corners[0].x, xMax = corners[0].x;
        float yMin = corners[0].y, yMax = corners[0].y;

        for (int i = 1; i < corners.Length; i++)
        {
            xMin = Mathf.Min(xMin, corners[i].x);
            xMax = Mathf.Max(xMax, corners[i].x);
            yMin = Mathf.Min(yMin, corners[i].y);
            yMax = Mathf.Max(yMax, corners[i].y);
        }

        corners[0] = new Vector2(xMin, yMin);
        corners[1] = new Vector2(xMax, yMin);
        corners[2] = new Vector2(xMax, yMax);
        corners[3] = new Vector2(xMin, yMax);
    }

    void Warn(string message)
    {
        if (loggedFailure) return;
        loggedFailure = true;
        Debug.LogWarning("[미니맵 시야] 그리지 못했습니다: " + message, this);
    }

    bool TryGetGroundCorners()
    {
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));

        for (int i = 0; i < 4; i++)
        {
            Ray ray = mainCamera.ViewportPointToRay(ViewportCorners[i]);

            if (!groundPlane.Raycast(ray, out float distance))
                return false;

            groundCorners[i] = ray.GetPoint(distance);
        }

        return true;
    }

    void AddLineSegment(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (lineThickness * 0.5f);

        int startIndex = vh.currentVertCount;

        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }
}
