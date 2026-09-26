using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크래프트·스타·롤 방식의 전략 게임 카메라.
/// 화면 가장자리로 마우스를 밀거나 WASD/방향키로 이동하고, 휠로 확대·축소한다.
/// 스페이스(또는 Home)를 누르면 자기 레인 가운데로 돌아온다.
/// 카메라는 기울기를 유지한 채 수평으로만 움직인다(회전 없음).
/// </summary>
public class RtsCameraController : MonoBehaviour
{
    [Header("이동")]
    // 110 * 4.167 — maxHeight와 같은 이유로 생성 시점엔 MapGenerator.SetUpCamera가 덮어쓴다.
    [SerializeField] float moveSpeed = 458.4f;
    [SerializeField] float edgeThickness = 16f;      // 화면 가장자리에서 몇 픽셀 안쪽까지를 밀기 영역으로 볼지
    [SerializeField] bool edgeScrollEnabled = true;
    [SerializeField] float inputSmoothing = 12f;     // 클수록 즉각적. 0이면 감속 없음

    [Header("확대·축소")]
    [SerializeField] float zoomStep = 8f;            // 휠 한 칸당 높이 변화
    [SerializeField] float zoomSmoothing = 10f;
    [SerializeField] float minHeight = 12f;
    // 420 * 4.167(원작 비율 2단계 정본 배율, PM 지시 2026-09-23) — 맵 실제 생성 시점엔
    // MapGenerator.SetUpCamera가 이 값을 다시 덮어쓴다(MapLayout은 에디터 전용이라 여기서
    // 직접 참조할 수 없다). 이 기본값은 생성 전 인스펙터에 뜨는 값일 뿐이라 실제 동작에는
    // 영향이 없지만, 혼동을 막기 위해 같이 올려둔다.
    [SerializeField] float maxHeight = 1750.1f;

    [Header("이동 범위")]
    // 실제 생성 시점엔 MapGenerator.SetUpCamera가 MeasureIslands()로 섬 배치를 실측해
    // ±CameraMargin을 더한 값으로 덮어쓴다 — 여기 기본값은 그 결과를 그대로 옮겨 적은 것뿐이다
    // (레인이 좌우로 비대칭 확장돼 단순 배율(420 구간)로는 안 나온다).
    [SerializeField] Vector2 boundsMin = new Vector2(-2263.2f, -1095.9f);
    [SerializeField] Vector2 boundsMax = new Vector2(1391.8f, 1989.7f);

    // 이동 속도의 기준 높이. 이보다 높으면 빠르게, 낮으면 천천히 움직여
    // 화면에서 체감하는 이동량을 비슷하게 유지한다.
    const float SpeedReferenceHeight = 100f;

    // 한 프레임에 반영할 최대 시간(초). 30fps 한 프레임분.
    const float MaxFrameDelta = 1f / 30f;

    Vector2 smoothedInput;
    float targetHeight;

    // 방향키와 가장자리 밀기가 같은 속도인지 눈으로 확인하려고 둔 값 — 디버그 HUD(F1)가 읽는다.
    public Vector2 KeyboardAxis { get; private set; }
    public Vector2 EdgeAxis { get; private set; }
    /// <summary>지금 실제로 나가는 초당 이동 거리. 어느 입력이든 같은 값을 써야 정상이다.</summary>
    public float CurrentSpeed => smoothedInput.magnitude * moveSpeed * HeightScale();

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
        if (lane != null && FrameLaneAndPen(lane)) return;
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

