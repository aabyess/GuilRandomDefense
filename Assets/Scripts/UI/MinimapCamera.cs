using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 맵을 위에서 내려다보는 미니맵 전용 카메라. 이 컴포넌트가 붙은 RawImage와 같은 GameObject에서
// 스스로 캡처용 Camera를 만들어 RenderTexture에 그리고, RawImage 클릭을 받아 메인 카메라를 이동시킨다.
[RequireComponent(typeof(RawImage))]
public class MinimapCamera : MonoBehaviour, IPointerClickHandler
{
    // 맵을 못 잴 때만 쓰는 대비값. Map 오브젝트가 원점에 생성된다는 가정이다.
    [SerializeField] Vector3 mapCenter = Vector3.zero;
    [SerializeField] float mapExtent = 370f;
    [SerializeField] float cameraHeight = 500f;
    [SerializeField] int textureSize = 256;
    [SerializeField] float refreshesPerSecond = 12f;
    [SerializeField] RtsCameraController mainCameraController;

    Camera minimapCam;
    RenderTexture renderTexture;
    RawImage rawImage;

    // 땅이 차지하는 반폭(가로 x·세로 z) — FitToMap이 잰 값. 화면 칸 비율에 맞춰 늘린 값이 halfX/halfZ다.
    Vector2 groundHalf;
    float halfX, halfZ;
    float appliedAspect;

    // 맵이 커지거나 섬이 옮겨질 때마다 값을 손으로 맞추면 언젠가 어긋난다 —
    // 실제로 레인을 1.5배로 키웠을 때 미니맵 왼쪽 위가 잘렸다.
    // 씬에 놓인 맵을 직접 재서 맞춘다. 맵을 못 찾으면 인스펙터 값을 그대로 쓴다.
    //
    // 🔴 2026-09-23 섬이 미니맵 왼쪽 위 절반에만 몰리고 나머지는 바다였다(사장님 화면 1920×1080 실측).
    //    바다를 **이름 "Sea"로만** 빼고 있었는데, 09-13부터 보이는 수면은 「바다_물결」(SeaWaterBuilder)이 맡아
    //    바다 전체가 맵 경계에 들어갔다. 이름 목록은 또 어긋난다 — 그래서 바다를 **크기로** 뺀다: 맵 전체 경계의
    //    가로·세로 절반을 넘게 덮는 판은 수면(이름이 뭐든). 섬 하나가 맵의 절반을 덮는 일은 없다.
    //    ✗ NavMesh(걷는 땅)로 재는 안은 시험 후 버렸다(09-23 브리지 실측 DescribeMapFit): 걷는 땅은 z −476~1936뿐인데
    //      조합판처럼 걷지 않는 섬이 z −1773까지 있어 미니맵 아래쪽이 통째로 잘렸다.
    //
    // ⚠️ 범위는 이렇게 런타임에 스스로 맞춘다 — MapGenerator(편집 모드) 쪽에서 다시 맞추지
    // 말 것. 이 컴포넌트는 GameHud.BuildMinimap이 `new GameObject(...)`로 실행 중에만
    // 만든다(씬·프리팹 어디에도 없다), 그래서 편집 모드 생성기는 절대 이 인스턴스를 못 찾는다
    // — 예전엔 그 자리에 FitMinimapToIslands()라는 죽은 단계가 있었다(2026-09-05 삭제,
    // PM 배선 감사).
    void FitToMap()
    {
        groundHalf = new Vector2(mapExtent, mapExtent);

        Bounds? ground = MeasureMapRenderers();
        if (ground == null) return;

        Bounds bounds = ground.Value;
        // 여백은 맵 크기에 비례시킨다 — 맵이 4배로 커진 뒤 고정 30은 가장자리 섬을 틀에 딱 붙였다.
        float margin = Mathf.Max(MinMapMargin, Mathf.Max(bounds.extents.x, bounds.extents.z) * MapMarginRatio);
        mapCenter = new Vector3(bounds.center.x, 0f, bounds.center.z);
        groundHalf = new Vector2(bounds.extents.x + margin, bounds.extents.z + margin);
    }

    const float MinMapMargin = 30f;
    const float MapMarginRatio = 0.03f;

    // 진단 비교용(DescribeMapFit) — NavMesh에서 Sea 영역이 아닌 삼각형의 경계. 걷지 않는 섬이 빠져 범위로는 못 쓴다(위 FitToMap 주석).
    static Bounds? MeasureWalkableGround()
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        if (mesh.vertices == null || mesh.vertices.Length == 0 || mesh.indices == null) return null;

