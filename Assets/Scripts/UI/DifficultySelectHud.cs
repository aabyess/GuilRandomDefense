using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 원작 InitTrig_Select1/Select_effect(Docs/reference/DIFFICULTY_SPEC_2026-09-11.md §7) 대응.
// 게임 시작 직후 호스트(Player 0)에게만 6버튼 창을 띄운다 — 제한시간·기본값이 없다(원작
// 그대로: 아무도 안 누르면 게임은 영원히 대기한다). 다른 플레이어에겐 원작 안내 문구
// ("방장이 모드를선택하고있습니다. 잠시 기다려주세요.")만 보여주고 버튼은 없다.
//
// GameHud와 완전히 분리된 자기완결 컴포넌트다(EventSystem도 스스로 보장) — 선택이 끝나면
// 자기 UI를 숨기기만 하고 사라지지 않는다(다시 볼 일은 없지만 파괴할 이유도 없다).
public class DifficultySelectHud : MonoBehaviour
{
    static readonly DifficultyMode[] Order =
    {
        DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard,
        DifficultyMode.Hell, DifficultyMode.God, DifficultyMode.Nightmare,
    };

    GameObject hostPanel;
    GameObject waitingPanel;

    bool IsHost => GameAuthority.LocalPlayerId == 0;

    void Awake()
    {
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        bool selected = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected;

        if (hostPanel != null && hostPanel.activeSelf == selected) hostPanel.SetActive(!selected);
        if (waitingPanel != null && waitingPanel.activeSelf == selected) waitingPanel.SetActive(!selected);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 게임 시작 직후라 겹칠 다른 모달이 없다 — GameHud 위에만 뜨면 된다.

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        if (IsHost) BuildHostPanel();
        else BuildWaitingPanel();
    }

    void BuildHostPanel()
    {
        RectTransform panel = CreatePanel(transform, "DifficultyHostPanel", new Color(0f, 0f, 0f, 0.85f));
        SetAnchors(panel, new Vector2(0.25f, 0.28f), new Vector2(0.75f, 0.72f));
        hostPanel = panel.gameObject;

        RectTransform titleRect = CreateChildRect(panel, "Title", new Vector2(0f, 0.78f), new Vector2(1f, 1f));
        Text title = titleRect.gameObject.AddComponent<Text>();
        SetupLabel(title, "난이도를 선택하세요");
        title.fontSize = 30;

        for (int i = 0; i < Order.Length; i++)
        {
            DifficultyMode mode = Order[i];
            int column = i % 3;
            int row = i / 3; // 0=위 행, 1=아래 행

            float left = 0.03f + column * 0.32f;
            float right = left + 0.29f;
            float top = row == 0 ? 0.68f : 0.32f;
            float bottom = top - 0.30f;

            RectTransform buttonRect = CreateChildRect(panel, $"Button_{mode}", new Vector2(left, bottom), new Vector2(right, top));
            Image background = buttonRect.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.15f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            DifficultyMode capturedMode = mode;
            button.onClick.AddListener(() => OnModeButtonClicked(capturedMode));

            RectTransform labelRect = CreateChildRect(buttonRect, "Label", Vector2.zero, Vector2.one);
            Text label = labelRect.gameObject.AddComponent<Text>();
            SetupLabel(label, mode.KoreanName());
            label.fontSize = 24;
            label.raycastTarget = false;
        }
    }

    void BuildWaitingPanel()
    {
        RectTransform panel = CreatePanel(transform, "DifficultyWaitingPanel", new Color(0f, 0f, 0f, 0.7f));
        SetAnchors(panel, new Vector2(0.25f, 0.44f), new Vector2(0.75f, 0.56f));
        waitingPanel = panel.gameObject;

        Text label = CreateLabel(panel, "WaitingText",
            "방장이 모드를선택하고있습니다. 잠시 기다려주세요."); // 원작 안내문 원문 그대로.
        label.fontSize = 22;
        label.raycastTarget = false;
    }

    void OnModeButtonClicked(DifficultyMode mode)
    {
        if (DifficultyManager.Instance == null) return;
        DifficultyManager.Instance.SelectMode(mode);
    }

    static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj.GetComponent<RectTransform>();
    }

    static RectTransform CreateChildRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static Text CreateLabel(Transform parent, string name, string content)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        Text text = obj.GetComponent<Text>();
        SetupLabel(text, content);
        return text;
    }

    static void SetupLabel(Text text, string content)
    {
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