    // ─────────────── 시작 구도: 레인 섬 + 우리를 HUD 사이 보이는 띠 안에 ───────────────
    //
    // 🔴 2026-09-23 사장님 「아직도 유닛 위에 이름이 안 보인다」. 예전엔 레인 섬 **중심을 화면 가운데**에 뒀는데,
    //    ① 뽑은 유닛은 섬 **밖 아래** 우리(LaneMarker.TakeSpawnPosition)에 생기고
    //    ② 기울어진 카메라는 가운데 아래쪽을 훨씬 짧게 보며(높이 425·50.4°에서 가운데→아래 끝 279, →위 끝 791)
    //    ③ 하단 HUD가 화면 아래 22%를 또 덮는다.
    //    그래서 새 유닛이 화면 아래 끝 너머에 생겼다(gameshot @pen 실측: 머리 screenPos y −187, HUD 윗선 238).
    //    이름표 코드는 멀쩡했다 — 시험을 경로 안쪽(보이는 자리)에서만 해서 「고쳤다」고 잘못 보고했다.
    // 그래서 **가운데 맞추기를 버리고** 광선으로 맞춘다: 우리 앞쪽 끝이 「하단 바 윗선 바로 위」에 오게 카메라를 밀고,
    // 섬 먼 끝과 좌우가 상단 바 아래·화면 안에 들 때까지 높이를 올린다. HUD 높이는 **실제 배치에서 읽는다**(숫자 안 박음).
    // 수렴 못 하면 경고를 남기고 그 자리에 둔다 — 조용히 이상한 높이로 끝내지 않는다.
    // 🔴 **계산이 맞는지 보기 전에, 그 값이 실제로 쓰이는지 본다.** MapGenerator.SetUpCamera가 씬에 적는 시작 카메라 값
    //    (09-23: 높이 216.7·z 1432.4)은 실행하면 이 함수가 **덮어쓴다**(실측 높이 425.5·z 1224.9). 그 편집 시점 값으로 계산해
    //    「높이 338.7로 올리자」까지 갔었는데, 넣었으면 화면이 한 픽셀도 안 바뀌었다. 시작 구도는 여기서만 정해진다.

    const int FrameIterations = 10;
    const float FrameMarginRatio = 0.03f;   // 보이는 띠 높이의 3%씩 위아래 여유
    const float FrameHeightStep = 1.15f;

