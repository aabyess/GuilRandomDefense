using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 필드에 나와 있는 플레이어 유닛 머리 위 이름표(PM 지시 2026-09-23, 원작/원랜디 스타일).
// 조합표 인형이 아니라 UnitIdentity.Active(필드 등록부)를 돈다 — HealthBarLayer·
// SideBossBarLayer와 같은 자기완결형 Screen Space Overlay 캔버스 + 풀링 구조.
//
// 색은 UnitGrade.Color()(Assets/Scripts/Data/UnitData.cs)를 그대로 쓴다. 이 표는 이미
// Assets/Scripts 쪽 공용 코드다 — Assets/Editor/MapGenerator.cs의 GradeColor(grade)도
// grade.Color()를 그대로 위임할 뿐이라(에디터 전용 코드에 따로 정의돼 있지 않다) 여기서
// 새로 옮기거나 중복 정의할 게 없다.
public class UnitNameplateLayer : MonoBehaviour
{
    // HealthBarLayer(-100)보다는 위, GameHud(0)보다는 아래 — 플레이어 유닛은 체력바가 없어서
    // (UnitData.hp 주석 참고) 겹칠 대상이 없지만 관례를 맞춘다.
    const int SortingOrder = -95;

    [SerializeField] int maxLabels = 48;
    [SerializeField] float headHeightMargin = 6f;   // 머리 위로 띄우는 여유
    [SerializeField] float fontSize = 13f;
    [SerializeField] Vector2 labelSize = new Vector2(160f, 20f);

    // 거리 컬링 — 화면이 유닛으로 뒤덮이는 걸 막는다(PM 지시: "멀면 작아지거나 사라지게").
    // fadeStartDistance부터 alpha가 선형으로 줄다가 maxDistance에서 완전히 사라진다.
    [SerializeField] float fadeStartDistance = 35f;
    [SerializeField] float maxDistance = 60f;

    class Label
    {
        public RectTransform root;
        public TMP_Text text;
    }

    Camera cam;
    RectTransform canvasRect;
    readonly List<Label> pool = new List<Label>();

    void Awake()
    {
        cam = Camera.main;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("UnitNameplateCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    // 화면에 실제로 보이는 만큼만, 필요해질 때 만든다(HealthBarLayer와 같은 이유).
    Label GetOrCreateLabel(int index)
    {
        while (pool.Count <= index)
            pool.Add(CreateLabel());

        return pool[index];
    }

    Label CreateLabel()
    {
        GameObject root = new GameObject("Nameplate", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);

        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = labelSize;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        // 3D 위에 뜨는 글자라 TMP(SDF) 효과가 특히 크다 — 카메라 줌에 따라 크기가 계속 바뀌는데
        // 레거시 Text는 구워둔 비트맵을 늘려서 번졌다(2026-09-23 TMP 전환).
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Bottom;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        // 맨눈에 잘 읽히도록 검정 외곽선. TMP는 머티리얼이 외곽선을 직접 지원해서
        // 레거시 Outline 컴포넌트(정점을 네 벌 더 그리던 방식)보다 싸고 깨끗하다.
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 220);

        root.SetActive(false);
        return new Label { root = rootRect, text = text };
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
        Vector3 camPos = cam.transform.position;

        foreach (UnitIdentity identity in UnitIdentity.Active)
        {
            if (used >= maxLabels) break;
            if (identity == null) continue;

            UnitData data = identity.Data;
            if (data == null) continue;

            float distance = Vector3.Distance(camPos, identity.transform.position);
            if (distance >= maxDistance) continue;

            Vector3 worldPos = identity.transform.position + Vector3.up * (HeadHeight(identity) + headHeightMargin);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) continue;
            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height) continue;

            Label label = GetOrCreateLabel(used);
            label.root.gameObject.SetActive(true);
            label.root.position = new Vector3(screenPos.x, screenPos.y, 0f);

            if (label.text.text != data.unitName) label.text.text = data.unitName;

            Color gradeColor = data.grade.Color();
            float alpha = distance <= fadeStartDistance
                ? 1f
                : Mathf.Clamp01(1f - (distance - fadeStartDistance) / Mathf.Max(0.01f, maxDistance - fadeStartDistance));
            gradeColor.a = alpha;
            label.text.color = gradeColor;

            used++;
        }
    }

    // HealthBarLayer.HeadHeight·SideBossBarLayer.HeadHeight와 같은 계산이다 — 둘 다
    // private static이라 재사용할 수 없어 여기도 같은 방식으로 둔다.
    static float HeadHeight(UnitIdentity identity)
    {
        if (identity.TryGetComponent(out Collider body))
            return body.bounds.max.y - identity.transform.position.y;

        Renderer renderer = identity.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.max.y - identity.transform.position.y;

        return 2f;
    }
}
