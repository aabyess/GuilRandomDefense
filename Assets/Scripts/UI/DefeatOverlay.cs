using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패배했을 때 화면 한가운데에 알린다(사장님 10-09: 제목 「상호파 입단 실패」 + 워크3 금속 톤 불투명 패널).
///
/// 🔴 왜 만들었나 — **졌는데 진 줄 모른다.**(2026-09-24 긴 판: 라운드 4에 졌는데 화면엔 팀 칸의 작은 「사망」 한 줄뿐이었다.)
/// 무엇을 적는가: 왜 졌는지(원인)와 무엇을 잃었는지와 무엇이 남았는지를 RoundManager가 **실제로 하는 일**과 같은 말로.
/// GameHud와 분리된 자기완결 컴포넌트 — 맵 생성이 씬에 하나 넣어 두면 스스로 뜨고 숨는다.
/// 10-09: 같이 하기에선 「계속 지켜보기」(창만 닫고 판을 둘러본다)·「처음 화면으로」 단추를 단다(처음 화면 = 메뉴와 같은 GameHud.LeaveGameFromDefeat).
/// </summary>
public class DefeatOverlay : MonoBehaviour
{
    // GameHud(0)·난이도 선택(100)보다 위. 패배는 다른 무엇보다 먼저 보여야 한다.
    const int SortingOrder = 200;

    GameObject panel;
    TMP_Text titleText;
    TMP_Text roundText, reasonText, lostText, footText;
    GameObject spectateButton;
    bool shown, dismissed;

    /// <summary>패배 문구를 상호파 톤으로(10-09 사장님): 「패배하셨습니다」→「상호파 입단에 실패하셨습니다」 등. 원작 이식 문구는 그대로 두고 보이는 말만 바꾼다.</summary>
    public static string Tone(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("패배하셨습니다", "상호파 입단에 실패하셨습니다").Replace("패배하였습니다", "상호파 입단에 실패하였습니다")
                   .Replace("패배합니다", "상호파 입단에 실패합니다").Replace("패배하지", "입단에 실패하지").Replace("패배", "입단 실패");
    }

    void Awake() { BuildUI(); }

    void Update()
    {
        PlayerContext local = PlayerContext.Local;
        bool dead = local != null && local.IsDead;
        if (dead && !shown) { shown = true; Fill(); }
        // 메뉴가 열려 있는 동안·「계속 지켜보기」를 누른 동안은 비킨다(09-29 「게임오버 창과 메뉴 창이 겹친다」).
        if (shown) panel.SetActive(!GameHud.IsGameMenuOpen && !dismissed);
    }

    void Fill()
    {
        RoundManager rounds = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        PlayerContext local = PlayerContext.Local;
        int round = rounds != null ? rounds.CurrentRound : 0;
        bool allDead = rounds != null && rounds.IsGameOver;
        NetLauncher launcher = NetLauncher.Instance;
        bool online = launcher != null && launcher.InRoom;

        titleText.text = allDead ? "게임 오버" : "상호파 입단 실패";
        roundText.text = $"<color=#FFD138>라운드 {round}</color>";
        string reason = string.IsNullOrEmpty(local?.DefeatMessage) ? "레인에 적이 너무 많아 유닛 카운트가 0이 됐습니다." : local.DefeatMessage;
        reasonText.text = "<color=#FF9A3A>원인</color>  " + Tone(reason);
        // ⚠️ 여기 적는 문장은 RoundManager.HandlePlayerDefeated가 실제로 하는 일과 같아야 한다(골드·목재·특성 포인트 0, 내 유닛·위습·레인 적 제거).
        lostText.text = "<color=#FF9A3A>잃은 것</color>  골드 · 목재 · 특성 포인트 · 내 유닛 · 위습 · 내 레인의 적";
        footText.text = allDead ? "<color=#BBBBBB>모두가 입단에 실패했습니다. 처음 화면에서 새 판을 시작하세요.</color>"
                                : "<color=#BBBBBB>다른 플레이어가 남아 있어 게임은 계속됩니다.</color>";
        if (spectateButton != null) spectateButton.SetActive(!allDead && online);   // 같이 하기에서만 둘러보기 의미가 있다
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();   // 단추를 누르게 — 바탕(어둠막)은 raycastTarget을 꺼서 아래 게임 클릭을 안 먹는다

        panel = new GameObject("DefeatPanel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        RectTransform root = panel.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
        RectTransform dim = CreatePanel(root, "Dim", new Color(0f, 0f, 0f, 0.5f));
        Stretch(dim);

        RectTransform card = CreatePanel(root, "Card", new Color(0.03f, 0.04f, 0.08f, 1f));   // 불투명
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(860f, 600f);
        Image cardImage = card.GetComponent<Image>(); cardImage.raycastTarget = true;   // 카드 안 클릭은 게임으로 새지 않게
        RectTransform frame = CreatePanel(card, "Wc3Frame", Color.white);
        Stretch(frame);
        Sprite panelSprite = UiSkin.Wc3("menu_panel");
        if (!UiSkin.ApplyWc3(frame.GetComponent<Image>(), "menu_panel", 2f)) frame.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.12f, 1f);

        titleText = CreateLabel(card, "Title", "상호파 입단 실패", 64f, FontStyles.Bold, new Color(0.96f, 0.26f, 0.22f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-90f, 92f), new Vector2(0f, -44f));
        titleText.outlineWidth = 0.28f; titleText.outlineColor = new Color32(30, 0, 0, 255);
        roundText = CreateLabel(card, "Round", "", 34f, FontStyles.Bold, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-90f, 44f), new Vector2(0f, -150f));
        reasonText = CreateLabel(card, "Reason", "", 25f, FontStyles.Normal, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-110f, 84f), new Vector2(0f, -208f));
        lostText = CreateLabel(card, "Lost", "", 25f, FontStyles.Normal, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-110f, 84f), new Vector2(0f, -300f));
        footText = CreateLabel(card, "Foot", "", 22f, FontStyles.Normal, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-110f, 40f), new Vector2(0f, -396f));
        foreach (TMP_Text t in new[] { reasonText, lostText }) { t.alignment = TextAlignmentOptions.Left; t.textWrappingMode = TextWrappingModes.Normal; }

        // 단추: 처음 화면으로(항상) · 계속 지켜보기(같이 하기)
        TMP_Text homeLabel = GameHud.MakeWc3Button(card, "HomeButton", "처음 화면으로", new Vector2(250f, 62f), new Vector2(0.5f, 0f), new Vector2(150f, 52f), () => GameHud.LeaveGameFromDefeat());
        GameObject spectate = GameHud.MakeWc3Button(card, "SpectateButton", "계속 지켜보기", new Vector2(250f, 62f), new Vector2(0.5f, 0f), new Vector2(-150f, 52f), () => { dismissed = true; panel.SetActive(false); }).transform.parent.gameObject;
        spectateButton = spectate;
        panel.SetActive(false);
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return obj.GetComponent<RectTransform>();
    }

    static TMP_Text CreateLabel(Transform parent, string name, string content, float size, FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = sizeDelta; rect.anchoredPosition = anchoredPos;
        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        if (GameHud.UiFontAsset != null) text.font = GameHud.UiFontAsset;
        text.fontSize = size; text.fontStyle = style; text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
