using UnityEngine;
using UnityEngine.UI;

// 메인 카메라의 현재 시야를 미니맵 위에 테두리로 그린다. 카메라가 기울어져 있어 지면에 비치는
// 영역이 사각형이 아니라 사다리꼴이라, Image를 여러 개 붙이는 대신 OnPopulateMesh로 직접 그린다.
public class MinimapViewportIndicator : MaskableGraphic
{
    [SerializeField] MinimapCamera minimapCamera;
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

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // 미니맵 클릭 이동을 가리면 안 된다.
        color = new Color(1f, 1f, 1f, 0.8f);

        if (minimapCamera == null) minimapCamera = GetComponentInParent<MinimapCamera>();
        if (mainCamera == null) mainCamera = Camera.main;
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

        if (minimapCamera == null || mainCamera == null) return;
        if (!TryGetGroundCorners()) return; // 지면과 안 만나면(카메라가 수평/위를 보면) 그리지 않는다.

        for (int i = 0; i < 4; i++)
            localCorners[i] = minimapCamera.WorldToMinimapLocal(groundCorners[i]);

        for (int i = 0; i < 4; i++)
            AddLineSegment(vh, localCorners[i], localCorners[(i + 1) % 4]);
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
