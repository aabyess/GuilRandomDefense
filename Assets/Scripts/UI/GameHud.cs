using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 화면 하단 상시 HUD (원랜디/워크래프트3 스타일). 좌: 미니맵 자리, 중앙: 선택 유닛 정보, 우: 명령 카드 그리드 자리.
// 프리팹 루트에 이 스크립트 하나만 붙여두면 Awake에서 전체 uGUI 계층을 스스로 구성한다
// (에디터 없이 만든 프리팹이 손으로 짠 UI 계층 때문에 깨지는 걸 피하기 위한 구조).
public class GameHud : MonoBehaviour
{
    const int CommandSlotCount = 12;
    const int CommandColumns = 3;
    const int TeamSlotCount = 4;
    const int MaxSelectionCards = 12;
    const int SelectionCardColumns = 4;   // 유닛 정보 칸이 좁아져 6열은 넘친다 (12칸 = 4열 3행)
    const int MaxInventoryEntries = 16;

    [SerializeField] SelectionManager selectionManager;

    Text unitInfoText;
    Text goldWoodText;
    Text roundTimeText;
    Text teamPanelText;
    Text storyText;
    Text inventoryText;
    GameObject inventoryPanelObject;

    GameObject unitCardsPanel;
    readonly GameObject[] cardRoots = new GameObject[MaxSelectionCards];
    readonly Image[] cardBackgrounds = new Image[MaxSelectionCards];
    readonly Text[] cardNames = new Text[MaxSelectionCards];
    readonly Text[] cardOverflowTexts = new Text[MaxSelectionCards];
    readonly Selectable[] lastCardTargets = new Selectable[MaxSelectionCards];
    int lastCardShownCount = -1;
    int lastOverflow = -1;

    readonly StringBuilder teamPanelBuilder = new StringBuilder(256);

    // 매 프레임 도는 경로라 비교용 버퍼를 재사용한다. 여기서 new를 하면
    // 변경 감지로 아낀 것보다 할당이 더 나온다.
    readonly int[] slotEnemy = new int[TeamSlotCount];
    readonly int[] slotGold = new int[TeamSlotCount];
    readonly int[] slotWood = new int[TeamSlotCount];
    readonly bool[] slotHas = new bool[TeamSlotCount];

    RoundManager roundManager;
    CombineSystem combineSystem;

    // 조합 카드(레시피) 12칸. 유닛 카드와 같은 패턴 — 미리 만들어두고 내용만 바꾼다.
    const float RecipeRefreshInterval = 0.4f;
    static readonly List<CombineRecipe> EmptyRecipes = new List<CombineRecipe>();

    // 조합 카드/도움소 스킬 칸 위에 뜨는 툴팁 — 종류가 둘이어도 오브젝트는 하나만 공유해서
    // 위치·내용만 바꾼다(두 번째를 만들면 캔버스 리빌드가 늘어난다).
    readonly StringBuilder tooltipBuilder = new StringBuilder(256);
    GameObject combineTooltipObject;
    Text combineTooltipText;

    // 호버 중인 칸의 툴팁은 마우스가 그 위에 머무는 동안 주기적으로 다시 그린다 —
    // 도움소 스킬의 "재사용까지 N초"처럼 시간이 지나면 바뀌는 값이 있어서다.
    const float TooltipRefreshInterval = 0.2f;
    int hoveredCommandSlotIndex = -1;
    float nextTooltipRefreshTime;

    // 유닛 명령 그리드(12칸). 0~2번은 공격/정지/모으기 고정 placeholder라 절대 안 건드림.
    // 나머지 칸 중 맨 아랫줄부터 왼쪽→오른쪽, 넘치면 그 윗줄로 이어지는 순서로 "선택한 유닛이
    // 무엇이 되는가"(조합 결과)를 채운다. 한 유닛이 최대 4개 레시피의 첫 재료라 이 정도면 충분하다.
    static readonly int[] UnitCommandResultSlotOrder = { 9, 10, 11, 6, 7, 8, 3, 4, 5 };

    readonly GameObject[] unitCommandSlotRoots = new GameObject[CommandSlotCount];
    readonly Image[] unitCommandSlotBackgrounds = new Image[CommandSlotCount];
    readonly Text[] unitCommandSlotNames = new Text[CommandSlotCount];
    readonly Button[] unitCommandSlotButtons = new Button[CommandSlotCount];
    readonly CombineRecipe[] unitCommandRecipes = new CombineRecipe[CommandSlotCount];

    UnitData lastCommandUnitData;
    int unitCommandSlotCount;
    float nextUnitCommandDimRefreshTime;

    // 도움소 선택 시 같은 12칸을 스킬 표시로 돌려쓴다. currentShop이 non-null이면
    // RefreshUnitCommandCards가 조합 로직 대신 이 경로로 빠진다.
    static readonly Color SupportSkillColor = new Color(0.25f, 0.55f, 0.9f, 0.9f);
    readonly SupportSkillData[] unitCommandSkills = new SupportSkillData[CommandSlotCount];
    SupportShop currentShop;
    SupportSkillData pendingSkill;   // 커서로 대상을 찍기를 기다리는 스킬 (null이면 대기 아님)
    int targetingStartFrame;        // 스킬을 고른 바로 그 클릭이 대상 클릭으로 다시 잡히지 않게

    // 값이 안 바뀌면 문자열을 새로 만들지 않기 위한 마지막 표시값 캐시.
    int lastGold = int.MinValue;
    int lastWood = int.MinValue;
    int lastRound = int.MinValue;
    int lastTimeTenths = int.MinValue;

    // StoryManager.Instance는 씬에 없을 수도, 나중에 생길 수도 있어 캐시하지 않고 매번 읽는다.
    bool lastStoryVisible;
    bool lastStoryRunning;
    string lastStoryLabel;
    int lastStorySeconds = int.MinValue;

    // 인벤토리는 자주 안 바뀌므로 UnitInventory.OnInventoryChanged를 구독해서 실제로 바뀔 때만
    // dirty 플래그를 세운다 — 매 프레임 목록을 비교하지 않는다. 집계·정렬용 컬렉션은 재사용한다.
    readonly StringBuilder inventoryBuilder = new StringBuilder(512);
    readonly Dictionary<UnitData, int> inventoryCounts = new Dictionary<UnitData, int>();
    readonly List<UnitData> inventoryKeys = new List<UnitData>();
    UnitInventory subscribedInventory;
    bool inventoryDirty = true;

