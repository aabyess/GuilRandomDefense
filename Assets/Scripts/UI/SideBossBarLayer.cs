using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 신세계 사이드보스 전용 UI — 캐스팅바·스턴게이지바·무적 표시(ORIGINAL_BOSS_COMBAT_SPEC.md,
// PM 지시 2026-09-06: "게이지가 안 보이면 이 전투가 성립하지 않습니다. 원작도 캐스팅바·
// 스턴게이지바를 화면에 띄웁니다"). 무적 표시가 특히 중요하다 — 무적일 때 체력바가 안
// 움직이는 게 정상인데, 표시가 없으면 플레이어에게 버그로 보인다.
//
// HealthBarLayer와 같은 자기완결형 Screen Space Overlay 캔버스 + 풀링 구조를 쓰지만 별도
// 스크립트다 — HealthBarLayer의 LateUpdate는 "적 하나당 바 하나"로 고정돼 있어 그대로 얹을
// 수 없다. 사이드보스는 동시에 최대 8마리(플레이어당 1)뿐이라 SideBossEncounter.Active를
// 도는 비용은 무시할 만하다.
public class SideBossBarLayer : MonoBehaviour
{
    const int SortingOrder = -90; // HealthBarLayer(-100)보다 위, GameHud(0)보다 아래.

    [SerializeField] Vector2 barSize = new Vector2(50f, 6f);
    [SerializeField] float barHeightMargin = 14f;  // 체력바(HealthBarLayer)와 안 겹치게 그 위로 띄운다
    [SerializeField] float barSpacing = 2f;        // 바 사이 간격
    [SerializeField] Color castColor = new Color(1f, 0.55f, 0.1f);   // 주황 — 시전(§④)
    [SerializeField] Color stunColor = new Color(0.25f, 0.75f, 1f);  // 하늘 — 스턴게이지(§⑥)
    [SerializeField] Color invulnColor = new Color(1f, 1f, 1f, 0.9f);

    class Group
    {
        public RectTransform root;
        public Image castFill;
        public Image stunFill;
        public Text invulnLabel;
    }

    Camera cam;
    RectTransform canvasRect;
    readonly List<Group> pool = new List<Group>();

    void Awake()
    {
        cam = Camera.main;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("SideBossBarCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    // 화면에 실제로 보이는 만큼만, 필요해질 때 만든다(HealthBarLayer와 같은 이유).
    Group GetOrCreateGroup(int index)
    {
        while (pool.Count <= index)
            pool.Add(CreateGroup());

        return pool[index];
    }

    Group CreateGroup()
    {
        GameObject root = new GameObject("SideBossBars", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero; // 자식들이 앵커 기준으로 스스로 쌓인다.

        // 아래부터 위로: 캐스팅바 → 스턴게이지바 → 무적 라벨.
        Image castFill = CreateBar(rootRect, "CastBar", 0f, castColor);
        Image stunFill = CreateBar(rootRect, "StunGaugeBar", barSize.y + barSpacing, stunColor);

        GameObject labelObject = new GameObject("InvulnLabel", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.SetParent(rootRect, false);
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, (barSize.y + barSpacing) * 2f);
        labelRect.sizeDelta = new Vector2(barSize.x * 2f, 16f);

        Text invulnLabel = labelObject.GetComponent<Text>();
        invulnLabel.text = "무적";
        invulnLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        invulnLabel.fontSize = 12;
        invulnLabel.fontStyle = FontStyle.Bold;
        invulnLabel.alignment = TextAnchor.LowerCenter;
        invulnLabel.color = invulnColor;
        invulnLabel.raycastTarget = false;

        root.SetActive(false);
        return new Group { root = rootRect, castFill = castFill, stunFill = stunFill, invulnLabel = invulnLabel };
    }

    Image CreateBar(RectTransform parent, string barName, float yOffset, Color color)
    {
        GameObject barRoot = new GameObject(barName, typeof(RectTransform));
        RectTransform barRect = (RectTransform)barRoot.transform;
        barRect.SetParent(parent, false);
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = barSize;
        barRect.anchoredPosition = new Vector2(0f, yOffset);

        // Image가 요구하는 CanvasRenderer를 직접 나열해서 넣는다(HealthBarLayer와 같은 이유 —
        // new GameObject(name, types)는 RequireComponent 체인을 못 믿을 수 있다).
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        StretchToParent(background.transform, barRect);
        background.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        StretchToParent(fillObject.transform, barRect);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        fillImage.color = color;

        return fillImage;
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

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].root.gameObject.activeSelf)
                pool[i].root.gameObject.SetActive(false);
        }

        int used = 0;

        foreach (SideBossEncounter boss in SideBossEncounter.Active)
        {
            if (boss == null) continue;
            if (boss.CurrentStage == SideBossEncounter.Stage.Done) continue;

            Vector3 worldPos = boss.transform.position + Vector3.up * (HeadHeight(boss) + barHeightMargin);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) continue;
            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height) continue;

            Group group = GetOrCreateGroup(used);
            group.root.gameObject.SetActive(true);
            group.root.position = new Vector3(screenPos.x, screenPos.y, 0f);

            group.castFill.fillAmount = Mathf.Clamp01(boss.CastProgress / 100f);
            group.stunFill.fillAmount = Mathf.Clamp01(boss.StunGauge / 100f);
            group.invulnLabel.gameObject.SetActive(boss.IsInvulnerable);

            used++;
        }
    }

    // HealthBarLayer.HeadHeight와 같은 계산이다 — private static이라 그쪽 걸 재사용할 수
    // 없고, 사이드보스 3종뿐이라 별도 추상화를 만들 만큼은 아니다.
    static float HeadHeight(SideBossEncounter boss)
    {
        if (boss.TryGetComponent(out Collider body))
            return body.bounds.max.y - boss.transform.position.y;

        Renderer renderer = boss.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.max.y - boss.transform.position.y;

        return 2f;
    }
}
