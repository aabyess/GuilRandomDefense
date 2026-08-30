using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 맵을 위에서 내려다보는 미니맵 전용 카메라. 이 컴포넌트가 붙은 RawImage와 같은 GameObject에서
// 스스로 캡처용 Camera를 만들어 RenderTexture에 그리고, RawImage 클릭을 받아 메인 카메라를 이동시킨다.
[RequireComponent(typeof(RawImage))]
public class MinimapCamera : MonoBehaviour, IPointerClickHandler
{
    // Map 오브젝트가 원점에 생성된다는 가정. 아니면 인스펙터에서 맞춰야 한다.
    [SerializeField] Vector3 mapCenter = Vector3.zero;
    [SerializeField] float mapExtent = 220f;
    [SerializeField] float cameraHeight = 300f;
    [SerializeField] int textureSize = 256;
    [SerializeField] float refreshesPerSecond = 12f;
    [SerializeField] RtsCameraController mainCameraController;

    Camera minimapCam;
    RenderTexture renderTexture;
    RawImage rawImage;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();

        renderTexture = new RenderTexture(textureSize, textureSize, 16) { name = "MinimapRenderTexture" };

        // UI 계층에 붙이면 Canvas의 스케일·위치 변화를 그대로 따라간다. 월드에 독립으로 둔다.
        GameObject camObj = new GameObject("MinimapCaptureCamera");
        camObj.transform.position = new Vector3(mapCenter.x, mapCenter.y + cameraHeight, mapCenter.z);
        camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        minimapCam = camObj.AddComponent<Camera>();
        minimapCam.orthographic = true;
        minimapCam.orthographicSize = mapExtent;
        minimapCam.targetTexture = renderTexture;
        // 켜 두면 매 프레임 맵 전체가 한 번 더 렌더링된다. 미니맵은 초당 몇 번이면 충분하다.
        minimapCam.enabled = false;
        minimapCam.clearFlags = CameraClearFlags.SolidColor;
        minimapCam.backgroundColor = Color.black;

        // UI를 다시 찍으면 미니맵 안에 HUD가 또 그려지므로 UI 레이어만 뺀다.
        int uiLayer = LayerMask.NameToLayer("UI");
        minimapCam.cullingMask = uiLayer >= 0 ? ~(1 << uiLayer) : ~0;

        rawImage.texture = renderTexture;

        if (mainCameraController == null)
            mainCameraController = FindFirstObjectByType<RtsCameraController>();
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
    // 이걸로만 변환하면 되고, 이 클래스 밖에서 mapCenter/mapExtent를 직접 계산하지 않는다.
    public Vector3 MinimapLocalToWorld(Vector2 localPoint)
    {
        Rect r = ((RectTransform)transform).rect;
        float normalizedX = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
        float normalizedZ = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);

        float worldX = mapCenter.x + (normalizedX - 0.5f) * 2f * mapExtent;
        float worldZ = mapCenter.z + (normalizedZ - 0.5f) * 2f * mapExtent;
        return new Vector3(worldX, mapCenter.y, worldZ);
    }

    public Vector2 WorldToMinimapLocal(Vector3 worldPosition)
    {
        Rect r = ((RectTransform)transform).rect;

        float normalizedX = 0.5f + (worldPosition.x - mapCenter.x) / (2f * mapExtent);
        float normalizedZ = 0.5f + (worldPosition.z - mapCenter.z) / (2f * mapExtent);

        float localX = Mathf.LerpUnclamped(r.xMin, r.xMax, normalizedX);
        float localY = Mathf.LerpUnclamped(r.yMin, r.yMax, normalizedZ);
        return new Vector2(localX, localY);
    }
}