    bool teamPanelInitialized;
    int lastTotalEnemyCount = int.MinValue;
    int lastDeathCount = int.MinValue;
    readonly int[] lastSlotEnemyCount = new int[TeamSlotCount];
    readonly int[] lastSlotGold = new int[TeamSlotCount];
    readonly int[] lastSlotWood = new int[TeamSlotCount];
    readonly bool[] lastSlotHasContext = new bool[TeamSlotCount];

    SelectionManager Selection => selectionManager != null
        ? selectionManager
        : selectionManager = FindFirstObjectByType<SelectionManager>();

    RoundManager RoundManagerRef => roundManager != null
        ? roundManager
        : roundManager = FindFirstObjectByType<RoundManager>();

    CombineSystem CombineSystemRef => combineSystem != null
        ? combineSystem
        : combineSystem = FindFirstObjectByType<CombineSystem>();

    void Awake()
    {
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        RefreshSelectionPanel();
        RefreshTopBar();
        RefreshTeamPanel();
        RefreshStoryPanel();
        RefreshUnitCommandCards();
        RefreshInventoryPanel();
        RefreshSupportSkillTargeting();
        RefreshHoveredTooltip();
    }

    void OnDestroy()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnInventoryChanged -= OnLocalInventoryChanged;
        }
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    void BuildUI()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        RectTransform bar = CreatePanel(transform, "BottomBar", new Color(0f, 0f, 0f, 0.75f));
        SetAnchors(bar, new Vector2(0f, 0f), new Vector2(1f, 0.22f));

        // 원작 배치: [미니맵] [조합 카드] [선택 유닛 정보] [유닛 명령(공격/정지/모으기/스킬)]
        RectTransform minimapPanel = CreatePanel(bar, "MinimapPanel", new Color(1f, 1f, 1f, 0.08f));
        SetAnchors(minimapPanel, new Vector2(0.01f, 0.05f), new Vector2(0.15f, 0.95f));
        BuildMinimap(minimapPanel);

        RectTransform infoPanel = CreatePanel(bar, "UnitInfoPanel", new Color(1f, 1f, 1f, 0.05f));
        SetAnchors(infoPanel, new Vector2(0.16f, 0.05f), new Vector2(0.65f, 0.95f));
        unitInfoText = CreateLabel(infoPanel, "UnitInfoText", "선택된 유닛 없음");
        unitInfoText.alignment = TextAnchor.UpperLeft;
        unitInfoText.fontSize = 30;
        unitInfoText.lineSpacing = 1.15f;
        unitInfoText.horizontalOverflow = HorizontalWrapMode.Wrap;

        BuildSelectionCards(infoPanel);

        RectTransform commandPanel = CreatePanel(bar, "UnitCommandPanel", new Color(1f, 1f, 1f, 0.05f));
        SetAnchors(commandPanel, new Vector2(0.66f, 0.05f), new Vector2(0.99f, 0.95f));
        BuildUnitCommandGrid(commandPanel);

        BuildTopBar();
        BuildStoryPanel();
        BuildTeamPanel();
        BuildInventoryPanel();

        // 툴팁은 맨 마지막에 만들어야 형제 순서상 가장 나중에 그려져서(항상 위) 다른 패널에 안 가려진다.
        BuildCombineTooltip();
    }

    // 좌측 세로 패널. 하단 HUD(y 0~0.22)·상단 바(0.95~1)·스토리 줄(0.90~0.95)을 피해서
    // 그 사이 여유 공간에 둔다. 명령 카드 그리드(우측)와는 별개.
    void BuildInventoryPanel()
    {
        RectTransform panel = CreatePanel(transform, "InventoryPanel", new Color(0f, 0f, 0f, 0.6f));
        SetAnchors(panel, new Vector2(0.01f, 0.30f), new Vector2(0.16f, 0.88f));
        inventoryPanelObject = panel.gameObject;

        inventoryText = CreateLabel(panel, "InventoryText", "");
        inventoryText.alignment = TextAnchor.UpperLeft;
        inventoryText.fontSize = 18;
        inventoryText.lineSpacing = 1.1f;
        inventoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        inventoryText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    // 상단 바가 이미 골드·목재·라운드·타이머로 차 있어 그 아래 별도 줄로 뺀다.
    // 팀 현황판(우측 상단)과 겹치지 않게 좌측 절반만 쓴다.
    void BuildStoryPanel()
    {
        RectTransform storyPanel = CreatePanel(transform, "StoryPanel", Color.clear);
        SetAnchors(storyPanel, new Vector2(0.01f, 0.90f), new Vector2(0.5f, 0.95f));

        storyText = CreateLabel(storyPanel, "StoryText", "");
        storyText.alignment = TextAnchor.MiddleLeft;
        storyText.fontSize = 20;
        storyText.gameObject.SetActive(false);
    }

    void BuildTopBar()
    {
        RectTransform topBar = CreatePanel(transform, "TopBar", new Color(0f, 0f, 0f, 0.75f));
        SetAnchors(topBar, new Vector2(0f, 0.95f), new Vector2(1f, 1f));

        RectTransform resourcePanel = CreatePanel(topBar, "ResourcePanel", Color.clear);
        SetAnchors(resourcePanel, new Vector2(0.01f, 0f), new Vector2(0.35f, 1f));
        goldWoodText = CreateLabel(resourcePanel, "ResourceText", "골드 -   목재 -");
        goldWoodText.alignment = TextAnchor.MiddleLeft;
        goldWoodText.fontSize = 22;

        RectTransform roundPanel = CreatePanel(topBar, "RoundPanel", Color.clear);
        SetAnchors(roundPanel, new Vector2(0.36f, 0f), new Vector2(0.64f, 1f));
        roundTimeText = CreateLabel(roundPanel, "RoundTimeText", "라운드 -   남은시간 -");
        roundTimeText.fontSize = 22;

        RectTransform menuButtonsPanel = CreatePanel(topBar, "TopBarButtons", Color.clear);
        SetAnchors(menuButtonsPanel, new Vector2(0.66f, 0.08f), new Vector2(0.99f, 0.92f));

        HorizontalLayoutGroup layout = menuButtonsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // 동작 없음 — 메뉴/동맹/대화 기능이 아직 없어 원작 배치만 재현한다.
        CreateTopBarButton(menuButtonsPanel, "MenuButton", "메뉴");
        CreateTopBarButton(menuButtonsPanel, "AllianceButton", "동맹");
        CreateTopBarButton(menuButtonsPanel, "ChatButton", "대화");
    }

    static void CreateTopBarButton(Transform parent, string name, string label)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        obj.GetComponent<LayoutElement>().preferredWidth = 90f;

        Text text = CreateLabel(obj.transform, name + "Label", label);
        text.fontSize = 18;
    }

    void BuildTeamPanel()
    {
        // 화면 좌측 위·우측 위는 DebugHud(OnGUI)가 쓰고 있어, 상단 바 바로 아래에서 시작한다.
        RectTransform teamPanel = CreatePanel(transform, "TeamPanel", new Color(0f, 0f, 0f, 0.6f));
        SetAnchors(teamPanel, new Vector2(0.71f, 0.70f), new Vector2(0.99f, 0.95f));

        teamPanelText = CreateLabel(teamPanel, "TeamPanelText", "");
        teamPanelText.alignment = TextAnchor.UpperLeft;
        teamPanelText.fontSize = 20;
        teamPanelText.lineSpacing = 1.1f;
        teamPanelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        teamPanelText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    static void BuildMinimap(RectTransform parent)
    {
        GameObject obj = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage), typeof(MinimapCamera), typeof(RectMask2D));
        obj.transform.SetParent(parent, false);

        // 미니맵 점은 초당 10회 다시 그려야 한다. 같은 캔버스에 있으면 그때마다 HUD 전체가
        // 다시 빌드돼서, 나머지 패널을 "값이 바뀔 때만 갱신"하도록 맞춰둔 게 의미가 없어진다.
        // 중첩 캔버스로 떼어내면 리빌드가 이 안에서 끝난다.
        Canvas nested = obj.AddComponent<Canvas>();
        nested.overrideSorting = true;
        nested.sortingOrder = 1;
        obj.AddComponent<GraphicRaycaster>();   // 중첩 캔버스는 자기 레이캐스터가 있어야 클릭이 먹는다

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 시야 표시는 RawImage와 별개의 CanvasRenderer가 필요해 자식 오브젝트로 둔다.
        // 같은 크기로 꽉 채워야 MinimapCamera.WorldToMinimapLocal이 계산하는 로컬 좌표계와 일치한다.
        // new GameObject(name, types)는 [RequireComponent]를 채워주지 않는다.
        // Graphic 계열은 CanvasRenderer 없이는 Awake에서 바로 예외가 난다.
        GameObject indicatorObj = new GameObject("ViewportIndicator",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapViewportIndicator));
        indicatorObj.transform.SetParent(obj.transform, false);

        RectTransform indicatorRect = (RectTransform)indicatorObj.transform;
        indicatorRect.anchorMin = Vector2.zero;
        indicatorRect.anchorMax = Vector2.one;
        indicatorRect.offsetMin = Vector2.zero;
        indicatorRect.offsetMax = Vector2.zero;

        // 부모가 붙은 뒤에 물려준다. Awake는 SetParent보다 먼저 돌아서 스스로는 못 찾는다.
        indicatorObj.GetComponent<MinimapViewportIndicator>()
            .SetMinimap(obj.GetComponent<MinimapCamera>());

        // 유닛·위습·적 점 표시. 시야 표시(흰 사각형)보다 나중에 만들어서 형제 순서상 그 위에 그려지게 한다.
        GameObject blipsObj = new GameObject("MinimapBlips",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapBlips));
        blipsObj.transform.SetParent(obj.transform, false);

        RectTransform blipsRect = (RectTransform)blipsObj.transform;
        blipsRect.anchorMin = Vector2.zero;
        blipsRect.anchorMax = Vector2.one;
        blipsRect.offsetMin = Vector2.zero;
        blipsRect.offsetMax = Vector2.zero;

        blipsObj.GetComponent<MinimapBlips>().SetMinimap(obj.GetComponent<MinimapCamera>());
    }

    // 카드 12개를 미리 만들어두고 선택이 바뀔 때만 켜고 끈다 — 매 프레임 새로 만들지 않는다.
    void BuildSelectionCards(RectTransform parent)
    {
        unitCardsPanel = new GameObject("SelectionCardsPanel", typeof(RectTransform));
        unitCardsPanel.transform.SetParent(parent, false);

        RectTransform cardsRect = (RectTransform)unitCardsPanel.transform;
        cardsRect.anchorMin = Vector2.zero;
        cardsRect.anchorMax = Vector2.one;
        cardsRect.offsetMin = Vector2.zero;
        cardsRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = unitCardsPanel.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(58f, 58f);
        grid.spacing = new Vector2(4f, 4f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SelectionCardColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < MaxSelectionCards; i++)
            BuildCard(i, unitCardsPanel.transform);

        unitCardsPanel.SetActive(false);
    }

    void BuildCard(int index, Transform parent)
    {
        GameObject card = new GameObject($"Card{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;

        int capturedIndex = index;
        card.GetComponent<Button>().onClick.AddListener(() => OnCardClicked(capturedIndex));

        GameObject portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObj.transform.SetParent(card.transform, false);
        RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.1f, 0.35f);
        portraitRect.anchorMax = new Vector2(0.9f, 0.95f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;

        Image portrait = portraitObj.GetComponent<Image>();
        portrait.sprite = null; // 초상화는 나중에 아트가 들어오면 꽂는다.
        portrait.color = new Color(1f, 1f, 1f, 0.3f);
        portrait.raycastTarget = false;

        Text nameText = CreateLabel(card.transform, "Name", "");
        nameText.raycastTarget = false;
        nameText.fontSize = 11;
        nameText.alignment = TextAnchor.UpperCenter;
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.35f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        Text overflowText = CreateLabel(card.transform, "Overflow", "");
        overflowText.raycastTarget = false;
        overflowText.fontSize = 14;
        overflowText.fontStyle = FontStyle.Bold;
        overflowText.color = Color.white;
        RectTransform overflowRect = overflowText.rectTransform;
        overflowRect.anchorMin = new Vector2(0.55f, 0.7f);
        overflowRect.anchorMax = new Vector2(1f, 1f);
        overflowRect.offsetMin = Vector2.zero;
        overflowRect.offsetMax = Vector2.zero;
        overflowText.gameObject.SetActive(false);

        cardRoots[index] = card;
        cardBackgrounds[index] = background;
        cardNames[index] = nameText;
        cardOverflowTexts[index] = overflowText;
    }

    void OnCardClicked(int index)
    {
        if (index < 0 || index >= lastCardTargets.Length) return;

        Selectable target = lastCardTargets[index];
        if (target == null) return;

        Selection?.SelectOnly(target);
    }

    // UnitIdentity가 없는 대상(위습 등) 카드 배경색.
    static readonly Color UnidentifiedCardColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);

    // 전설적인=빨강, 희귀함=보라, 특별함=노랑, 히든=파랑, 흔함·안흔함=초록, 나머지=회색.
    static Color GetGradeColor(UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Legendary: return new Color(0.85f, 0.2f, 0.2f, 0.9f);
            case UnitGrade.Rare: return new Color(0.6f, 0.3f, 0.85f, 0.9f);
            case UnitGrade.Special: return new Color(0.9f, 0.85f, 0.2f, 0.9f);
            case UnitGrade.Hidden: return new Color(0.25f, 0.45f, 0.9f, 0.9f);
            case UnitGrade.Common:
            case UnitGrade.Uncommon: return new Color(0.3f, 0.7f, 0.35f, 0.9f);
            default: return new Color(0.4f, 0.4f, 0.4f, 0.9f);
        }
    }



    static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }



    void OnCombineCardHoverExit()
    {
        hoveredCommandSlotIndex = -1;
        HideCombineTooltip();
    }

    // 하단 바 안에 그리면 그 좁은 영역 안에서 잘리므로, 카드 위쪽에 절대 좌표로 띄운다.
    // ScreenSpaceOverlay 캔버스라 RectTransform.position이 곧 화면 픽셀 좌표라 이렇게 계산할 수 있다.
    // 조합 카드·도움소 스킬 칸 양쪽에서 공유해서 쓴다 — 종류별로 만들지 않는다.
    void ShowTooltip(string text, RectTransform cardRect)
    {
        if (combineTooltipObject == null || string.IsNullOrEmpty(text) || cardRect == null) return;

        // 0.2초마다 다시 불리는데, 대개 내용은 그대로다(쿨다운이 도는 스킬만 바뀐다).
        // Text.text에 같은 값을 다시 넣어도 캔버스는 통째로 다시 그려지므로, 바뀔 때만 넣는다.
        if (combineTooltipText.text != text) combineTooltipText.text = text;

        RectTransform tooltipRect = (RectTransform)combineTooltipObject.transform;
        float halfHeight = cardRect.rect.height * cardRect.lossyScale.y * 0.5f;
        tooltipRect.position = cardRect.position + new Vector3(0f, halfHeight + 12f, 0f);

        combineTooltipObject.SetActive(true);
    }

    void HideCombineTooltip()
    {
        if (combineTooltipObject != null) combineTooltipObject.SetActive(false);
    }

    // 마우스가 카드에 올라간 순간과, 그 뒤로는 TooltipRefreshInterval마다 다시 불린다
    // (RefreshHoveredTooltip) — 매 프레임 문자열을 새로 만들지 않는다.
    void ShowHoveredTooltipNow(int index)
    {
        if (index < 0 || index >= unitCommandSlotRoots.Length || unitCommandSlotRoots[index] == null) return;

        RectTransform cardRect = (RectTransform)unitCommandSlotRoots[index].transform;

        if (currentShop != null)
        {
            SupportSkillData skill = index < unitCommandSkills.Length ? unitCommandSkills[index] : null;
            if (skill == null) { HideCombineTooltip(); return; }
            ShowTooltip(BuildSupportSkillTooltipText(skill), cardRect);
        }
        else
        {
            CombineRecipe recipe = index < unitCommandRecipes.Length ? unitCommandRecipes[index] : null;
            if (recipe == null) { HideCombineTooltip(); return; }
            ShowTooltip(BuildRecipeTooltipText(recipe), cardRect);
        }
    }

    void RefreshHoveredTooltip()
    {
        if (hoveredCommandSlotIndex < 0) return;
        if (Time.time < nextTooltipRefreshTime) return;

        nextTooltipRefreshTime = Time.time + TooltipRefreshInterval;
        ShowHoveredTooltipNow(hoveredCommandSlotIndex);
    }

    // 원작 조합표와 같은 순서: 재료 + 재료 + 재료 = 결과. 골드·자원 비용은 재료와 헷갈리지
    // 않게 괄호로 묶어 다음 줄에 둔다.
    string BuildRecipeTooltipText(CombineRecipe recipe)
    {
        tooltipBuilder.Clear();

        bool first = true;

        if (recipe.ingredients != null)
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient == null) continue;

                string label = IngredientLabel(ingredient);
                if (label == null) continue;

                int count = Mathf.Max(1, ingredient.count);
                for (int i = 0; i < count; i++)
                {
                    if (!first) tooltipBuilder.Append(" + ");
                    tooltipBuilder.Append(label);
                    first = false;
                }
            }
        }

        string resultName = recipe.result != null ? recipe.result.unitName : "?";
        tooltipBuilder.Append(" = ").Append(resultName);

        bool firstCost = true;

        if (recipe.goldCost > 0)
        {
            tooltipBuilder.Append(firstCost ? "\n(" : ", ").Append("골드 ").Append(recipe.goldCost);
            firstCost = false;
        }

        if (recipe.resourceCosts != null)
        {
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
            {
                if (cost == null || cost.amount <= 0) continue;

                tooltipBuilder.Append(firstCost ? "\n(" : ", ").Append(ResourceLabel(cost.type)).Append(' ').Append(cost.amount);
                firstCost = false;
            }
        }

        if (!firstCost) tooltipBuilder.Append(')');

        return tooltipBuilder.ToString();
    }

    // 스킬 이름·효과 서술(SupportSkillData.description)은 그대로 옮기고, 비용/쿨다운/피해량/범위/
    // 지속시간처럼 수치인 부분만 코드가 채운다 — 스킬마다 분기 없이 필드값으로만 조립된다.
    string BuildSupportSkillTooltipText(SupportSkillData skill)
    {
        tooltipBuilder.Clear();
        tooltipBuilder.Append(skill.skillName);

        if (!string.IsNullOrEmpty(skill.description))
            tooltipBuilder.Append('\n').Append(skill.description);

        tooltipBuilder.Append("\n비용: ");
        bool hasCost = false;

        if (skill.manaCost > 0)
        {
            tooltipBuilder.Append("마나 ").Append(skill.manaCost);
            hasCost = true;
        }

        if (skill.goldCost > 0)
        {
            if (hasCost) tooltipBuilder.Append(" + ");
            tooltipBuilder.Append("골드 ").Append(skill.goldCost);
            hasCost = true;
        }

        if (!hasCost) tooltipBuilder.Append("없음");

        tooltipBuilder.Append("\n쿨다운: ").Append(skill.cooldownSeconds.ToString("0.#")).Append('s');

        float remaining = currentShop != null ? currentShop.GetCooldownRemaining(skill) : 0f;
        if (remaining > 0f)
            tooltipBuilder.Append(" (재사용까지 ").Append(remaining.ToString("F1")).Append("s)");

        if (skill.damageBase > 0f || skill.damagePerRound > 0f)
        {
            RoundManager rm = RoundManagerRef;
            int round = rm != null ? rm.CurrentRound : 1;
            tooltipBuilder.Append("\n피해량: ").Append(skill.ComputeDamage(round).ToString("F0"))
                .Append(" (").Append(round).Append("라운드 기준)");
        }

        if (skill.targetKind == SupportSkillTargetKind.Ground)
        {
            tooltipBuilder.Append("\n범위: ").Append(skill.mapWide ? "맵 전체" : $"반경 {skill.radius:0.#}");
        }

        if (skill.duration > 0f)
            tooltipBuilder.Append("\n지속시간: ").Append(skill.duration.ToString("0.#")).Append('s');

        return tooltipBuilder.ToString();
    }

    static string IngredientLabel(RecipeIngredient ingredient)
    {
        switch (ingredient.kind)
        {
            case IngredientKind.SpecificUnit:
                return ingredient.unit != null ? ingredient.unit.unitName : null;
            case IngredientKind.SpecificItem:
                return ingredient.item != null ? ingredient.item.itemName : null;
            case IngredientKind.UnitGradeWildcard:
                return ingredient.wildcardGrade.KoreanName() + "아무거나";
            default:
                return null;
        }
    }

    static string ResourceLabel(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "목재";
            case ResourceType.Token: return "토큰";
            case ResourceType.LuckyToken: return "행운의토큰";
            case ResourceType.Mana: return "마나";
            default: return type.ToString();
        }
    }

    void BuildCombineTooltip()
    {
        GameObject tooltipObj = new GameObject("CombineTooltip", typeof(RectTransform), typeof(Image));
        tooltipObj.transform.SetParent(transform, false);

        RectTransform rect = tooltipObj.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(440f, 110f);

        Image background = tooltipObj.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.9f);
        background.raycastTarget = false; // 켜져 있으면 툴팁이 카드를 가려서 PointerExit로 처리된다.

        combineTooltipText = CreateLabel(tooltipObj.transform, "TooltipText", "");
        combineTooltipText.raycastTarget = false;
        combineTooltipText.fontSize = 16;
        combineTooltipText.alignment = TextAnchor.UpperLeft;
        combineTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;

        combineTooltipObject = tooltipObj;
        combineTooltipObject.SetActive(false);
    }

    // 유닛 명령 자리(공격/정지/모으기/스킬 + 맨 아랫줄은 조합 결과). 0~2번은 공격/정지/모으기
    // 고정 placeholder로 인터랙션을 꺼둔다. 나머지는 RefreshUnitCommandCards가 채운다.
    static readonly Color UnitCommandDefaultColor = new Color(1f, 1f, 1f, 0.12f);

    void BuildUnitCommandGrid(RectTransform parent)
    {
        GridLayoutGroup grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90f, 50f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CommandColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        string[] placeholderLabels = { "공격", "정지", "모으기" };

        for (int i = 0; i < CommandSlotCount; i++)
        {
            BuildUnitCommandSlot(i, parent);

            if (i < placeholderLabels.Length)
            {
                unitCommandSlotNames[i].text = placeholderLabels[i];
                unitCommandSlotButtons[i].interactable = false; // 스킬 시스템 없어 동작 없는 칸 — 절대 안 채워짐
            }
            else
            {
                // 조합 결과가 들어올 칸. 채워지기 전까지는 보이지 않게 둔다.
                unitCommandSlotBackgrounds[i].color = Color.clear;
            }
        }
    }

    void BuildUnitCommandSlot(int index, Transform parent)
    {
        GameObject card = new GameObject($"UnitCommandSlot{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;
        background.color = UnitCommandDefaultColor;

        int capturedIndex = index;
        Button button = card.GetComponent<Button>();
        button.onClick.AddListener(() => OnUnitCommandSlotClicked(capturedIndex));

        EventTrigger trigger = card.AddComponent<EventTrigger>();
        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnUnitCommandSlotHoverEnter(capturedIndex));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnCombineCardHoverExit());

        Text nameText = CreateLabel(card.transform, "Name", "");
        nameText.raycastTarget = false;
        nameText.fontSize = 12;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;

        unitCommandSlotRoots[index] = card;
        unitCommandSlotBackgrounds[index] = background;
        unitCommandSlotNames[index] = nameText;
        unitCommandSlotButtons[index] = button;
    }

    void OnUnitCommandSlotClicked(int index)
    {
        if (currentShop != null)
        {
            OnSupportSkillSlotClicked(index);
            return;
        }

        if (index < 0 || index >= unitCommandRecipes.Length) return;

        CombineRecipe recipe = unitCommandRecipes[index];
        if (recipe == null) return;

        CombineSystem system = CombineSystemRef;
        if (system == null || !system.CanCombineNow(recipe)) return; // 흐린 상태면 눌러도 아무 일 없음

        if (system.TryCombine(recipe))
        {
            // 인벤토리가 바뀌어 흐림 상태가 달라졌을 것 — 다음 정기 갱신까지 안 기다리고 바로 반영.
            RefreshUnitCommandAffordability();
            nextUnitCommandDimRefreshTime = Time.time + RecipeRefreshInterval;
            HideCombineTooltip();
        }
    }

    // 스킬 칸 클릭. 마나포션(ManaRestore)은 위치·대상이 필요 없어 그 자리에서 바로 시전하고,
    // 나머지는 커서로 지점/유닛을 찍을 때까지 기다린다(RefreshSupportSkillTargeting).
    void OnSupportSkillSlotClicked(int index)
    {
        if (index < 0 || index >= unitCommandSkills.Length) return;

        SupportSkillData skill = unitCommandSkills[index];
        if (skill == null || currentShop == null || !currentShop.CanCast(skill)) return;

        if (skill.effect == SupportSkillEffect.ManaRestore)
        {
            currentShop.TryCastSelf(skill);
            RefreshSupportSkillAffordability();
            return;
        }

        pendingSkill = skill;
        targetingStartFrame = Time.frameCount;
    }

    // 스킬을 고른 뒤 다음 클릭을 기다린다. 우클릭이면 취소. uGUI 위 클릭(다른 버튼 등)은
    // SelectionManager와 같은 이유로 무시한다 — 스킬 버튼 클릭 그 자체가 targetingStartFrame
    // 이전 프레임이라 같은 클릭이 대상 지정으로 다시 잡히는 일은 없다.
    void RefreshSupportSkillTargeting()
    {
        if (pendingSkill == null || currentShop == null) return;
        if (Mouse.current == null) { pendingSkill = null; return; }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            pendingSkill = null;
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.frameCount <= targetingStartFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        SupportSkillData skill = pendingSkill;
        SupportShop shop = currentShop;
        pendingSkill = null;

        if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return;

        if (skill.targetKind == SupportSkillTargetKind.Unit)
        {
            if (hit.collider.TryGetComponent(out Selectable selectable))
                shop.TryCastOnUnit(skill, selectable.gameObject);
        }
        else
        {
            shop.TryCastOnGround(skill, hit.point);
        }

        RefreshSupportSkillAffordability();
    }

    void OnUnitCommandSlotHoverEnter(int index)
    {
        hoveredCommandSlotIndex = index;
        nextTooltipRefreshTime = Time.time + TooltipRefreshInterval;
        ShowHoveredTooltipNow(index);
    }

    // 선택이 바뀔 때만(매 프레임·주기 아님) GetRecipesStartingWith를 부른다.
    // 재료 충족 여부(CanCombineNow)만 0.4초 주기로 다시 확인해서 흐림 상태만 갱신한다.
    void RefreshUnitCommandCards()
    {
        SelectionManager selection = Selection;
        int count = selection != null ? selection.Selected.Count : 0;

        SupportShop shop = null;
        UnitData selectedData = null;

        if (count == 1)
        {
            Selectable single = selection.Selected[0];
            if (single != null)
            {
                if (single.TryGetComponent(out SupportShop shopComponent))
                    shop = shopComponent;
                else if (single.TryGetComponent(out UnitIdentity identity))
                    selectedData = identity.Data;
            }
        }

        // 도움소가 선택된 동안은 조합 카드 로직(조합 레시피 캐시·흐림 처리)을 아예 건드리지 않는다 —
        // 12칸의 내용·클릭 의미만 스킬로 바뀐다.
        if (shop != currentShop)
        {
            currentShop = shop;
            pendingSkill = null;
            RebuildSupportSkillSlots(shop);

            if (shop == null)
            {
                // 도움소 모드에서 빠져나온 직후 — 직전과 같은 유닛을 다시 선택해도
                // 조합 카드 쪽이 강제로 다시 그려지도록 캐시를 무효화한다.
                lastCommandUnitData = null;
                unitCommandSlotCount = 0;
            }
        }

        if (shop != null)
        {
            if (Time.time >= nextUnitCommandDimRefreshTime)
            {
                nextUnitCommandDimRefreshTime = Time.time + RecipeRefreshInterval;
                RefreshSupportSkillAffordability();
            }
            return;
        }

        if (selectedData != lastCommandUnitData)
        {
            lastCommandUnitData = selectedData;
            RebuildUnitCommandSlots(selectedData);
        }

        if (unitCommandSlotCount == 0) return;
        if (Time.time < nextUnitCommandDimRefreshTime) return;

        nextUnitCommandDimRefreshTime = Time.time + RecipeRefreshInterval;
        RefreshUnitCommandAffordability();
    }

    // 스킬도 조합 결과와 같은 9칸 순서(UnitCommandResultSlotOrder)를 그대로 재사용한다 —
    // 0~2번(공격/정지/모으기)은 도움소에서 의미가 없지만 굳이 숨기지 않고 그대로 둔다.
    void RebuildSupportSkillSlots(SupportShop shop)
    {
        for (int i = 0; i < UnitCommandResultSlotOrder.Length; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            unitCommandSkills[slot] = null;
            unitCommandSlotNames[slot].text = "";
            unitCommandSlotBackgrounds[slot].color = Color.clear;
        }

        if (shop == null) return;

        IReadOnlyList<SupportSkillData> skills = shop.Skills;
        int shown = Mathf.Min(skills.Count, UnitCommandResultSlotOrder.Length);

        for (int i = 0; i < shown; i++)
        {
            SupportSkillData skill = skills[i];
            if (skill == null) continue;

            int slot = UnitCommandResultSlotOrder[i];
            unitCommandSkills[slot] = skill;
            unitCommandSlotNames[slot].text = skill.skillName;
            unitCommandSlotBackgrounds[slot].color = SupportSkillColor;
        }

        RefreshSupportSkillAffordability();
    }

    // 마나/골드/쿨다운 중 하나라도 부족하면 알파만 낮춘다 — 조합 카드의 흐림 처리와 같은 패턴.
    void RefreshSupportSkillAffordability()
    {
        if (currentShop == null) return;

        for (int i = 0; i < UnitCommandResultSlotOrder.Length; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            SupportSkillData skill = unitCommandSkills[slot];
            if (skill == null) continue;

            bool canCast = currentShop.CanCast(skill);
            Color color = SupportSkillColor;
            color.a = canCast ? color.a : 0.35f;
            unitCommandSlotBackgrounds[slot].color = color;
        }
    }

    void RebuildUnitCommandSlots(UnitData selectedData)
    {
        for (int i = 0; i < unitCommandSlotCount; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            unitCommandRecipes[slot] = null;
            unitCommandSlotNames[slot].text = "";
            // 빈 칸은 투명하게 둔다. GridLayoutGroup은 비활성 자식을 건너뛰기 때문에
            // SetActive(false)로 숨기면 뒤 칸이 앞으로 당겨져 슬롯 번호와 실제 자리가 어긋난다.
            unitCommandSlotBackgrounds[slot].color = Color.clear;
        }

        unitCommandSlotCount = 0;

        if (selectedData == null) return;

        CombineSystem system = CombineSystemRef;
        if (system == null) return;

        // 반환 버퍼는 재사용된다 — 즉시 소비만 하고 보관하지 않는다.
        List<CombineRecipe> startingWith = system.GetRecipesStartingWith(selectedData);
        int shown = Mathf.Min(startingWith.Count, UnitCommandResultSlotOrder.Length);

        for (int i = 0; i < shown; i++)
        {
            CombineRecipe recipe = startingWith[i];
            if (recipe == null || recipe.result == null) continue;

            int slot = UnitCommandResultSlotOrder[i];
            unitCommandRecipes[slot] = recipe;
            unitCommandSlotNames[slot].text = recipe.result.unitName;
        }

        unitCommandSlotCount = shown;
        RefreshUnitCommandAffordability();
    }

    // 재료가 부족하면 등급 색은 유지한 채 알파만 낮춘다 — 회색으로 칠하면 무슨 등급이 될지 안 보인다.
    void RefreshUnitCommandAffordability()
    {
        CombineSystem system = CombineSystemRef;

        for (int i = 0; i < unitCommandSlotCount; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            CombineRecipe recipe = unitCommandRecipes[slot];
            if (recipe == null || recipe.result == null) continue;

            bool canCombine = system != null && system.CanCombineNow(recipe);
            Color color = GetGradeColor(recipe.result.grade);
            color.a = canCombine ? color.a : 0.4f;
            unitCommandSlotBackgrounds[slot].color = color;
        }
    }

    void RefreshSelectionPanel()
    {
        if (unitInfoText == null || unitCardsPanel == null) return;

        SelectionManager selection = Selection;
        int count = selection != null ? selection.Selected.Count : 0;

        if (count <= 1)
            ShowSingleInfo(selection, count);
        else
            ShowCardGrid(selection);
    }

    void ShowSingleInfo(SelectionManager selection, int count)
    {
        unitCardsPanel.SetActive(false);
        unitInfoText.gameObject.SetActive(true);

        if (count == 0)
        {
            unitInfoText.text = "선택된 유닛 없음";
            return;
        }

        Selectable first = selection.Selected[0];
        if (first == null)
        {
            unitInfoText.text = "선택된 유닛 없음";
            return;
        }

        first.TryGetComponent(out UnitIdentity identity);
        first.TryGetComponent(out UnitAttacker attacker);

        string unitName = identity != null && identity.Data != null ? identity.Data.unitName : first.name;
        string grade = identity != null && identity.Data != null ? identity.Data.grade.KoreanName() : "-";
        // 플레이어 유닛에 아직 별도 체력 컴포넌트가 없어, UnitData의 기준 hp를 표시한다(실시간 값 아님).
        string hp = identity != null && identity.Data != null ? identity.Data.hp.ToString("F0") : "-";
        string attackPower = attacker != null ? attacker.AttackDamage.ToString("F0") : "-";
        string attackRange = attacker != null ? attacker.AttackRange.ToString("F1") : "-";
        string attackSpeed = attacker != null && attacker.AttackInterval > 0f
            ? (1f / attacker.AttackInterval).ToString("F2")
            : "-";

        unitInfoText.text =
            $"{unitName}\n등급: {grade}\n체력: {hp}\n공격력: {attackPower}\n사거리: {attackRange}\n공격속도: {attackSpeed}/s";
    }

    // 다중 선택. 카드 자체는 BuildSelectionCards에서 미리 만들어뒀고, 여기서는 내용과
    // 활성 상태만 바꾼다. 이전 프레임과 같은 대상 구성이면 아무것도 다시 안 그린다.
    void ShowCardGrid(SelectionManager selection)
    {
        unitInfoText.gameObject.SetActive(false);
        unitCardsPanel.SetActive(true);

        IReadOnlyList<Selectable> selected = selection.Selected;
        int shownCount = Mathf.Min(selected.Count, MaxSelectionCards);
        int overflow = Mathf.Max(0, selected.Count - MaxSelectionCards);

        if (!CardSelectionChanged(selected, shownCount, overflow)) return;

        lastCardShownCount = shownCount;
        lastOverflow = overflow;

        for (int i = 0; i < MaxSelectionCards; i++)
        {
            Selectable target = i < shownCount ? selected[i] : null;
            lastCardTargets[i] = target;

            if (target == null)
            {
                cardRoots[i].SetActive(false);
                continue;
            }

            cardRoots[i].SetActive(true);

            target.TryGetComponent(out UnitIdentity identity);
            UnitData data = identity != null ? identity.Data : null;

            cardBackgrounds[i].color = data != null ? GetGradeColor(data.grade) : UnidentifiedCardColor;
            cardNames[i].text = data != null ? data.unitName : target.name;

            bool showOverflow = overflow > 0 && i == MaxSelectionCards - 1;
            cardOverflowTexts[i].text = showOverflow ? $"+{overflow}" : "";
            cardOverflowTexts[i].gameObject.SetActive(showOverflow);
        }
    }

    bool CardSelectionChanged(IReadOnlyList<Selectable> selected, int shownCount, int overflow)
    {
        if (shownCount != lastCardShownCount || overflow != lastOverflow) return true;

        for (int i = 0; i < shownCount; i++)
            if (selected[i] != lastCardTargets[i])
                return true;

        return false;
    }

    void RefreshTopBar()
    {
        if (goldWoodText == null || roundTimeText == null) return;

        PlayerContext local = PlayerContext.Local;
        int gold = local != null && local.GoldWallet != null ? local.GoldWallet.Gold : 0;
        int wood = local != null && local.ResourceWallet != null ? local.ResourceWallet.Get(ResourceType.Wood) : 0;

        if (gold != lastGold || wood != lastWood)
        {
            lastGold = gold;
            lastWood = wood;
            goldWoodText.text = $"골드 {gold}   목재 {wood}";
        }

        RoundManager rm = RoundManagerRef;
        int round = rm != null ? rm.CurrentRound : 0;
        int timeTenths = rm != null ? Mathf.RoundToInt(rm.RoundTimeLeft * 10f) : 0;

        if (round != lastRound || timeTenths != lastTimeTenths)
        {
            lastRound = round;
            lastTimeTenths = timeTenths;
            roundTimeText.text = rm != null
                ? $"라운드 {round}   남은시간 {timeTenths / 10f:F1}s"
                : "라운드 -   남은시간 -";
        }
    }

    // 팀 현황판 값은 자주 안 바뀌므로(적/골드/목재), 이전 프레임과 비교해 실제로 바뀐 경우에만
    // StringBuilder를 다시 채운다.
    void RefreshTeamPanel()
    {
        if (teamPanelText == null) return;

        RoundManager rm = RoundManagerRef;
        int totalEnemies = EnemyDummy.Active.Count;
        int deathCount = rm != null ? rm.DeathCount : 0;

        bool changed = !teamPanelInitialized || totalEnemies != lastTotalEnemyCount || deathCount != lastDeathCount;

        for (int i = 0; i < TeamSlotCount; i++)
        {
            PlayerContext context = PlayerContext.GetOccupied(i);
            slotHas[i] = context != null;
            slotEnemy[i] = context != null ? EnemyDummy.CountInLane(context.PlayerId) : 0;
            slotGold[i] = context != null && context.GoldWallet != null ? context.GoldWallet.Gold : 0;
            slotWood[i] = context != null && context.ResourceWallet != null ? context.ResourceWallet.Get(ResourceType.Wood) : 0;

            if (slotHas[i] != lastSlotHasContext[i]
                || slotEnemy[i] != lastSlotEnemyCount[i]
                || slotGold[i] != lastSlotGold[i]
                || slotWood[i] != lastSlotWood[i])
            {
                changed = true;
            }
        }

        if (!changed) return;

        teamPanelInitialized = true;
        lastTotalEnemyCount = totalEnemies;
        lastDeathCount = deathCount;

        teamPanelBuilder.Clear();
        teamPanelBuilder.Append("유닛 카운트 ").Append(totalEnemies).Append(" / 데스카운트 ").Append(deathCount);

        for (int i = 0; i < TeamSlotCount; i++)
        {
            lastSlotHasContext[i] = slotHas[i];
            lastSlotEnemyCount[i] = slotEnemy[i];
            lastSlotGold[i] = slotGold[i];
            lastSlotWood[i] = slotWood[i];

            teamPanelBuilder.Append('\n');

            bool isLocal = slotHas[i] && i == LocalPlayer.LocalPlayerId;
            if (isLocal) teamPanelBuilder.Append("<b><color=#FFD54A>");

            teamPanelBuilder.Append("플레이어 ").Append(i + 1);
            if (slotHas[i])
            {
                teamPanelBuilder.Append(" | 적 ").Append(slotEnemy[i])
                    .Append(" | 골드 ").Append(slotGold[i])
                    .Append(" | 목재 ").Append(slotWood[i]);
            }
            else
            {
                teamPanelBuilder.Append(" | 비어있음");
            }

            if (isLocal) teamPanelBuilder.Append("</color></b>");
        }

        teamPanelText.text = teamPanelBuilder.ToString();
    }

    // StoryManager.Instance는 씬에 없을 수 있고(그러면 줄을 숨긴다) 나중에 생길 수도 있어 매번 다시 읽는다.
    // SecondsUntilNext는 매 프레임 바뀌는 float라, 초 단위(Mathf.CeilToInt)로 잘라 비교해야
    // 실제로 표시되는 숫자가 바뀔 때만 텍스트를 다시 만든다.
    void RefreshStoryPanel()
    {
        if (storyText == null) return;

        StoryManager story = StoryManager.Instance;

        bool hasStory = story != null;
        bool running = hasStory && story.Running != null;
        bool waiting = hasStory && !running && story.IsWaiting;
        string label = hasStory ? story.StatusLabel : null;
        int seconds = waiting ? Mathf.CeilToInt(story.SecondsUntilNext) : 0;
        bool visible = running || waiting;

        bool changed = visible != lastStoryVisible
            || running != lastStoryRunning
            || label != lastStoryLabel
            || (waiting && seconds != lastStorySeconds);

        if (!changed) return;

        lastStoryVisible = visible;
        lastStoryRunning = running;
        lastStoryLabel = label;
        lastStorySeconds = seconds;

        if (storyText.gameObject.activeSelf != visible)
            storyText.gameObject.SetActive(visible);

        if (!visible) return;

        storyText.text = running
            ? $"스토리: {label}"
            : $"{label} {seconds / 60:00}:{seconds % 60:00}";
    }

    // PlayerContext.Local이 없으면(씬에 없거나 로컬 ID 불일치) 패널 자체를 숨긴다.
    // UnitInventory가 바뀌면(OnInventoryChanged) dirty만 세우고, 실제 재구성은 여기서 한 번에 한다 —
    // 로컬 인벤토리 참조가 바뀔 수도 있어(씬 전환 등) 매 프레임 같은 인스턴스인지 확인해 구독을 갱신한다.
    void RefreshInventoryPanel()
    {
        if (inventoryText == null) return;

        PlayerContext local = PlayerContext.Local;
        UnitInventory inventory = local != null ? local.UnitInventory : null;

        if (inventory != subscribedInventory)
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.OnInventoryChanged -= OnLocalInventoryChanged;
            }

            subscribedInventory = inventory;

            if (subscribedInventory != null)
            {
                subscribedInventory.OnInventoryChanged += OnLocalInventoryChanged;
            }

            inventoryDirty = true;
        }

        bool visible = inventory != null;
        if (inventoryPanelObject.activeSelf != visible)
        {
            inventoryPanelObject.SetActive(visible);
        }

        if (!visible || !inventoryDirty) return;

        inventoryDirty = false;
        RebuildInventoryText(inventory);
    }

    void OnLocalInventoryChanged()
    {
        inventoryDirty = true;
    }

    // 등급별 색 점(■) + "이름 xN" 목록. 등급(Tier) 오름차순 → 이름순 정렬.
    // 스크롤 없이 상위 MaxInventoryEntries개만 보여주고 나머지는 "외 N종"으로 뭉갠다.
    void RebuildInventoryText(UnitInventory inventory)
    {
        inventoryCounts.Clear();

        foreach (UnitData unit in inventory.Units)
        {
            if (unit == null) continue;

            inventoryCounts.TryGetValue(unit, out int count);
            inventoryCounts[unit] = count + 1;
        }

        inventoryKeys.Clear();
        foreach (UnitData unit in inventoryCounts.Keys)
        {
            inventoryKeys.Add(unit);
        }

        inventoryKeys.Sort(CompareUnitByGradeThenName);

        inventoryBuilder.Clear();

        int shown = Mathf.Min(inventoryKeys.Count, MaxInventoryEntries);
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) inventoryBuilder.Append('\n');

            UnitData unit = inventoryKeys[i];
            string colorHex = ColorUtility.ToHtmlStringRGB(GetGradeColor(unit.grade));

            inventoryBuilder.Append("<color=#").Append(colorHex).Append(">■</color> ")
                .Append(unit.unitName).Append(" x").Append(inventoryCounts[unit]);
        }

        int remaining = inventoryKeys.Count - shown;
        if (remaining > 0)
        {
            if (shown > 0) inventoryBuilder.Append('\n');
            inventoryBuilder.Append("외 ").Append(remaining).Append("종");
        }

        inventoryText.text = inventoryBuilder.ToString();
    }

    static int CompareUnitByGradeThenName(UnitData a, UnitData b)
    {
        int tierCompare = a.grade.Tier().CompareTo(b.grade.Tier());
        return tierCompare != 0 ? tierCompare : string.CompareOrdinal(a.unitName, b.unitName);
    }

    static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        return obj.GetComponent<RectTransform>();
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
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
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
