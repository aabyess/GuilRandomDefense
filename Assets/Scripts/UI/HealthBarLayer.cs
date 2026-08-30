using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 자기완결형: 씬 빈 오브젝트에 붙이면 스스로 Canvas와 체력바 풀을 만든다(GameHud와 같은 구조).
// 적마다 World Space Canvas를 붙이면 캔버스 리빌드 비용 때문에 수십 마리만 돼도 프레임이 무너지므로,
// Screen Space 캔버스 하나에 바를 풀링해두고 매 프레임 위치만 옮긴다.
public class HealthBarLayer : MonoBehaviour
{
    // GameHud 캔버스(기본 0)보다 확실히 아래.
    const int HealthBarSortingOrder = -100;

    [SerializeField] int maxBars = 64;
    [SerializeField] Vector2 barSize = new Vector2(40f, 5f);
    [SerializeField] float barHeightOffset = 1.5f;
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] Color highHpColor = Color.green;
    [SerializeField] Color midHpColor = Color.yellow;
    [SerializeField] Color lowHpColor = Color.red;

    class Bar
    {
        public RectTransform root;
        public Image fill;
    }

    Camera cam;
    RectTransform canvasRect;
    readonly List<Bar> pool = new List<Bar>();

    void Awake()
    {
        cam = Camera.main;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("HealthBarCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 체력바는 월드 위에 얹히는 표시라 HUD보다 아래에 그려야 한다.
        // 기본값(0)이면 GameHud 캔버스와 순서가 정해지지 않아 하단 바를 덮을 수 있다.
        canvas.sortingOrder = HealthBarSortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    // 미리 64개를 만들면 플레이 진입 순간에 GameObject 192개가 한꺼번에 생겨 끊긴다.
    // 화면에 실제로 보이는 만큼만, 필요해질 때 만든다.
    Bar GetOrCreateBar(int index)
    {
        while (pool.Count <= index)
            pool.Add(CreateBar());

        return pool[index];
    }

    Bar CreateBar()
    {
        GameObject root = new GameObject("HealthBar", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);

        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = barSize;

        // Image가 요구하는 CanvasRenderer를 직접 나열해서 넣는다 — new GameObject(name, types)는
        // RequireComponent 체인을 못 믿을 수 있어(순서·Awake 시점 문제) 명시적으로 채운다.
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        StretchToParent(background.transform, rootRect);
        background.GetComponent<Image>().color = backgroundColor;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        StretchToParent(fillObject.transform, rootRect);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;
        fillImage.color = highHpColor;

        root.SetActive(false);

        return new Bar { root = rootRect, fill = fillImage };
    }

    static void StretchToParent(Transform child, RectTransform parent)
    {
        child.SetParent(parent, false);
        RectTransform rect = (RectTransform)child;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 적이 이동한 뒤(Update)에 따라가야 한 프레임 늦게 쫓아가며 떨리지 않는다.
    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        int used = 0;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null) continue;
            if (used >= maxBars) break;

            Vector3 worldPos = enemy.transform.position + Vector3.up * barHeightOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // z <= 0: 카메라 뒤. 화면 범위 밖: 어차피 안 보이니 자리를 낭비하지 않는다.
            if (screenPos.z <= 0f) continue;
            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height) continue;

            Bar bar = GetOrCreateBar(used);
            if (!bar.root.gameObject.activeSelf)
            {
                bar.root.gameObject.SetActive(true);
            }
            // WorldToScreenPoint의 z는 카메라까지의 거리다. Overlay 캔버스에서는 화면 좌표만 쓰므로
            // 그대로 넣으면 바가 캔버스 평면 밖으로 나간다.
            bar.root.position = new Vector3(screenPos.x, screenPos.y, 0f);

            float ratio = enemy.HpRatio;
            bar.fill.fillAmount = ratio;
            bar.fill.color = ColorForRatio(ratio);

            used++;
        }

        for (int i = used; i < pool.Count; i++)
        {
            if (pool[i].root.gameObject.activeSelf)
            {
                pool[i].root.gameObject.SetActive(false);
            }
        }
    }

    Color ColorForRatio(float ratio)
    {
        return ratio > 0.5f
            ? Color.Lerp(midHpColor, highHpColor, (ratio - 0.5f) / 0.5f)
            : Color.Lerp(lowHpColor, midHpColor, ratio / 0.5f);
    }
}
