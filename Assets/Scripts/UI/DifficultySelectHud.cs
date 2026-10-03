using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 원작 InitTrig_InitStart/Select1/Select_effect(j 15260~15285, 15324~) 대응 — 난이도는 **게임에 들어가서 방장이 정한다**(사장님 확정 10-03).
//   0.02초: 시네마틱(화면 검정 · 「잠시만 기다려주십시오」 6초) → 6.6초: 방장(1번 플레이어)에게만 대화상자 「모드를 선택하세요.」 + 버튼 6개
//   (쉬움 00bfff · 보통 ee82ee · 어려움 ff0000 · 지옥 9400d3 · 신 ffd700 · 악몽 ffd700 + 「 모드」), 전원에게 「방장이 모드를선택하고있습니다. 잠시기다려주세요.」 30초.
//   제한시간·기본값·투표 없음 — 아무도 안 누르면 영원히 대기(라운드 타이머·점수판·스토리는 선택 뒤에만 시작한다).
// 모양은 사장님 사진(Docs/reference/ui/원랜디_난이도선택.png): 검정 화면 위에 돌 테+금테 남색 판, 상단 바·하단 콘솔은 그대로 보임
// (그래서 검정 가림막은 GameHud 캔버스 **아래** 정렬, 대화상자는 위).
//
// GameHud와 완전히 분리된 자기완결 컴포넌트다(EventSystem도 스스로 보장) — 선택이 끝나면 자기 UI를 숨기기만 한다.
public class DifficultySelectHud : MonoBehaviour
{
    static readonly DifficultyMode[] Order =
    {
        DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard,
        DifficultyMode.Hell, DifficultyMode.God, DifficultyMode.Nightmare,
    };

    // 원작 버튼 글자색(j 15267~15277) — 이름 앞부분만 색, 뒤 「 모드」는 기본(흰)색.
    static string ColorHex(DifficultyMode mode)
    {
        switch (mode)
        {
            case DifficultyMode.Easy: return "00BFFF";
            case DifficultyMode.Normal: return "EE82EE";
            case DifficultyMode.Hard: return "FF0000";
            case DifficultyMode.Hell: return "9400D3";
            default: return "FFD700";   // 신 · 악몽
        }
    }

    const float CinematicSeconds = 6.6f;     // 원작 Select1 타이머(j 15284)
    const float WaitingMessageSeconds = 30f; // 원작 「방장이 모드를선택하고있습니다」 표시 시간(j 15279)
    const float WaitPleaseSeconds = 6f;      // 원작 「잠시만 기다려주십시오」(j 15209)

    GameObject blackout;      // 시네마틱 검정(선택 전 내내)
    GameObject pleaseWait;    // 「잠시만 기다려주십시오」
    GameObject hostPanel;     // 방장 대화상자
    GameObject waitingPanel;  // 나머지 플레이어 안내 문구
    float startedAt;
    bool dialogShown;

    bool IsHost => GameAuthority.LocalPlayerId == 0;

    void Awake()
    {
        startedAt = Time.unscaledTime;
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        bool selected = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected;
        float elapsed = Time.unscaledTime - startedAt;

        if (blackout != null && blackout.activeSelf == selected) blackout.SetActive(!selected);
        if (pleaseWait != null)
        {
            bool show = !selected && elapsed < WaitPleaseSeconds;
            if (pleaseWait.activeSelf != show) pleaseWait.SetActive(show);
        }

        // 6.6초 뒤에 대화상자(원작 Select1). 방장 화면엔 버튼 판, 나머지엔 안내 문구 30초.
        bool dialogTime = !selected && elapsed >= CinematicSeconds;
        if (hostPanel != null)
        {
            bool show = dialogTime;
            if (hostPanel.activeSelf != show) hostPanel.SetActive(show);
        }
        if (waitingPanel != null)
        {
            bool show = dialogTime && elapsed < CinematicSeconds + WaitingMessageSeconds;
            if (waitingPanel.activeSelf != show) waitingPanel.SetActive(show);
        }
        if (dialogTime) dialogShown = true;
    }