    bool FrameLaneAndPen(LaneMarker lane)
    {
        Camera cam = GetComponent<Camera>();
        Vector3 planar = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        // 이 구도는 카메라가 z축을 따라 볼 때만 뜻이 있다(우리 맵은 +z를 본다). 아니면 예전 방식으로.
        if (cam == null || Mathf.Abs(planar.z) < Mathf.Abs(planar.x)) return false;

        Transform island = lane.transform;
        Bounds region = new Bounds(island.position, island.lossyScale);   // 섬은 스케일된 큐브다
        bool hasPen = TryGetPenBounds(lane, out Bounds pen);
        if (hasPen) region.Encapsulate(pen);
        float groundY = island.position.y + island.lossyScale.y * 0.5f;

        (float bottom, float top, string hudSource) = VisibleViewportBand();
        float margin = (top - bottom) * FrameMarginRatio;
        float nearViewportY = bottom + margin, farViewportY = top - margin;

        bool towardPlusZ = planar.z > 0f;
        float nearZ = towardPlusZ ? region.min.z : region.max.z;
        float farZ = towardPlusZ ? region.max.z : region.min.z;

        float height = Mathf.Clamp(HeightToCover(region.size.z), minHeight, maxHeight);
        bool converged = false;
        int iteration;
        for (iteration = 0; iteration < FrameIterations; iteration++)
        {
            // 높이를 정하고, 가로는 영역 가운데에 맞춘 뒤, 「보이는 띠 아래 끝」 광선이 우리 앞쪽 끝에 닿게 z를 민다.
            Vector3 position = transform.position;
            position.y = height;
            transform.position = position;
            position.x = region.center.x - FocusOffset().x;
            transform.position = position;

            if (!RayToGround(cam.ViewportPointToRay(new Vector3(0.5f, nearViewportY, 0f)), groundY, out Vector3 nearHit)) break;
            position.z += nearZ - nearHit.z;
            transform.position = position;

            // 먼 끝(좌우 두 모서리)과 앞쪽 좌우 모서리가 띠 안·화면 안인가.
            if (InBand(cam, new Vector3(region.min.x, groundY, farZ), nearViewportY, farViewportY) &&
                InBand(cam, new Vector3(region.max.x, groundY, farZ), nearViewportY, farViewportY) &&
                InBand(cam, new Vector3(region.min.x, groundY, nearZ), nearViewportY - margin, farViewportY) &&
                InBand(cam, new Vector3(region.max.x, groundY, nearZ), nearViewportY - margin, farViewportY))
            {
                converged = true;
                break;
            }
            if (height >= maxHeight) break;
            height = Mathf.Min(height * FrameHeightStep, maxHeight);
        }

        Vector3 unclamped = transform.position;
        Vector3 clamped = unclamped;
        clamped.x = Mathf.Clamp(clamped.x, boundsMin.x, boundsMax.x);
        clamped.z = Mathf.Clamp(clamped.z, boundsMin.y, boundsMax.y);
        transform.position = clamped;
        targetHeight = transform.position.y;

        string report = $"[카메라] 레인 {lane.LaneIndex} 시작 구도 — 섬{(hasPen ? "+우리" : "(우리 못 찾음)")} z {region.min.z:F0}~{region.max.z:F0} · " +
                        $"보이는 띠 뷰포트 {bottom:F2}~{top:F2}({hudSource}) · 높이 {transform.position.y:F0} · 위치 {transform.position} · 반복 {iteration + 1}";
        if (!converged || clamped != unclamped)
            Debug.LogWarning(report + (converged ? "" : $" — ⚠️ {FrameIterations}번 안에(또는 최대 높이 {maxHeight:F0}에서) 다 못 담았다") +
                             (clamped != unclamped ? $" — ⚠️ 이동 범위에 걸려 {unclamped} → {clamped}로 잘렸다" : ""), this);
        else
            Debug.Log(report, this);
        return true;
    }

