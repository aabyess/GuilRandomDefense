using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 선택 정보칸 초상화(사장님 09-26 「원랜디처럼 유닛 서 있는 거 — 모습이 보였으면」). 워크3 초상화처럼
/// **실제 모델이 서서 Idle로 움직이는 모습**을 RenderTexture로 찍는다.
///
///   무대: 맵에서 먼 곳(y −10000)에 레이어 31만 찍는 카메라 하나. 다른 카메라는 전부 31을 뺀다.
///   대상이 바뀌면 이전 복제를 지우고, 대상의 겉모습(Animator가 붙은 모델, 없으면 렌더러를 가진 루트)만 복제한다 —
///   비활성 부모 아래에서 게임 스크립트·콜라이더·NavMeshAgent·Rigidbody·LineRenderer를 떼어 낸 뒤 켠다
///   (Awake가 꺼진 컴포넌트에도 도는 걸 피하려고 — NetReplicaBuilder와 같은 방식). 컨트롤러 기본 상태가 Idle이라 숨 쉬듯 움직인다.
///   구도: 렌더러 경계로 전신이 칸에 들어오게, 약간 비스듬한 정면에서. 키가 달라도(0.4m 물범 ~ 4.5m 매머드) 칸에 꽉 찬다.
///   모델이 없는 자리표시(캡슐 등)면 false — 호출부가 지금의 글자 칸을 그대로 둔다.
/// </summary>
public class PortraitStage : MonoBehaviour
{
    public const int Layer = 31;
    const int TextureSize = 256;
    static readonly Vector3 StageOrigin = new Vector3(0f, -10000f, 0f);
    // 모델에서 카메라 쪽으로 가는 방향: 정면(+Z)에서 오른쪽으로 25°, **위로** 12° — 워크3 초상화처럼 살짝 위에서 비스듬히.
    // (처음엔 Euler(10, …)라 x가 양수 = 아래쪽이었다 — 카메라가 모델 밑에서 올려다봤다. 09-26 정정)
    static readonly Vector3 ViewDirection = Quaternion.Euler(-12f, 25f, 0f) * Vector3.forward;
    // 모델의 높이와 폭 중 큰 쪽이 칸의 이만큼을 채운다(PM 09-26: 약 90%).
    const float FillFraction = 0.9f;
    const float FieldOfView = 28f;

    static PortraitStage instance;

    Camera stageCamera;
    RenderTexture texture;
    Transform holder;         // 복제를 켜기 전에 잠깐 두는 비활성 부모
    GameObject clone;
    int currentTargetId;
    bool currentHasModel;

    public static RenderTexture Texture => Ensure().texture;

    static PortraitStage Ensure()
    {
        if (instance != null) return instance;
        instance = new GameObject("[초상화 무대]").AddComponent<PortraitStage>();
        instance.Build();
        return instance;
    }

    void Build()
    {
        transform.position = StageOrigin;

        texture = new RenderTexture(TextureSize, TextureSize, 16) { name = "Portrait", antiAliasing = 2 };

        GameObject camObj = new GameObject("PortraitCamera");
        camObj.transform.SetParent(transform, false);
        stageCamera = camObj.AddComponent<Camera>();
        stageCamera.cullingMask = 1 << Layer;
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(0.16f, 0.18f, 0.23f, 1f);   // GameHud SlotColor
        stageCamera.fieldOfView = FieldOfView;
        stageCamera.nearClipPlane = 0.05f;
        stageCamera.farClipPlane = 500f;
        stageCamera.targetTexture = texture;
        stageCamera.enabled = false;   // 대상이 있을 때만 찍는다

        // 무대 조명 — 씬의 방향광도 비추지만, 초상은 늘 밝게 보이게 앞에서 하나 더(레이어 31만).
        GameObject lightObj = new GameObject("PortraitLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.rotation = Quaternion.LookRotation(-ViewDirection + Vector3.down * 0.4f);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.9f;
        light.cullingMask = 1 << Layer;

        GameObject holderObj = new GameObject("Holder(inactive)");
        holderObj.transform.SetParent(transform, false);
        holderObj.SetActive(false);
        holder = holderObj.transform;
    }

    /// <summary>
    /// 대상을 초상화 무대에 세운다. 같은 대상이면 아무것도 안 한다(매 프레임 불려도 싸다).
    /// null이면 무대를 비운다. 돌려주는 값: 모델이 있어 초상을 찍는가(false면 글자 칸).
    /// </summary>
    public static bool Show(GameObject target)
    {
        PortraitStage stage = Ensure();
        int id = target != null ? target.GetInstanceID() : 0;
        if (id == stage.currentTargetId && (target != null || stage.clone == null)) return stage.currentHasModel;

        stage.currentTargetId = id;
        stage.Clear();
        stage.currentHasModel = target != null && stage.Stage(target);
        stage.stageCamera.enabled = stage.currentHasModel;
        StripLayerFromOtherCameras(stage.stageCamera);
        return stage.currentHasModel;
    }

    void Clear()
    {
        if (clone != null) Destroy(clone);
        clone = null;
    }

    bool Stage(GameObject target)
    {
        // 겉모습의 뿌리: Animator가 붙은 모델(유닛·적은 보통 자식). 없으면 대상 자체(조합표 인형 등).
        Animator animator = target.GetComponentInChildren<Animator>(true);
        GameObject source = animator != null ? animator.gameObject : target;

        if (!HasRealModel(source)) return false;

        clone = Instantiate(source, holder, false);
        clone.name = source.name + " (초상)";
        Strip(clone);
        SetLayerRecursively(clone, Layer);

        Transform t = clone.transform;
        t.SetParent(transform, false);   // 여기서 켜진다(Animator가 Idle부터)
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = source.transform.lossyScale;   // 실물과 같은 크기(ArtBinder가 맞춘 키) — 구도는 경계로 다시 맞춘다

        Frame();
        StopAllCoroutines();
        StartCoroutine(RefineByPixels(clone));
        return true;
    }

    // 자리표시(캡슐·큐브 기본 메시)만 있으면 초상이 무의미하다 — 글자 칸으로 둔다.
    static bool HasRealModel(GameObject source)
    {
        if (source.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) return true;
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            string name = mesh.name;
            if (name == "Capsule" || name == "Cube" || name == "Sphere" || name == "Cylinder" || name == "Quad" || name == "Plane") continue;
            return true;
        }
        return false;
    }

