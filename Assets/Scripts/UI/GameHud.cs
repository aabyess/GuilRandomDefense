using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
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

    readonly GameObject[] combineCardRoots = new GameObject[CommandSlotCount];
    readonly Image[] combineCardBackgrounds = new Image[CommandSlotCount];
    readonly Text[] combineCardNames = new Text[CommandSlotCount];
    readonly Text[] combineCardOverflowTexts = new Text[CommandSlotCount];
    readonly CombineRecipe[] combineCardRecipes = new CombineRecipe[CommandSlotCount];
    readonly List<CombineRecipe> lastAvailableRecipes = new List<CombineRecipe>();
    float nextRecipeRefreshTime;

    // 조합 카드 위에 뜨는 조합식 툴팁 — 카드마다 만들지 않고 하나를 공유해서 위치·내용만 바꾼다.
    readonly StringBuilder tooltipBuilder = new StringBuilder(256);
    GameObject combineTooltipObject;
    Text combineTooltipText;

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
        RefreshCombineCards();
        RefreshInventoryPanel();
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

        RectTransform combineCardPanel = CreatePanel(bar, "CombineCardPanel", new Color(1f, 1f, 1f, 0.05f));
        SetAnchors(combineCardPanel, new Vector2(0.16f, 0.05f), new Vector2(0.42f, 0.95f));
        BuildCombineCardGrid(combineCardPanel);

        RectTransform infoPanel = CreatePanel(bar, "UnitInfoPanel", new Color(1f, 1f, 1f, 0.05f));
        SetAnchors(infoPanel, new Vector2(0.43f, 0.05f), new Vector2(0.65f, 0.95f));
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

    // 조합 카드 = 지금 조합 가능한 레시피. 12칸을 미리 만들어두고(BuildCombineCard) 이후로는
    // RefreshCombineCards가 내용·활성 상태만 바꾼다 — 유닛 카드와 같은 패턴.
    void BuildCombineCardGrid(RectTransform parent)
    {
        GridLayoutGroup grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(70f, 70f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CommandColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < CommandSlotCount; i++)
            BuildCombineCard(i, parent);
    }

    void BuildCombineCard(int index, Transform parent)
    {
        GameObject card = new GameObject($"CombineSlot{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(parent, false);

        Image background = card.GetComponent<Image>();
        background.raycastTarget = true;
        background.color = new Color(1f, 1f, 1f, 0.12f);

        int capturedIndex = index;
        card.GetComponent<Button>().onClick.AddListener(() => OnCombineCardClicked(capturedIndex));

        EventTrigger trigger = card.AddComponent<EventTrigger>();
        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnCombineCardHoverEnter(capturedIndex));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnCombineCardHoverExit());

        Text nameText = CreateLabel(card.transform, "Name", "");
        nameText.raycastTarget = false;
        nameText.fontSize = 11;
        nameText.horizontalOverflow = HorizontalWrapMode.Wrap;

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

        // 조합 가능한 게 없다고 가정하고 시작 — 첫 RefreshCombineCards가 실제 내용으로 채운다.
        card.SetActive(false);

        combineCardRoots[index] = card;
        combineCardBackgrounds[index] = background;
        combineCardNames[index] = nameText;
        combineCardOverflowTexts[index] = overflowText;
    }

    static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    void OnCombineCardClicked(int index)
    {
        if (index < 0 || index >= combineCardRecipes.Length) return;

        CombineRecipe recipe = combineCardRecipes[index];
        if (recipe == null) return;

        CombineSystem system = CombineSystemRef;
        if (system == null) return;

        if (system.TryCombine(recipe))
        {
            // 인벤토리가 바뀌어 목록이 달라졌을 것 — 다음 정기 갱신(최대 0.4초)까지 기다리지 않고 즉시 반영.
            nextRecipeRefreshTime = 0f;
            RefreshCombineCards();
            HideCombineTooltip(); // 방금 조합에 쓴 재료 기준 조합식이라 그대로 두면 정보가 낡는다.
        }
    }

    void OnCombineCardHoverEnter(int index)
    {
        ShowCombineTooltip(index);
    }

    void OnCombineCardHoverExit()
    {
        HideCombineTooltip();
    }

    // 0.3~0.5초에 한 번만 CombineSystem.GetAvailableRecipes()를 부른다 — 레시피 199개를
    // 매 프레임 전부 검사하면 안 된다. 목록이 이전과 같으면(개수·순서 동일) 카드도 안 다시 그린다.
    void RefreshCombineCards()
    {
        if (Time.time < nextRecipeRefreshTime) return;
        nextRecipeRefreshTime = Time.time + RecipeRefreshInterval;

        CombineSystem system = CombineSystemRef;
        List<CombineRecipe> available = system != null ? system.GetAvailableRecipes() : EmptyRecipes;

        if (!AvailableRecipesChanged(available)) return;

        lastAvailableRecipes.Clear();
        lastAvailableRecipes.AddRange(available);

        int shownCount = Mathf.Min(available.Count, CommandSlotCount);
        int overflow = Mathf.Max(0, available.Count - CommandSlotCount);

        for (int i = 0; i < CommandSlotCount; i++)
        {
            CombineRecipe recipe = i < shownCount ? available[i] : null;

            if (recipe != null && recipe.result == null)
            {
                Debug.LogWarning($"GameHud: {recipe.commandId} 레시피의 result가 비어있어 카드를 건너뜁니다.");
                recipe = null;
            }

            combineCardRecipes[i] = recipe;

            if (recipe == null)
            {
                combineCardRoots[i].SetActive(false);
                continue;
            }

            combineCardRoots[i].SetActive(true);
            combineCardBackgrounds[i].color = GetGradeColor(recipe.result.grade);
            combineCardNames[i].text = recipe.result.unitName;

            bool showOverflow = overflow > 0 && i == CommandSlotCount - 1;
            combineCardOverflowTexts[i].text = showOverflow ? $"+{overflow}" : "";
            combineCardOverflowTexts[i].gameObject.SetActive(showOverflow);
        }
    }

    bool AvailableRecipesChanged(List<CombineRecipe> available)
    {
        if (available.Count != lastAvailableRecipes.Count) return true;

        for (int i = 0; i < available.Count; i++)
            if (available[i] != lastAvailableRecipes[i])
                return true;

        return false;
    }

    // 하단 바 안에 그리면 그 좁은 영역 안에서 잘리므로, 카드 위쪽에 절대 좌표로 띄운다.
    // ScreenSpaceOverlay 캔버스라 RectTransform.position이 곧 화면 픽셀 좌표라 이렇게 계산할 수 있다.
    void ShowCombineTooltip(int index)
    {
        if (combineTooltipObject == null) return;
        if (index < 0 || index >= combineCardRecipes.Length) return;

        CombineRecipe recipe = combineCardRecipes[index];
        if (recipe == null) return;

        combineTooltipText.text = BuildRecipeTooltipText(recipe);

        RectTransform cardRect = (RectTransform)combineCardRoots[index].transform;
        RectTransform tooltipRect = (RectTransform)combineTooltipObject.transform;

        float halfHeight = cardRect.rect.height * cardRect.lossyScale.y * 0.5f;
        tooltipRect.position = cardRect.position + new Vector3(0f, halfHeight + 12f, 0f);

        combineTooltipObject.SetActive(true);
    }

    void HideCombineTooltip()
    {
        if (combineTooltipObject != null) combineTooltipObject.SetActive(false);
    }

    // 마우스가 카드 위에 올라간 순간에만 호출된다(호버 이벤트) — 매 프레임 만들지 않는다.
    string BuildRecipeTooltipText(CombineRecipe recipe)
    {
        tooltipBuilder.Clear();

        string resultName = recipe.result != null ? recipe.result.unitName : "?";
        tooltipBuilder.Append(resultName).Append(" : ");

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

        if (recipe.goldCost > 0)
        {
            if (!first) tooltipBuilder.Append(" + ");
            tooltipBuilder.Append("골드 ").Append(recipe.goldCost);
            first = false;
        }

        if (recipe.resourceCosts != null)
        {
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
            {
                if (cost == null || cost.amount <= 0) continue;

                if (!first) tooltipBuilder.Append(" + ");
                tooltipBuilder.Append(ResourceLabel(cost.type)).Append(' ').Append(cost.amount);
                first = false;
            }
        }

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

    // 유닛 명령 자리(공격/정지/모으기/스킬). 스킬 시스템이 아직 없어 지금은 동작 없는 칸이다 —
    // 첫 세 칸에 이름만 넣어서 용도를 보여준다.
    static void BuildUnitCommandGrid(RectTransform parent)
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
            GameObject slot = new GameObject($"UnitCommandSlot{i}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(parent, false);
            slot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            if (i < placeholderLabels.Length)
            {
                Text label = CreateLabel(slot.transform, "Label", placeholderLabels[i]);
                label.fontSize = 16;
            }
            // 나머지 칸(스킬 등)은 동작 없음 — 스킬 데이터가 아직 없어 껍데기만 배치한다.
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