    static bool InBand(Camera cam, Vector3 world, float minViewportY, float maxViewportY)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= minViewportY - 0.001f && v.y <= maxViewportY + 0.001f;
    }

    static bool RayToGround(Ray ray, float groundY, out Vector3 hit)
    {
        hit = Vector3.zero;
        if (ray.direction.y >= -0.0001f) return false;
        float t = (groundY - ray.origin.y) / ray.direction.y;
        if (t <= 0f) return false;
        hit = ray.origin + ray.direction * t;
        return true;
    }

    static bool TryGetPenBounds(LaneMarker lane, out Bounds bounds)
    {
        bounds = default;
        Transform pen = lane.UnitPen;
        if (pen == null) return false;
        bool any = false;
        foreach (Renderer r in pen.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        // 흔함 줄(첫 줄)만 담으면 그 뒤 칸 안 줄 — 흔함 아닌 유닛이 새로 나오거나 정렬(C)로 가는 자리 — 이 하단 바에 가려졌다
        // (2026-09-26 C 점검 사진: 이름표만 바 위로 보임). 두 줄 다 담는다.
        foreach (Vector3 slot in lane.FirstRowSlotPositions().Concat(lane.FreeRowSlotPositions()))
        {
            if (!any) { bounds = new Bounds(slot, Vector3.zero); any = true; }
            else bounds.Encapsulate(slot);
        }
        return any;
    }

    // 화면에서 3D가 실제로 보이는 세로 띠(뷰포트 0~1) — 하단 바 윗선 ~ 상단 바 아랫선. GameHud가 **실제로 배치한** 판을 읽는다
    // (상수를 옮겨 적으면 하단 바를 바꿀 때 또 어긋난다 — 2026-09-23 미니맵·팀 패널에서 같은 병). 못 찾으면 화면 전체.
    static (float bottom, float top, string source) VisibleViewportBand()
    {
        Canvas.ForceUpdateCanvases();
        float bottom = 0f, top = 1f;
        string source = "HUD 못 찾음 — 화면 전체";
        if (TryScreenYRange("BottomBar", out float _, out float bottomBarTop)) { bottom = bottomBarTop; source = "하단 바"; }
        if (TryScreenYRange("TopBar", out float topBarBottom, out float _)) { top = topBarBottom; source += "·상단 바"; }
        if (top - bottom < 0.2f) return (0f, 1f, $"HUD 띠가 비정상({bottom:F2}~{top:F2}) — 화면 전체");
        return (bottom, top, source);
    }

    static bool TryScreenYRange(string objectName, out float minViewportY, out float maxViewportY)
    {
        minViewportY = maxViewportY = 0f;
        GameObject found = GameObject.Find(objectName);
        if (found == null || !(found.transform is RectTransform rect) || Screen.height <= 0) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return false;   // 오버레이면 월드 모서리 = 화면 픽셀
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        minViewportY = Mathf.Min(corners[0].y, corners[2].y) / Screen.height;
        maxViewportY = Mathf.Max(corners[0].y, corners[2].y) / Screen.height;
        return true;
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
        // 채팅 입력 중엔 카메라가 안 움직여야 한다 — Input System은 텍스트 필드 포커스와
        // 무관하게 Keyboard.current를 그대로 읽어서, 안 막으면 채팅으로 "w"를 치는 순간
        // 카메라가 이동한다(ChatInputGate.cs 참고, 사장님 지시 2026-09-05).
        if (ChatInputGate.IsOpen) return;

        // 일시정지·배속과 무관하게 카메라는 움직여야 한다.
        // 다만 상한을 둔다: 플레이 진입·컴파일 직후 첫 프레임의 deltaTime은 초 단위로 튄다.
        // 그때 마우스가 화면 가장자리에 있으면 가장자리 밀기가 한 번에 수십 유닛을 이동시켜,
        // Start()에서 맞춰 놓은 시작 구도가 즉시 날아간다.
        float delta = Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta);

        // 내 레인으로 돌아오기. RTS의 '본진 보기'다.
        // 스페이스가 주 키이고, Home도 남겨둔다 — 에디터에서는 스페이스가 Game 뷰 최대화와
        // 겹쳐서 카메라만 확인하고 싶을 때 Home이 필요하다(빌드에서는 겹치지 않는다).
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.homeKey.wasPressedThisFrame))
            FocusOnLocalLane();

        // 두 입력은 같은 축 값으로 합쳐져 같은 moveSpeed·같은 감쇠를 지난다 —
        // 방향키가 느리게 느껴진다면 속도가 아니라 다른 데(에디터 포커스, 카메라 높이)에 원인이 있다.
        KeyboardAxis = KeyboardInput();
        EdgeAxis = EdgeInput();
        Vector2 rawInput = Vector2.ClampMagnitude(KeyboardAxis + EdgeAxis, 1f);
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

        // 2026-09-26 베타 피드백: WASD는 뺀다 — A(공격)·S(정지)·H(홀드) 같은 유닛 명령과 겹치고,
        // 방향키로 이미 움직인다. 원작(워크3)도 방향키·가장자리 밀기만 쓴다.
        Vector2 input = Vector2.zero;
        if (keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.upArrowKey.isPressed) input.y += 1f;
        return input;
    }

    Vector2 EdgeInput()
    {
        if (!edgeScrollEnabled || Mouse.current == null) return Vector2.zero;

        // 창이 포커스를 잃었으면 밀지 않는다(워크래프트와 같다). 멀티 두 창을 한 화면에 띄우면 포커스 없는 창의 커서 좌표가
        // 가장자리로 읽혀 그 창 카메라가 혼자 흘러갔다(09-26 구현담당2 캡처 — 호스트 창이 시작 직후 바다로).
        if (!Application.isFocused) return Vector2.zero;

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