    static void Strip(GameObject root)
    {
        // 뒤에서부터 — RequireComponent로 딸린 것은 보통 요구한 쪽보다 앞에 있다.
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component c = components[i];
            if (c == null || c is Transform || c is Animator || c is Renderer || c is MeshFilter) continue;
            if (c is NavMeshAgent || c is NavMeshObstacle || c is Collider || c is Rigidbody || c is LineRenderer
                || c is MonoBehaviour || c is Light || c is AudioSource || c is ParticleSystem)
                DestroyImmediate(c);
        }
        // 선택 고리·사거리 원(Selectable이 만드는 자식, 이름 고정)은 끈다 — 모델 부품 이름과 헷갈리지 않게 정확한 이름만.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == "SelectionIndicator" || child.name == "AttackRangeIndicator")
                child.gameObject.SetActive(false);
    }

    static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // 구도 두 단계.
    //  1. Frame: 렌더러 경계 상자로 **넉넉하게**(모델이 절대 안 잘리게) 세운다. 스킨 메시 경계는 느슨해서(임포트 때 잡힌 범위)
    //     이것만으론 사람형 ~60%, 노가리는 점처럼 작다(09-26 캡처).
    //  2. RefineByPixels: 실제로 찍힌 칸을 읽어 모델 픽셀의 상자를 재고, 큰 쪽이 FillFraction이 되게 거리·중심을 고친다(두 번).
    //     정점·경계로 짐작하지 않고 **보이는 그대로** 맞춘다 — BakeMesh 정점은 부모 스케일이 빠져 너무 가까이 붙었다(09-26 시도).
    Vector3 aim;
    float distance;
    float clipRadius;
    Texture2D readback;

    void Frame()
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        clipRadius = bounds.extents.magnitude;
        float t = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * FillFraction;
        aim = bounds.center;
        distance = clipRadius / t + clipRadius;   // 구(球)로 감싸 어느 방향이든 칸 안
        ApplyCamera();
    }

    void ApplyCamera()
    {
        Transform cam = stageCamera.transform;
        cam.rotation = Quaternion.LookRotation(-ViewDirection, Vector3.up);
        cam.position = aim + ViewDirection * distance;
        stageCamera.nearClipPlane = Mathf.Max(0.01f, distance - clipRadius * 1.5f);
        stageCamera.farClipPlane = distance + clipRadius * 1.5f + 1f;
    }

    IEnumerator RefineByPixels(GameObject target)
    {
        var endOfFrame = new WaitForEndOfFrame();
        yield return null;                     // Idle 자세가 한 번 적용되게(바인드 자세면 팔 폭이 끼어든다)
        for (int pass = 0; pass < 2; pass++)
        {
            yield return endOfFrame;           // 무대 카메라가 이번 프레임을 찍은 뒤
            if (clone != target || clone == null) yield break;
            if (!MeasureModel(out float x0, out float y0, out float x1, out float y1)) yield break;

            // 픽셀 상자(0~1) → 지금 거리에서의 화면 크기. 큰 쪽이 FillFraction이 되게 거리를 비례로, 상자 가운데를 칸 가운데로.
            float fraction = Mathf.Max(x1 - x0, y1 - y0);
            float halfView = distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            Transform cam = stageCamera.transform;
            aim += cam.right * ((x0 + x1 - 1f) * halfView) + cam.up * ((y0 + y1 - 1f) * halfView);
            distance = Mathf.Max(0.05f, distance * fraction / FillFraction);
            ApplyCamera();
            if (Debug.isDebugBuild || Application.isEditor)
                Debug.Log($"[초상] {clone.name} {pass + 1}차: 채움 {fraction:P0} → 거리 {distance:0.00}");
        }
    }

    // 칸에서 배경색과 다른 픽셀의 상자(0~1, 아래 왼쪽 원점). 모델이 없거나 가장자리에 닿아 잘렸으면 false.
    bool MeasureModel(out float x0, out float y0, out float x1, out float y1)
    {
        x0 = y0 = x1 = y1 = 0f;
        int size = texture.width;
        if (readback == null) readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        readback.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
        RenderTexture.active = previous;

        Color32[] pixels = readback.GetPixels32();
        Color32 bg = pixels[0];                // 모서리 = 배경(1단계가 넉넉하니 모델이 모서리엔 없다)
        int minX = size, minY = size, maxX = -1, maxY = -1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color32 p = pixels[y * size + x];
                if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) <= 12) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        if (maxX < 0) return false;
        x0 = minX / (float)size; x1 = (maxX + 1) / (float)size;
        y0 = minY / (float)size; y1 = (maxY + 1) / (float)size;
        return true;
    }

    // 메인·미니맵 카메라가 무대(레이어 31)를 찍지 않게. 미니맵 카메라는 평소 꺼져 있어 allCameras에 안 잡혀서 전부 찾는다.
    static void StripLayerFromOtherCameras(Camera stage)
    {
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam != stage) cam.cullingMask &= ~(1 << Layer);
    }

    void OnDestroy()
    {
        if (readback != null) Destroy(readback);
        if (texture != null) texture.Release();
        if (instance == this) instance = null;
    }
}