        int seaArea = NavMesh.GetAreaFromName("Sea");
        Bounds? acc = null;
        for (int triangle = 0; triangle < mesh.areas.Length; triangle++)
        {
            if (mesh.areas[triangle] == seaArea) continue;
            for (int corner = 0; corner < 3; corner++)
            {
                Vector3 v = mesh.vertices[mesh.indices[triangle * 3 + corner]];
                if (acc == null) acc = new Bounds(v, Vector3.zero);
                else { Bounds b = acc.Value; b.Encapsulate(v); acc = b; }
            }
        }
        return acc;
    }

    // 브리지 진단용(call MinimapCamera.DescribeMapFit) — 두 측정값과 NavMesh 영역별 삼각형 수를 적는다.
    static string DescribeMapFit()
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        var counts = new System.Collections.Generic.SortedDictionary<int, int>();
        foreach (int area in mesh.areas) counts[area] = counts.TryGetValue(area, out int c) ? c + 1 : 1;
        string areas = string.Join(", ", System.Linq.Enumerable.Select(counts, kv => $"{kv.Key}({NavMeshAreaName(kv.Key)}):{kv.Value}"));
        return $"Sea 영역 번호 {NavMesh.GetAreaFromName("Sea")} · 삼각형 영역별 [{areas}]\n" +
               $"   걷는 땅 경계 {Describe(MeasureWalkableGround())}\n" +
               $"   렌더러 경계(미니맵이 쓰는 값) {Describe(MeasureMapRenderers())}\n" +
               $"   오른쪽 끝(x 큰 순) {Extremes(r => r.bounds.max.x)}\n" +
               $"   왼쪽 끝(x 작은 순) {Extremes(r => -r.bounds.min.x)}";
    }

    // 경계를 붙잡는 렌더러 이름 — 「왜 여기까지 잡혔나」를 가린다. 바다로 빠지는 판은 제외.
    static string Extremes(System.Func<Renderer, float> key)
    {
        GameObject map = GameObject.Find("Map");
        if (map == null) return "Map 없음";
        Bounds? ground = MeasureMapRenderers();
        return string.Join(" · ", System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(
            System.Linq.Enumerable.Where(map.GetComponentsInChildren<Renderer>(), r => !r.name.StartsWith("Sea") && !r.name.StartsWith("바다")
                && (ground == null || r.bounds.size.x <= ground.Value.size.x * 0.9f)), key), 6)
            .Select(r => $"{r.transform.parent?.name}/{r.name} x {r.bounds.min.x:F0}~{r.bounds.max.x:F0}"));
    }

    static string NavMeshAreaName(int area)
    {
        foreach (string name in new[] { "Walkable", "Not Walkable", "Jump", "Sea" })
            if (NavMesh.GetAreaFromName(name) == area) return name;
        return "?";
    }

    static string Describe(Bounds? b) => b == null ? "없음" : $"중심 {b.Value.center} · 크기 {b.Value.size} · x {b.Value.min.x:F0}~{b.Value.max.x:F0} · z {b.Value.min.z:F0}~{b.Value.max.z:F0}";

    // "Map" 아래 렌더러의 경계. 바다 판은 크기(맵 전체 가로·세로의 절반 넘게 덮는 판)로 빼고, 이름("Sea"·"바다"로 시작)은 덤으로 뺀다.
    static Bounds? MeasureMapRenderers()
    {
        GameObject map = GameObject.Find("Map");
        if (map == null) return null;

        Renderer[] renderers = map.GetComponentsInChildren<Renderer>();
        Bounds? all = null;
        foreach (Renderer renderer in renderers)
        {
            if (all == null) all = renderer.bounds;
            else { Bounds b = all.Value; b.Encapsulate(renderer.bounds); all = b; }
        }
        if (all == null) return null;

        Bounds? acc = null;
        foreach (Renderer renderer in renderers)
        {
            string name = renderer.name;
            if (name.StartsWith("Sea") || name.StartsWith("바다")) continue;
            Bounds r = renderer.bounds;
            if (r.size.x > all.Value.size.x * 0.5f && r.size.z > all.Value.size.z * 0.5f) continue;

            if (acc == null) acc = r;
            else { Bounds b = acc.Value; b.Encapsulate(r); acc = b; }
        }
        return acc;
    }

    void Awake()
    {
        FitToMap();

        rawImage = GetComponent<RawImage>();

        // UI 계층에 붙이면 Canvas의 스케일·위치 변화를 그대로 따라간다. 월드에 독립으로 둔다.
        GameObject camObj = new GameObject("MinimapCaptureCamera");
        camObj.transform.position = new Vector3(mapCenter.x, mapCenter.y + cameraHeight, mapCenter.z);
        camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        minimapCam = camObj.AddComponent<Camera>();
        minimapCam.orthographic = true;
        // 켜 두면 매 프레임 맵 전체가 한 번 더 렌더링된다. 미니맵은 초당 몇 번이면 충분하다.
        minimapCam.enabled = false;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        // 칸이 가로로 길고 맵은 세로로 길어서(09-23: 3175×3715) 좌우에 여백이 생기고, 그 여백이 바다 판 밖까지 나가면
        // 배경이 그대로 보인다. 검정이면 「잘린 검은 띠」로 읽혀서 수면과 비슷한 짙은 파랑으로 둔다.
        minimapCam.backgroundColor = new Color(0.06f, 0.16f, 0.30f);

        // UI를 다시 찍으면 미니맵 안에 HUD가 또 그려지므로 UI 레이어만 뺀다.
        int uiLayer = LayerMask.NameToLayer("UI");
        minimapCam.cullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;

        // 칸 크기는 아직 모른다 — GameHud가 `new GameObject(..., typeof(MinimapCamera))`로 만들어 Awake가
        // 부모·앵커를 정하기 **전에** 불린다. 일단 정사각으로 두고 Start·크기 변경 때 칸 비율로 다시 맞춘다.
        ApplyAspect(1f);

        if (mainCameraController == null)
            mainCameraController = FindFirstObjectByType<RtsCameraController>();
    }

    // 경계를 Start에서 다시 잰다 — 실행 중에 생기는 섬 장식(OnEnable·Awake에서 붙는 것)까지 담기게, 씬의 Awake·OnEnable이
    // 전부 끝난 뒤에 한 번 더.
    void Start()
    {
        FitToMap();
        minimapCam.transform.position = new Vector3(mapCenter.x, mapCenter.y + cameraHeight, mapCenter.z);
        appliedAspect = 0f;   // 같은 비율이어도 새 경계로 다시 계산하게
        ApplyAspect(CurrentAspect());
    }

    void OnRectTransformDimensionsChange()
    {
        if (minimapCam != null) ApplyAspect(CurrentAspect());
    }

    float CurrentAspect()
    {
        Rect r = ((RectTransform)transform).rect;
        return r.width > 1f && r.height > 1f ? r.width / r.height : 1f;
    }

    // 🔴 칸이 가로로 길다(1920×1080에서 약 2:1). 예전엔 정사각 텍스처(256×256)·정사각 범위를 그 칸에 늘려 붙여
    //    섬이 가로로 두 배 늘어 보였다. 텍스처·카메라를 칸 비율로 맞추고, 땅 범위를 **줄이지 않고** 모자란 축만 넓힌다.
    void ApplyAspect(float aspect)
    {
        if (Mathf.Abs(aspect - appliedAspect) < 0.01f && renderTexture != null) return;
        appliedAspect = aspect;

        halfX = groundHalf.x;
        halfZ = groundHalf.y;
        if (halfX / halfZ < aspect) halfX = halfZ * aspect;
        else halfZ = halfX / aspect;

        int height = textureSize;
        int width = Mathf.Clamp(Mathf.RoundToInt(textureSize * aspect), 16, textureSize * 4);
        if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
        {
            if (renderTexture != null)
            {
                minimapCam.targetTexture = null;
                renderTexture.Release();
                Destroy(renderTexture);
            }
            renderTexture = new RenderTexture(width, height, 16) { name = "MinimapRenderTexture" };
            rawImage.texture = renderTexture;
        }

        minimapCam.targetTexture = renderTexture;
        minimapCam.aspect = aspect;
        minimapCam.orthographicSize = halfZ;
        nextRefreshTime = 0f;   // 다음 Update에서 바로 다시 그린다
    }

    float nextRefreshTime;

    void Update()
    {
        if (minimapCam == null || Time.unscaledTime < nextRefreshTime) return;

        nextRefreshTime = Time.unscaledTime + 1f / Mathf.Max(1f, refreshesPerSecond);
        minimapCam.Render();
    }

    void OnDestroy()
    {
        if (minimapCam != null)
        {
            minimapCam.targetTexture = null;
            Destroy(minimapCam.gameObject);   // 더 이상 부모를 따라 사라지지 않으므로 직접 정리한다
        }

        if (renderTexture != null) renderTexture.Release();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (mainCameraController == null) return;

        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        mainCameraController.MoveTo(MinimapLocalToWorld(localPoint));
    }

    // 좌표 변환 규칙이 이 두 메서드에만 있도록 정리했다 — 미니맵을 쓰는 다른 코드(시야 표시 등)는
    // 이걸로만 변환하면 되고, 이 클래스 밖에서 범위를 직접 계산하지 않는다.
    // 가로(x)·세로(z) 반폭이 다르다 — 칸 비율에 맞춰 카메라가 찍는 범위 그대로다(ApplyAspect).
    public Vector3 MinimapLocalToWorld(Vector2 localPoint)
    {
        Rect r = ((RectTransform)transform).rect;
        float normalizedX = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
        float normalizedZ = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);

        float worldX = mapCenter.x + (normalizedX - 0.5f) * 2f * halfX;
        float worldZ = mapCenter.z + (normalizedZ - 0.5f) * 2f * halfZ;
        return new Vector3(worldX, mapCenter.y, worldZ);
    }

    public Vector2 WorldToMinimapLocal(Vector3 worldPosition)
    {
        Rect r = ((RectTransform)transform).rect;

        float normalizedX = 0.5f + (worldPosition.x - mapCenter.x) / (2f * halfX);
        float normalizedZ = 0.5f + (worldPosition.z - mapCenter.z) / (2f * halfZ);

        float localX = Mathf.LerpUnclamped(r.xMin, r.xMax, normalizedX);
        float localY = Mathf.LerpUnclamped(r.yMin, r.yMax, normalizedZ);
        return new Vector2(localX, localY);
    }
}
