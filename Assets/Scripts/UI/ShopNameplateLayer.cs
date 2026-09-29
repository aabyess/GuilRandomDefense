using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// 레인 상점 건물 머리 위 이름표(2026-09-29 사장님 요청 「무슨 상점인지 모르겠다」).
// UnitNameplateLayer와 같은 자기완결형 Screen Space Overlay 캔버스다. 씬에는 없다 — 판이 열리면 스스로 선다
// (UnitAcquireNotice와 같은 설치 방식). 모든 플레이어 레인의 건물에 다 붙인다(내 것만이 아니다).
//
// 대상은 이름이 아니라 **ILaneShop 컴포넌트**로 찾는다 — 맵 생성기가 건물을 다시 세워도 따라온다.
// 표시 이름만 오브젝트 이름(「Lane1_유닛강화소」에서 「Lane1_」를 뗀 것)으로 아래 표에서 찾는다.
public class ShopNameplateLayer : MonoBehaviour
{
    // 오브젝트 이름(레인 번호를 뗀 것) → 사람이 읽는 이름. 표에 없으면 뗀 이름을 그대로 쓴다.
    // 긴 이름은 두 줄 — 건물 간격(기본 줌 1920폭에서 약 130px)보다 한 줄 이름이 넓어 이웃과 겹쳤다(09-29 실측).
    static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "도박소", "도박소" },
        { "도움소", "도움소" },
        { "유닛강화소", "유닛\n강화소" },
        { "다른세계강화소", "다른세계\n강화소" },
        { "영원함강화소", "영원함\n강화소" },
        { "공격타입강화소", "공격타입\n강화소" },
        { "해적단상점", "해적단\n상점" },
        { "항해일지", "항해일지" },
    };

    static readonly Regex LanePrefix = new Regex(@"^Lane\d+_");

    /// <summary>건물 오브젝트 이름(「Lane1_도박소」) → 한 줄 표시 이름(「도박소」). 하단 정보칸(GameHud)이 같은 표를 쓴다(09-29 PM).</summary>
    public static string DisplayNameOf(string objectName)
    {
        string key = LanePrefix.Replace(objectName ?? "", "");
        return (DisplayNames.TryGetValue(key, out string display) ? display : key).Replace("\n", "");
    }

    // 유닛 이름표(-95)보다 아래 — 상점은 레인 뒤편에 서 있어 유닛 이름표와 겹치면 유닛 쪽이 위여야 한다.
    // GameHud(0)보다 아래라 하단 바·팀 패널 뒤로 숨는다.
    const int SortingOrder = -97;
    const float FontSize = 22f;              // 1920×1080 기준(캔버스 스케일러가 창 크기에 맞춘다 — GameHud와 같은 설정)
    const float HeadMargin = 6f;             // 지붕 위 여유(세계 단위)
    static readonly Color TextColor = new Color(1f, 0.93f, 0.62f);   // 옅은 금색 — 등급 색(유닛 이름표)과 섞이지 않게

    class Plate
    {
        public Transform building;
        public string caption;
        public Vector3 roof;                 // 지붕 한가운데 위. 건물은 안 움직인다 — 한 번만 잰다
        public RectTransform root;
        public TMP_Text text;
    }

    static ShopNameplateLayer instance;

    readonly List<Plate> plates = new List<Plate>();
    RectTransform canvasRect;
    Camera cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        GameObject host = new GameObject("ShopNameplateLayer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ShopNameplateLayer>();
    }

    void Awake()
    {
        GameObject canvasObject = new GameObject("ShopNameplateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    // 씬이 바뀌면(대기실 → 게임, 다시 하기) 옛 건물 표는 버리고 새로 모은다.
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Clear();

    void Clear()
    {
        foreach (Plate plate in plates)
            if (plate.root != null) Destroy(plate.root.gameObject);
        plates.Clear();
        cam = null;
    }

    void Collect()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(behaviour is ILaneShop)) continue;
            Transform building = FindLaneBuilding(behaviour.transform);
            if (building == null || plates.Exists(p => p.building == building)) continue;

            string key = LanePrefix.Replace(building.name, "");
            string caption = DisplayNames.TryGetValue(key, out string display) ? display : key;
            plates.Add(new Plate { building = building, caption = caption, roof = Roof(building), });
        }
    }

    // 상점 컴포넌트가 건물 뿌리가 아니라 자식에 붙어 있어도 「LaneN_」 이름을 가진 조상까지 올라간다.
    static Transform FindLaneBuilding(Transform t)
    {
        for (; t != null; t = t.parent)
            if (LanePrefix.IsMatch(t.name)) return t;
        return null;
    }

    // 그리는 것의 경계로 잰다(원점이 건물 모서리에 있을 수 있다) — 가로는 경계 한가운데, 높이는 지붕 꼭대기.
    static Vector3 Roof(Transform building)
    {
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer renderer in building.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return any ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : building.position + Vector3.up * 2f;
    }

    void CreateLabel(Plate plate)
    {
        GameObject root = new GameObject("ShopNameplate", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(160f, 56f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.fontSize = FontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Bottom;
        text.lineSpacing = -12f;   // 두 줄 이름을 한 덩어리로 붙인다
        text.raycastTarget = false;   // 건물 클릭(상점 열기)을 가로채지 않게
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = TextColor;
        // 유닛 이름표와 같은 이유로 검정 외곽선(풀밭·모래·바다 어느 바닥에서도 떨어져 보이게).
        text.outlineWidth = 0.28f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.text = plate.caption;

        plate.root = rootRect;
        plate.text = text;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        // 게임 씬의 건물은 씬이 열린 뒤 곧바로 있다 — 없으면(대기실 등) 매 프레임 다시 찾지 않도록 1초에 한 번만.
        if (plates.Count == 0)
        {
            if (Time.unscaledTime < nextCollectAt) return;
            nextCollectAt = Time.unscaledTime + 1f;
            Collect();
            if (plates.Count > 0) Debug.Log($"[상점 이름표] 건물 {plates.Count}곳");
        }

        foreach (Plate plate in plates)
        {
            if (plate.building == null || !plate.building.gameObject.activeInHierarchy)
            {
                if (plate.root != null && plate.root.gameObject.activeSelf) plate.root.gameObject.SetActive(false);
                continue;
            }

            Vector3 world = plate.roof + Vector3.up * HeadMargin;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool visible = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;

            if (!visible)
            {
                if (plate.root != null && plate.root.gameObject.activeSelf) plate.root.gameObject.SetActive(false);
                continue;
            }

            if (plate.root == null) CreateLabel(plate);
            if (!plate.root.gameObject.activeSelf) plate.root.gameObject.SetActive(true);
            plate.root.position = new Vector3(screen.x, screen.y, 0f);
        }
    }

    float nextCollectAt;
}
