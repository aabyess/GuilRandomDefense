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
    // 정면에서 오른쪽으로 25°, 위에서 10° — 워크3 초상화처럼 살짝 비스듬하게
    static readonly Vector3 ViewDirection = Quaternion.Euler(10f, 25f, 0f) * Vector3.forward;
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

    void Frame()
    {
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // 전신이 칸에 꽉 차게: 경계 구로 맞추면 대각선까지 넣느라 모델이 칸의 절반만 찼다(09-26 캡처).
        // 세로 반높이·가로 반폭(비스듬히 보니 x·z 중 큰 쪽) 중 큰 것을 시야각에 맞추고, 카메라 쪽으로 튀어나온 깊이만큼 더 물러난다.
        float halfHeight = bounds.extents.y;
        float halfWidth = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float half = Mathf.Max(halfHeight, halfWidth, 0.01f);
        float depth = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float distance = half / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f + depth;
        float radius = bounds.extents.magnitude;
        Transform cam = stageCamera.transform;
        cam.position = bounds.center + ViewDirection * distance;
        cam.LookAt(bounds.center);
        stageCamera.farClipPlane = distance + radius * 2f + 1f;
        stageCamera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
    }

    // 메인·미니맵 카메라가 무대(레이어 31)를 찍지 않게. 미니맵 카메라는 평소 꺼져 있어 allCameras에 안 잡혀서 전부 찾는다.
    static void StripLayerFromOtherCameras(Camera stage)
    {
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam != stage) cam.cullingMask &= ~(1 << Layer);
    }

    void OnDestroy()
    {
        if (texture != null) texture.Release();
        if (instance == this) instance = null;
    }
}
