using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크래프트·스타·롤 방식의 전략 게임 카메라.
/// 화면 가장자리로 마우스를 밀거나 WASD/방향키로 이동하고, 휠로 확대·축소한다.
/// 카메라는 기울기를 유지한 채 수평으로만 움직인다(회전 없음).
/// </summary>
public class RtsCameraController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 70f;
    [SerializeField] float edgeThickness = 16f;      // 화면 가장자리에서 몇 픽셀 안쪽까지를 밀기 영역으로 볼지
    [SerializeField] bool edgeScrollEnabled = true;
    [SerializeField] float inputSmoothing = 12f;     // 클수록 즉각적. 0이면 감속 없음

    [Header("확대·축소")]
    [SerializeField] float zoomStep = 8f;            // 휠 한 칸당 높이 변화
    [SerializeField] float zoomSmoothing = 10f;
    [SerializeField] float minHeight = 12f;
    [SerializeField] float maxHeight = 220f;

    [Header("이동 범위")]
    [SerializeField] Vector2 boundsMin = new Vector2(-220f, -220f);
    [SerializeField] Vector2 boundsMax = new Vector2(220f, 220f);

    // 이동 속도의 기준 높이. 이보다 높으면 빠르게, 낮으면 천천히 움직여
    // 화면에서 체감하는 이동량을 비슷하게 유지한다.
    const float SpeedReferenceHeight = 60f;

    Vector2 smoothedInput;
    float targetHeight;

    void Start()
    {
        targetHeight = transform.position.y;
        FocusOnLocalLane();
    }

    /// <summary>내 레인이 화면 중앙에 오도록 맞춘다. 레인 표식이 없으면 씬에 놓인 위치를 그대로 쓴다.</summary>
    public void FocusOnLocalLane()
    {
        int laneIndex = LocalPlayer.LocalPlayerId;

        Vector3 center;
        float extent;
        string source;

        LaneMarker lane = LaneMarker.Get(laneIndex);
        if (lane != null)
        {
            center = lane.transform.position;
            // 섬은 스케일된 큐브라 lossyScale이 곧 크기다.
            extent = Mathf.Max(lane.transform.lossyScale.x, lane.transform.lossyScale.z);
            source = "LaneMarker";
        }
        else if (TryGetLaneBoundsFromPath(laneIndex, out center, out extent))
        {
            source = "순찰 경로";
        }
        else
        {
            Debug.LogWarning($"[카메라] {laneIndex}번 레인을 찾지 못해 시작 위치를 그대로 둡니다.", this);
            return;
        }

        // 레인이 화면에 차도록 높이를 맞춘다. 기울기와 시야각에서 지면 세로 커버리지를 역산한다.
        targetHeight = Mathf.Clamp(HeightToCover(extent * 1.4f), minHeight, maxHeight);
        Vector3 position = transform.position;
        position.y = targetHeight;
        transform.position = position;

        MoveTo(center);

        Debug.Log($"[카메라] {laneIndex}번 레인({source}) 중심 {center}, 크기 {extent:F0} " +
                  $"→ 높이 {targetHeight:F0}, 카메라 위치 {transform.position}, 회전 {transform.eulerAngles}");
    }

    /// <summary>지면에서 세로로 span만큼 담기려면 필요한 카메라 높이.</summary>
    float HeightToCover(float span)
    {
        Camera cam = GetComponent<Camera>();
        float halfFov = (cam != null ? cam.fieldOfView : 60f) * 0.5f;
        float pitch = transform.eulerAngles.x;

        float near = Mathf.Tan((pitch + halfFov) * Mathf.Deg2Rad);
        float far = Mathf.Tan((pitch - halfFov) * Mathf.Deg2Rad);

        // 시야 위쪽이 지평선을 넘어가면(각도 0 이하) 커버리지가 무한이 된다 — 그땐 높이를 못 구한다.
        if (far <= 0.01f || near <= 0.01f) return transform.position.y;

        float coveragePerUnitHeight = 1f / far - 1f / near;
        return coveragePerUnitHeight > 0.01f ? span / coveragePerUnitHeight : transform.position.y;
    }

    static bool TryGetLaneBoundsFromPath(int laneIndex, out Vector3 center, out float extent)
    {
        center = Vector3.zero;
        extent = 0f;

        string expected = $"Lane{laneIndex + 1}_Path";
        foreach (WaypointPath path in FindObjectsByType<WaypointPath>(FindObjectsSortMode.None))
        {
            if (path.name != expected || path.PointCount == 0) continue;

            Vector3 min = path.GetPoint(0);
            Vector3 max = min;
            for (int i = 1; i < path.PointCount; i++)
            {
                min = Vector3.Min(min, path.GetPoint(i));
                max = Vector3.Max(max, path.GetPoint(i));
            }

            center = (min + max) * 0.5f;
            extent = Mathf.Max(max.x - min.x, max.z - min.z);
            return true;
        }

        return false;
    }

    // 미니맵 클릭 등 외부에서 카메라를 특정 지점으로 즉시 옮길 때 쓴다. 높이(줌)는 유지한다.
    /// <summary>지정한 지면 좌표가 화면 중앙에 오도록 옮긴다(미니맵 클릭 등).</summary>
    public void MoveTo(Vector3 groundPosition)
    {
        // 카메라는 기울어져 있어서 자기 위치보다 앞쪽 지면을 본다.
        // 클릭한 지점에 카메라를 갖다 놓으면 실제로는 그 너머를 보게 되므로 기울기만큼 물러선다.
        Vector3 offset = FocusOffset();

        Vector3 position = transform.position;
        position.x = Mathf.Clamp(groundPosition.x - offset.x, boundsMin.x, boundsMax.x);
        position.z = Mathf.Clamp(groundPosition.z - offset.z, boundsMin.y, boundsMax.y);
        transform.position = position;
    }

    /// <summary>현재 높이·기울기에서 카메라 위치와 화면 중앙 지면 사이의 수평 거리.</summary>
    Vector3 FocusOffset()
    {
        Vector3 forward = transform.forward;
        if (forward.y >= -0.01f) return Vector3.zero;   // 수평이거나 위를 보면 지면 교차점이 없다

        float distance = transform.position.y / -forward.y;
        Vector3 offset = forward * distance;
        offset.y = 0f;
        return offset;
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;   // 일시정지·배속과 무관하게 카메라는 움직여야 한다

        Vector2 rawInput = Vector2.ClampMagnitude(KeyboardInput() + EdgeInput(), 1f);
        smoothedInput = inputSmoothing > 0f
            ? Vector2.Lerp(smoothedInput, rawInput, 1f - Mathf.Exp(-inputSmoothing * delta))
            : rawInput;

        targetHeight = Mathf.Clamp(targetHeight - ZoomInput() * zoomStep, minHeight, maxHeight);

        Vector3 position = transform.position;
        position += PlanarDirection(smoothedInput) * (moveSpeed * HeightScale() * delta);
        position.y = Mathf.Lerp(position.y, targetHeight, 1f - Mathf.Exp(-zoomSmoothing * delta));

        position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
        position.z = Mathf.Clamp(position.z, boundsMin.y, boundsMax.y);
        transform.position = position;
    }

    float HeightScale() => Mathf.Clamp(transform.position.y / SpeedReferenceHeight, 0.35f, 3f);

    // 카메라가 기울어져 있어도 이동은 수평면 기준이라, forward의 y 성분을 버린 방향을 쓴다.
    Vector3 PlanarDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return right * input.x + forward * input.y;
    }

    static Vector2 KeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        return input;
    }

    Vector2 EdgeInput()
    {
        if (!edgeScrollEnabled || Mouse.current == null) return Vector2.zero;

        Vector2 position = Mouse.current.position.ReadValue();

        // 커서가 창 밖으로 나가면 좌표가 화면 범위를 벗어난다. 그대로 두면 다른 창을 보는 동안
        // 카메라가 계속 흘러가므로, 범위 밖이면 밀기를 멈춘다.
        if (position.x < 0f || position.y < 0f || position.x > Screen.width || position.y > Screen.height)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (position.x <= edgeThickness) input.x -= 1f;
        if (position.x >= Screen.width - edgeThickness) input.x += 1f;
        if (position.y <= edgeThickness) input.y -= 1f;
        if (position.y >= Screen.height - edgeThickness) input.y += 1f;
        return input;
    }

    // 휠 한 칸의 크기가 플랫폼·장치마다 달라서 방향만 취한다.
    static float ZoomInput()
    {
        if (Mouse.current == null) return 0f;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return 0f;
        return Mathf.Sign(scroll);
    }
}