    void OnDestroy()
    {
        if (blackout != null) Destroy(blackout);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    void BuildUI()
    {
        // 검정 가림막 — GameHud(정렬 0) 아래, 이름표(-95)·킬골드(-94) 위. 상단 바·하단 콘솔은 가려지지 않는다.
        GameObject blackoutCanvas = new GameObject("DifficultyBlackout", typeof(RectTransform), typeof(Canvas));
        // 루트에 둔다 — 이 오브젝트의 캔버스(정렬 100)의 자식이면 정렬이 따라 올라가 HUD를 덮는다(첫 시험 때 상단 바·콘솔이 다 가려졌다).
        Canvas bc = blackoutCanvas.GetComponent<Canvas>();
        bc.renderMode = RenderMode.ScreenSpaceOverlay;
        bc.overrideSorting = true;
        bc.sortingOrder = -50;
        RectTransform black = CreatePanel(blackoutCanvas.transform, "Black", Color.black);
        SetAnchors(black, Vector2.zero, Vector2.one);
        black.GetComponent<Image>().raycastTarget = false;
        blackout = blackoutCanvas;

        // 「잠시만 기다려주십시오」 — 같은 가림막 캔버스 위에(원작 DisplayTimedText, 화면 왼쪽 가운데).
        TMP_Text please = CreateLabel(black, "PleaseWait", "잠시만 기다려주십시오");
        SetAnchors(please.rectTransform, new Vector2(0.06f, 0.46f), new Vector2(0.5f, 0.54f));
        please.alignment = TextAlignmentOptions.Left;
        please.fontSize = 28;
        pleaseWait = please.gameObject;

        // 대화상자·안내 문구는 GameHud 위 캔버스
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
        if (hostPanel != null) hostPanel.SetActive(false);
        if (waitingPanel != null) waitingPanel.SetActive(false);
        if (pleaseWait != null) pleaseWait.SetActive(true);
    }

    void BuildHostPanel()
    {
        // 사진: 판 가운데, 폭 ≈33% × 높이 ≈48%(6버튼이라 사진 5버튼보다 조금 높게).
        RectTransform panel = CreatePanel(transform, "DifficultyHostPanel", new Color(0.04f, 0.07f, 0.18f, 1f));
        SetAnchors(panel, new Vector2(0.335f, 0.20f), new Vector2(0.665f, 0.80f));
        UiSkin.Apply(panel.GetComponent<Image>(), "dialog_panel_9s", new Color(0.04f, 0.07f, 0.18f, 1f));
        hostPanel = panel.gameObject;

        TMP_Text title = CreateLabel(panel, "Title", "모드를 선택하세요.");
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f));
        title.fontSize = 30;
        title.color = new Color(1f, 0.84f, 0.25f);

        // 버튼 6개 세로(남색+금테). 위에서 아래로 쉬움…악몽.
        float top = 0.80f;
        const float height = 0.105f;
        const float gap = 0.022f;
        for (int i = 0; i < Order.Length; i++)
        {
            DifficultyMode mode = Order[i];
            float bottom = top - height;
            RectTransform buttonRect = CreatePanel(panel, $"Button_{mode}", new Color(0.1f, 0.16f, 0.4f, 1f));
            SetAnchors(buttonRect, new Vector2(0.09f, bottom), new Vector2(0.91f, top));
            Image background = buttonRect.GetComponent<Image>();
            UiSkin.Apply(background, "button_navy_9s", new Color(0.1f, 0.16f, 0.4f, 1f));

            Button button = buttonRect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.45f, 1f);   // 마우스를 올리면 밝아짐(사진의 지옥 버튼)
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            button.colors = colors;
            DifficultyMode capturedMode = mode;
            button.onClick.AddListener(() => OnModeButtonClicked(capturedMode));

            TMP_Text label = CreateLabel(buttonRect, "Label", $"<color=#{ColorHex(mode)}>{mode.KoreanName()}</color> 모드");
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
            label.fontSize = 26;
            label.raycastTarget = false;
            top = bottom - gap;
        }
    }

    void BuildWaitingPanel()
    {
        // 원작 DisplayTimedTextToForce — 화면 왼쪽 가운데 아래(사진: x 6~34%, y 65~70%), 방장 화면에도 같은 문구가 뜬다.
        TMP_Text label = CreateLabel(transform, "WaitingText", "방장이 모드를선택하고있습니다. 잠시기다려주세요.");
        SetAnchors(label.rectTransform, new Vector2(0.06f, 0.30f), new Vector2(0.34f, 0.36f));
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 24;
        label.raycastTarget = false;
        waitingPanel = label.gameObject;
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

    static TMP_Text CreateLabel(Transform parent, string name, string content)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.text = content;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
