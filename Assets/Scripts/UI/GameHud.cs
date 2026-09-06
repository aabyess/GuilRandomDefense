using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 화면 하단 상시 HUD (원랜디/워크래프트3 스타일). 좌: 미니맵 자리, 중앙: 선택 유닛 정보, 우: 명령 카드 그리드 자리.
// 프리팹 루트에 이 스크립트 하나만 붙여두면 Awake에서 전체 uGUI 계층을 스스로 구성한다
// (에디터 없이 만든 프리팹이 손으로 짠 UI 계층 때문에 깨지는 걸 피하기 위한 구조).
public class GameHud : MonoBehaviour
{
    // 12칸(공격/정지/모으기 + 조합·상점 9칸) 뒤에 정렬(C) 한 칸을 더 붙여 13칸.
    // 조합·상점 9칸(UnitCommandResultSlotOrder)은 도박소가 정확히 9칸을 쓰므로 하나도 못 줄인다.
    const int CommandSlotCount = 13;
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
    readonly bool[] slotDead = new bool[TeamSlotCount];

    RoundManager roundManager;
    CombineSystem combineSystem;
    UnitSpawner unitSpawner;

    // 특성강화(06번) 버튼 — 단일 선택 + UnitData.trait가 있을 때만 뜬다. 원작은 상점이 아니라
    // "그 유닛을 선택한 채로 버튼 하나"라(Trig_T_Ability_hero_Conditions), 명령 카드 그리드
    // (조합·상점 전용, 이미 13칸 다 참)와는 별개 자리에 둔다.
    GameObject traitButtonPanel;
    Text traitButtonText;
    Button traitButtonComponent;
    UnitTraitData lastTraitButtonTrait;
    bool lastTraitButtonUnlocked;
    int lastTraitButtonPoints = int.MinValue;
    int lastTraitButtonRepeatCount = int.MinValue;

    // 05번 「고대의 배」도박 능력 버튼들(2026-09-06 목록화) — 특성강화 버튼과 같은 이유로
    // 같은 열(선택 시 뜨는 전용 버튼)에 둔다. UnitData.gambleOptions 항목 수만큼 뜬다.
    // 슬롯 3개를 미리 만들어두고 항목 수만큼만 켠다 — 지금 알려진 목록 최대 크기(h05Y의
    // A023·A0OD·A0OC)에 맞춘 것이지 "나중에 늘 것 같아서" 여유를 둔 게 아니다
    // (RewardDistributor.startingSpecialUnit과 같은 원칙).
    const int GambleButtonSlotCount = 3;
    readonly GameObject[] gambleButtonPanels = new GameObject[GambleButtonSlotCount];
    readonly Text[] gambleButtonTexts = new Text[GambleButtonSlotCount];
    readonly Button[] gambleButtonComponents = new Button[GambleButtonSlotCount];
    readonly int[] lastGambleButtonWood = new int[GambleButtonSlotCount];

    // "유닛 판매" 버튼(2026-09-06, PM 지시) — 같은 이유로 같은 열, 고대의 배 버튼 바로
    // 아래. UnitData.sellRewardWisp/sellRewardTraitPoints 둘 다 비어있지 않을 때만 뜬다
    // (트레잇·고대의배 버튼과 같은 관례 — "보상이 없으면 버튼 자체가 없다").
    GameObject sellButtonPanel;
    Text sellButtonText;
    Button sellButtonComponent;
    UnitData lastSellButtonUnit;

    // 항법(5택1, NAVIGATION_ROUTES_FULL.md) — 유닛 선택과 무관하게 항상 뜨는 진입점이라
    // (원작 H0C4가 게임 시작부터 즉시 배치, 라운드·퀘스트 게이트 없음) 위 세 버튼과 성격이
    // 다르다. 자리만 재사용한다 — 05번 버튼이 목록화되며 비게 된 y 0.84~0.89(고대의 배
    // 옛 단일 버튼 자리)에 넣는다.
    static readonly NavigationChoice[] NavigationOptionOrder =
    {
        NavigationChoice.Hegemon, NavigationChoice.Union, NavigationChoice.Gambler,
        NavigationChoice.SupportBoost, NavigationChoice.SupportLock,
    };

    static readonly string[] NavigationOptionNames =
    {
        "① 패왕의 길", "② 연합세력", "③ 도박광", "④ 도움소 강화", "⑤ 도움소 잠금",
    };

    // ⚠️ 확인된 효과만 적는다(지어내지 않는다) — ②는 효과 자체가 아직 안 돈다는 걸
    // 그대로 적는다. 수치는 실제 자산 값(SupportSkill_해루석/버스터콜.asset,
    // GamblingOptionData.failureLuckyTokens, ItemGambleState.ReducedPoolActive)과 일치시킨다.
    static readonly string[] NavigationOptionDescriptions =
    {
        "일반 몹 강화 코드 레벨 영구 +2 (배율 0.90배→1.00배)",
        "🔴 미구현 — e0IX 위습 매핑 대기. 지금 골라도 아무 효과 없음\n(원작: 포인트값 100 초과 유닛 로스터 편입 시 랜덤위습 +1)",
        "「다른세계 도박」 실패 시 행운의 토큰 +1 (1개→2개)",
        "해루석 피해 250만→300만·마나 700→600\n버스터콜 재사용 100초→66초·마나 500→333",
        "아이템 도박 확률 풀이 13종으로 축소",
    };

    GameObject navigationButtonPanel;
    Text navigationButtonText;
    bool navigationButtonTextInitialized;
    bool lastNavigationHasChosen;
    NavigationChoice lastNavigationChoice = NavigationChoice.None;

    GameObject navigationModalPanel;
    readonly Text[] navigationRowTexts = new Text[5];
    readonly Button[] navigationRowButtons = new Button[5];
    readonly Text[] navigationRowButtonLabels = new Text[5];

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

    // 유닛 명령 그리드(13칸). 0~2번은 공격/정지/모으기, 12번은 정렬(C) 고정 placeholder라 절대 안 건드림.
    // 나머지 칸(3~11) 중 맨 아랫줄부터 왼쪽→오른쪽, 넘치면 그 윗줄로 이어지는 순서로 "선택한 유닛이
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

    // 레인 상점(도움소/도박소/강화소 등, ILaneShop) 선택 시 같은 12칸을 상점 칸 표시로 돌려쓴다.
    // currentShop이 non-null이면 RefreshUnitCommandCards가 조합 로직 대신 이 경로로 빠진다.
    // GameHud는 어떤 상점인지 몰라도 된다 — Docs/design/LANE_SHOP.md 참고.
    readonly int[] shopLogicalSlotIndex = new int[CommandSlotCount];   // 시각 슬롯 → 상점 논리 인덱스, -1이면 빈 칸
    ILaneShop currentShop;
    ILaneShop pendingShop;           // 대상 지정 중 currentShop이 바뀌어도 원래 상점에 정확히 반영되도록
    int pendingSlotIndex = -1;       // 커서로 대상을 찍기를 기다리는 논리 슬롯 (-1이면 대기 아님)
    LaneShopTargetKind pendingTargetKind;
    int targetingStartFrame;        // 칸을 고른 바로 그 클릭이 대상 클릭으로 다시 잡히지 않게

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
    readonly int[] lastSlotEnemyCount = new int[TeamSlotCount];
    readonly int[] lastSlotGold = new int[TeamSlotCount];
    readonly int[] lastSlotWood = new int[TeamSlotCount];
    readonly bool[] lastSlotHasContext = new bool[TeamSlotCount];
    readonly bool[] lastSlotDead = new bool[TeamSlotCount];

    SelectionManager Selection => selectionManager != null
        ? selectionManager
        : selectionManager = FindFirstObjectByType<SelectionManager>();

    RoundManager RoundManagerRef => roundManager != null
        ? roundManager
        : roundManager = FindFirstObjectByType<RoundManager>();

    CombineSystem CombineSystemRef => combineSystem != null
        ? combineSystem
        : combineSystem = FindFirstObjectByType<CombineSystem>();

    // 06번③ 변신 실행(ExecuteTransform)이 쓴다 — CombineSystem.Spawner와 같은 지연 조회 관례.
    UnitSpawner Spawner => unitSpawner != null
        ? unitSpawner
        : unitSpawner = FindFirstObjectByType<UnitSpawner>();

    // h0BS(메타몽) 판매→아이템 도박 결과를 받는다. CombineSystem이 조합 재료로 참조하는
    // 그 인벤토리와 같은 싱글턴 인스턴스다(씬에 하나, CombineSystem.itemInventory와 같은
    // 자리 — PlayerContext엔 이 참조가 없다, 지금은 플레이어별이 아니라 전역 공유 상태).
    ItemInventory itemInventory;
    ItemInventory ItemInventoryRef => itemInventory != null
        ? itemInventory
        : itemInventory = FindFirstObjectByType<ItemInventory>();

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
        RefreshShopTargeting();
        RefreshHoveredTooltip();
        RefreshTraitButton();
        RefreshGambleButtons();
        RefreshSellButton();
        RefreshNavigationButton();
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
        // 미니맵 폭은 0.14 → 0.23으로 넓혔다(사장님 지시 2026-09-07: "미니맵이 가로로 너무 좁다").
        // 남는 폭은 오른쪽 명령 카드 그리드에서 뺐다 — 그쪽은 3열(90px + 간격 6px = 282px)만
        // 필요한데 634px을 쓰고 있어서 절반 이상이 빈 공간이었다.
        RectTransform minimapPanel = CreatePanel(bar, "MinimapPanel", new Color(1f, 1f, 1f, 0.08f));
        SetAnchors(minimapPanel, new Vector2(0.01f, 0.05f), new Vector2(0.24f, 0.95f));
        BuildMinimap(minimapPanel);

        RectTransform infoPanel = CreatePanel(bar, "UnitInfoPanel", new Color(1f, 1f, 1f, 0.05f));
        SetAnchors(infoPanel, new Vector2(0.25f, 0.05f), new Vector2(0.81f, 0.95f));
        unitInfoText = CreateLabel(infoPanel, "UnitInfoText", "선택된 유닛 없음");
        unitInfoText.alignment = TextAnchor.UpperLeft;
        unitInfoText.fontSize = 30;
        unitInfoText.lineSpacing = 1.15f;
        unitInfoText.horizontalOverflow = HorizontalWrapMode.Wrap;

        BuildSelectionCards(infoPanel);

        RectTransform commandPanel = CreatePanel(bar, "UnitCommandPanel", new Color(1f, 1f, 1f, 0.05f));
        // 0.33 → 0.17. 3열 그리드에 필요한 최소 폭은 282px(90×3 + 6×2)이고
        // 0.17 × 1920 = 326px이라 여유가 남는다. 열을 늘리면 이 값을 다시 봐야 한다.
        SetAnchors(commandPanel, new Vector2(0.82f, 0.05f), new Vector2(0.99f, 0.95f));
        BuildUnitCommandGrid(commandPanel);

        BuildTopBar();
        BuildStoryPanel();
        BuildTeamPanel();
        BuildTraitButton();
        BuildGambleButtons();
        BuildSellButton();
        BuildNavigationUI();
        // 왼쪽 유닛 목록은 만들지 않는다. 화면 절반을 덮는데, 무엇을 들고 있는지는
        // 아래 명령 카드 그리드가 이미 보여준다. (F1 디버그 HUD에도 같은 목록이 있다.)

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

    // StoryPanel(0.01~0.5)과 TeamPanel(0.71~0.99) 사이, 같은 높이띠에 낀다 — 기존 앵커를
    // 하나도 안 건드리고 빈 자리에 끼워 넣는 자리다.
    void BuildTraitButton()
    {
        RectTransform panel = CreatePanel(transform, "TraitButtonPanel", new Color(1f, 1f, 1f, 0.15f));
        SetAnchors(panel, new Vector2(0.51f, 0.90f), new Vector2(0.70f, 0.95f));

        Button button = panel.gameObject.AddComponent<Button>();
        button.onClick.AddListener(OnTraitButtonClicked);

        traitButtonText = CreateLabel(panel, "TraitButtonText", "");
        traitButtonText.fontSize = 16;
        traitButtonText.raycastTarget = false;

        traitButtonPanel = panel.gameObject;
        traitButtonComponent = button;
        traitButtonPanel.SetActive(false);
    }

    // 단일 선택 + UnitData.trait가 있을 때만 보인다. 06번 26분기 외 213종은 trait가 null이라
    // 버튼 자체가 안 뜬다 — "능력이 없는 유닛"과 "특성강화가 아직 없는 유닛"을 구분하지 않는다
    // (원작도 능력강화가 없는 유닛은 그 조건 함수 자체가 없다).
    void RefreshTraitButton()
    {
        if (traitButtonPanel == null) return;

        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) { HideTraitButton(); return; }

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null)
        { HideTraitButton(); return; }

        UnitTraitData trait = identity.Data.trait;
        if (trait == null) { HideTraitButton(); return; }

        // 소유자가 없는 유닛(중립·디버그)은 특성포인트를 낼 플레이어가 없다 — 버튼을 안 보인다.
        if (!single.TryGetComponent(out OwnedByPlayer owner)) { HideTraitButton(); return; }

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        UnitUpgrades upgrades = context != null ? context.UnitUpgrades : null;
        if (upgrades == null) { HideTraitButton(); return; }

        bool unlocked = upgrades.IsUnlocked(trait);
        int points = upgrades.TraitPoints;
        int repeatCount = trait.isRepeatablePurchase ? upgrades.RepeatablePurchaseCount(trait) : 0;

        traitButtonPanel.SetActive(true);

        // 다른 Refresh들과 같은 관례 — 값이 안 바뀌었으면 텍스트를 다시 안 만든다.
        if (trait == lastTraitButtonTrait && unlocked == lastTraitButtonUnlocked &&
            points == lastTraitButtonPoints && repeatCount == lastTraitButtonRepeatCount)
            return;

        lastTraitButtonTrait = trait;
        lastTraitButtonUnlocked = unlocked;
        lastTraitButtonPoints = points;
        lastTraitButtonRepeatCount = repeatCount;

        // 06번⑤(반복구매형, 아카이누) — 언락 뒤에도 버튼이 안 잠긴다(원작: 몇 번이든
        // 다시 살 수 있다). 다른 25개(아래 unlocked 분기)와 갈리는 지점이 정확히 여기다.
        if (unlocked && trait.isRepeatablePurchase)
        {
            traitButtonText.text = $"{trait.traitName}\n{repeatCount}회 구매됨 — 추가 구매({trait.costTraitPoints}pt, 보유 {points}pt)";
            traitButtonComponent.interactable = points >= trait.costTraitPoints;
        }
        else if (unlocked)
        {
            traitButtonText.text = $"{trait.traitName}\n습득 완료";
            traitButtonComponent.interactable = false;
        }
        // ⚠️ 06번③(변신) — 메커니즘(ExecuteTransform)은 있지만 목적지(transformIntoUnit)가
        // 아직 사장님 배정 전이라 null이다. 예전엔 "메커니즘이 없어서" 막혀 있었지만, 이제는
        // "대상이 없어서" 막힌 것이다(PM 지시 2026-09-05) — transformIntoUnit이 채워지는
        // 순간 이 else if 없이 바로 아래 else(구매) 분기로 자연히 넘어간다.
        else if (trait.isTransformType && trait.transformIntoUnit == null)
        {
            traitButtonText.text = $"{trait.traitName}\n변신 대상 미정";
            traitButtonComponent.interactable = false;
        }
        else
        {
            traitButtonText.text = $"특성강화\n({trait.costTraitPoints}pt, 보유 {points}pt)";
            traitButtonComponent.interactable = points >= trait.costTraitPoints;
        }
    }

    void HideTraitButton()
    {
        if (traitButtonPanel != null && traitButtonPanel.activeSelf) traitButtonPanel.SetActive(false);
        lastTraitButtonTrait = null;
        lastTraitButtonPoints = int.MinValue;
    }

    // TraitButtonPanel(0.51~0.70, 0.90~0.95) 위쪽 열 바로 아래에서 시작해 슬롯마다 한 칸씩
    // 내려간다 — 같은 이유(단일 선택 시 뜨는 전용 버튼)라 같은 열에 쌓는다.
    void BuildGambleButtons()
    {
        for (int i = 0; i < GambleButtonSlotCount; i++)
        {
            float top = 0.77f - i * 0.06f;
            float bottom = top - 0.05f;

            RectTransform panel = CreatePanel(transform, $"GambleButtonPanel{i}", new Color(1f, 1f, 1f, 0.15f));
            SetAnchors(panel, new Vector2(0.51f, bottom), new Vector2(0.70f, top));

            int index = i;
            Button button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => OnGambleButtonClicked(index));

            Text label = CreateLabel(panel, $"GambleButtonText{i}", "");
            label.fontSize = 16;
            label.raycastTarget = false;

            gambleButtonPanels[i] = panel.gameObject;
            gambleButtonTexts[i] = label;
            gambleButtonComponents[i] = button;
            lastGambleButtonWood[i] = int.MinValue;
            gambleButtonPanels[i].SetActive(false);
        }
    }

    // 단일 선택 + UnitData.gambleOptions 항목 수만큼 보인다(트레잇 버튼과 같은 관례). 버튼은
    // 목재가 모자라도 항상 눌리게 둔다 — 원작이 "목재 부족=stop 명령"이라 조건 미달을
    // 구매 실패와 다르게 다뤄야 하고(OnGambleButtonClicked 참고), interactable을 꺼서
    // 미리 막으면 그 구분이 화면에 아예 안 보인다.
    void RefreshGambleButtons()
    {
        SelectionManager selection = Selection;
        UnitData data = null;
        OwnedByPlayer owner = null;
        if (selection != null && selection.Selected.Count == 1)
        {
            Selectable single = selection.Selected[0];
            if (single != null && single.TryGetComponent(out UnitIdentity identity) && identity.Data != null)
            {
                data = identity.Data;
                single.TryGetComponent(out owner);
            }
        }

        int count = (data != null && data.gambleOptions != null) ? data.gambleOptions.Count : 0;
        PlayerContext context = owner != null ? PlayerContext.Get(owner.OwnerId) : null;
        ResourceWallet wallet = context != null ? context.ResourceWallet : null;

        for (int i = 0; i < GambleButtonSlotCount; i++)
        {
            if (gambleButtonPanels[i] == null) continue;
            if (i >= count || wallet == null) { HideGambleButton(i); continue; }

            UnitGambleOption option = data.gambleOptions[i];

            // 결과가 없는 항목(A0OC처럼 resultPool이 아직 안 채워진 경우)은 버튼을 안 띄운다
            // — 목재를 쓰고 성공해도 "조용히 아무 일도 안 남"이면 플레이어가 버그로 본다
            // (PM 지시 2026-09-07). 채워지면 자동으로 다시 뜬다.
            bool hasResult = option.resultUnit != null || (option.resultPool != null && option.resultPool.Count > 0);
            if (!hasResult) { HideGambleButton(i); continue; }

            gambleButtonPanels[i].SetActive(true);

            int wood = wallet.Get(ResourceType.Wood);
            if (wood == lastGambleButtonWood[i]) continue;
            lastGambleButtonWood[i] = wood;

            gambleButtonTexts[i].text = $"{data.unitName} 시전({option.abilityId})\n(목재 {option.woodCost} 소모, 보유 {wood}, 성공 {option.successChance:P0})";
        }
    }

    void HideGambleButton(int index)
    {
        if (gambleButtonPanels[index] != null && gambleButtonPanels[index].activeSelf) gambleButtonPanels[index].SetActive(false);
        lastGambleButtonWood[index] = int.MinValue;
    }

    // ANCIENT_SHIP_SPEC_2026-09-06.md 표(A023 기준 확인값)를 셋 다에 같은 경로로 적용한다:
    // 목재 woodCost(성공·실패 둘 다 차감) → successChance → 성공 시 resultUnit(또는
    // resultPool에서 랜덤) 생성. 시전 유닛은 성공·실패 무관하게 항상 사라진다(RemoveUnit)
    // — 단, 목재가 애초에 모자라면 이 함수는 아무것도 안 하고 끝난다(차감도 소모도 없음,
    // "조건 미달"과 "도박 실패"를 같은 경로로 처리하면 안 된다는 사양 경고 그대로).
    void OnGambleButtonClicked(int index)
    {
        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) return;

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null) return;

        List<UnitGambleOption> options = identity.Data.gambleOptions;
        if (options == null || index >= options.Count) return;
        UnitGambleOption option = options[index];

        if (!single.TryGetComponent(out OwnedByPlayer owner)) return;

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        ResourceWallet wallet = context != null ? context.ResourceWallet : null;
        if (wallet == null) return;

        // 목재 부족 — 실패가 아니라 "조건 미달"이다. TrySpend가 모자라면 아무것도 안 깎고
        // false를 돌려주므로 여기서 그냥 리턴하면 원작의 "차감 없이 stop 명령만"과 같다.
        if (!wallet.TrySpend(ResourceType.Wood, option.woodCost))
        {
            PlayerNotification.Show(owner.OwnerId, "목재가 부족합니다!");
            return;
        }

        Vector3 shipPosition = identity.transform.position;
        bool success = Random.value < option.successChance;

        // 여기부턴 목재가 이미 나갔다 — 성공/실패 상관없이 시전 유닛이 사라진다(원작 RemoveUnit).
        identity.Consume();

        if (!success) return;

        UnitData resultUnit = option.resultUnit;
        if (resultUnit == null && option.resultPool != null && option.resultPool.Count > 0)
            resultUnit = option.resultPool[Random.Range(0, option.resultPool.Count)];

        UnitSpawner spawner = Spawner;
        if (spawner == null || resultUnit == null) return;

        // "조합 구역 중심"(udg_Mix_Loction) — MapGenerator.BuildIsland가 "CombineTable"
        // 이름으로 만드는 섬 오브젝트가 우리 쪽 대응이다(런타임엔 이름으로 찾는 것 말고
        // 다른 참조 경로가 없다 — 04번 RewireScene의 GameObject.Find("Lane")과 같은 이유).
        GameObject combineTable = GameObject.Find("CombineTable");
        Vector3 targetPosition = combineTable != null ? combineTable.transform.position : shipPosition;

        // UnitSpawner.Spawn은 위치를 그대로 Instantiate할 뿐 NavMesh 위인지 확인 안 한다
        // (ExecuteTransform과 같은 이유로 여기서 미리 붙인다) — 결과 유닛의 이동 능력
        // 기준으로 가장 가까운 밟을 수 있는 자리를 찾는다.
        int areaMask = UnitSpawner.ComputeAreaMask(resultUnit.movementAbility);
        Vector3 spawnPosition = NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, TransformSampleRadius, areaMask)
            ? hit.position
            : targetPosition;
        spawner.Spawn(resultUnit, spawnPosition, owner.OwnerId);
    }

    // "유닛 판매" 버튼(2026-09-06, PM 지시) — 고대의 배 버튼 바로 아래, 같은 열.
    // UnitData.sellRewardWisp/sellRewardTraitPoints 둘 다 비어있으면(기본값) 버튼 자체가
    // 안 뜬다 — 트레잇·고대의배 버튼과 같은 관례("보상이 없으면 버튼이 없다").
    void BuildSellButton()
    {
        RectTransform panel = CreatePanel(transform, "SellButtonPanel", new Color(1f, 1f, 1f, 0.15f));
        SetAnchors(panel, new Vector2(0.51f, 0.78f), new Vector2(0.70f, 0.83f));

        Button button = panel.gameObject.AddComponent<Button>();
        button.onClick.AddListener(OnSellButtonClicked);

        sellButtonText = CreateLabel(panel, "SellButtonText", "");
        sellButtonText.fontSize = 16;
        sellButtonText.raycastTarget = false;

        sellButtonPanel = panel.gameObject;
        sellButtonComponent = button;
        sellButtonPanel.SetActive(false);
    }

    // 단일 선택 + 판매 보상이 하나라도 있을 때만 보인다.
    void RefreshSellButton()
    {
        if (sellButtonPanel == null) return;

        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) { HideSellButton(); return; }

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null ||
            (identity.Data.sellRewardWisp == null && identity.Data.sellRewardTraitPoints <= 0 &&
             identity.Data.sellTriggersItemGamblePool == null))
        { HideSellButton(); return; }

        sellButtonPanel.SetActive(true);

        if (identity.Data == lastSellButtonUnit) return;
        lastSellButtonUnit = identity.Data;

        string wispPart = identity.Data.sellRewardWisp != null
            ? $"{identity.Data.sellRewardWisp.wispName} 1기"
            : null;
        string pointPart = identity.Data.sellRewardTraitPoints > 0
            ? $"특성포인트 {identity.Data.sellRewardTraitPoints}"
            : null;
        string gamblePart = identity.Data.sellTriggersItemGamblePool != null
            ? "아이템 도박 1회"
            : null;

        StringBuilder rewardDesc = new StringBuilder();
        foreach (string part in new[] { wispPart, pointPart, gamblePart })
        {
            if (part == null) continue;
            if (rewardDesc.Length > 0) rewardDesc.Append(" + ");
            rewardDesc.Append(part);
        }
        sellButtonText.text = $"판매\n({rewardDesc})";
    }

    void HideSellButton()
    {
        if (sellButtonPanel != null && sellButtonPanel.activeSelf) sellButtonPanel.SetActive(false);
        lastSellButtonUnit = null;
    }

    // 원작 GetSoldUnit() 대응 — 보상 지급 후 유닛 소모(RemoveUnit과 같다, UnitIdentity.
    // Consume). 조건 미달 개념이 없다(고대의 배와 달리 비용이 없어 항상 성공) — 버튼이
    // 뜬 시점에 이미 보상이 확정돼 있다.
    void OnSellButtonClicked()
    {
        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) return;

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null ||
            (identity.Data.sellRewardWisp == null && identity.Data.sellRewardTraitPoints <= 0 &&
             identity.Data.sellTriggersItemGamblePool == null)) return;

        if (!single.TryGetComponent(out OwnedByPlayer owner)) return;

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        if (context != null)
        {
            if (identity.Data.sellRewardTraitPoints > 0)
                context.UnitUpgrades?.AddTraitPoints(identity.Data.sellRewardTraitPoints);

            if (identity.Data.sellRewardWisp != null && RewardDistributor.Instance != null)
            {
                List<WispReward> reward = new List<WispReward> { new WispReward { wisp = identity.Data.sellRewardWisp, count = 1 } };
                RewardDistributor.Instance.GrantWisps(context, reward);
            }

            // h0BS(메타몽) 전용 — 재고(ItemGambleState.stock) 차감·도박·풀 제외 등록은
            // TryGamble 안에서 전부 처리한다(§31). 재고가 0이면 false를 돌려주지만
            // 판매(유닛 소멸) 자체는 막지 않는다 — 위 sellRewardWisp/TraitPoints와 같은
            // 관례로 "보상이 안 나올 수 있어도 판매는 항상 된다".
            if (identity.Data.sellTriggersItemGamblePool != null && context.ItemGambleState != null &&
                context.ItemGambleState.TryGamble(identity.Data.sellTriggersItemGamblePool, out ItemData wonItem) &&
                wonItem != null)
            {
                ItemInventoryRef?.Add(wonItem);
            }
        }

        // 보상 지급 뒤에 소모한다 — Consume이 오브젝트를 파괴하므로 그 전에 owner/보상을
        // 전부 읽어둬야 한다(위에서 이미 다 읽었다).
        identity.Consume();
    }

    // 항법 지속 버튼 — 유닛 선택과 무관하게 항상 보인다. 안 골랐으면 "항법 선택",
    // 골랐으면 "항법: <이름>"으로 바뀐다(되돌릴 수 없다는 걸 상시 노출).
    void BuildNavigationUI()
    {
        RectTransform panel = CreatePanel(transform, "NavigationButtonPanel", new Color(1f, 1f, 1f, 0.15f));
        SetAnchors(panel, new Vector2(0.51f, 0.84f), new Vector2(0.70f, 0.89f));

        Button button = panel.gameObject.AddComponent<Button>();
        button.onClick.AddListener(OnNavigationButtonClicked);

        navigationButtonText = CreateLabel(panel, "NavigationButtonText", "항법 선택");
        navigationButtonText.fontSize = 16;
        navigationButtonText.raycastTarget = false;

        navigationButtonPanel = panel.gameObject;

        BuildNavigationModal();
    }

    void RefreshNavigationButton()
    {
        if (navigationButtonText == null) return;

        NavigationState state = PlayerContext.Local?.NavigationState;
        bool hasChosen = state != null && state.HasChosen;
        NavigationChoice choice = state != null ? state.Choice : NavigationChoice.None;

        if (navigationButtonTextInitialized && hasChosen == lastNavigationHasChosen && choice == lastNavigationChoice) return;
        navigationButtonTextInitialized = true;
        lastNavigationHasChosen = hasChosen;
        lastNavigationChoice = choice;

        navigationButtonText.text = hasChosen ? $"항법: {NavigationDisplayName(choice)}" : "항법 선택";
    }

    static string NavigationDisplayName(NavigationChoice choice)
    {
        int index = System.Array.IndexOf(NavigationOptionOrder, choice);
        return index >= 0 ? NavigationOptionNames[index] : choice.ToString();
    }

    // 화면 중앙 모달. 5행 + 상단 경고문 + 하단 "닫기"(안 고르고 진행 가능, 강제 아님).
    // 배경 Image가 raycastTarget 기본 true라 열려 있는 동안 뒤쪽 HUD 클릭을 자연히 막는다.
    void BuildNavigationModal()
    {
        RectTransform modal = CreatePanel(transform, "NavigationModalPanel", new Color(0.05f, 0.05f, 0.05f, 0.92f));
        SetAnchors(modal, new Vector2(0.20f, 0.12f), new Vector2(0.80f, 0.88f));

        RectTransform titleHolder = NewHolder(modal, "NavigationModalTitleHolder", new Vector2(0f, 0.90f), new Vector2(1f, 1f));
        Text title = CreateLabel(titleHolder, "NavigationModalTitle",
            "항법(진행 루트) 선택 — 플레이어당 평생 1회, 되돌릴 수 없습니다");
        title.fontSize = 20;
        title.color = new Color(1f, 0.55f, 0.35f);
        title.raycastTarget = false;

        RectTransform subtitleHolder = NewHolder(modal, "NavigationModalSubtitleHolder", new Vector2(0f, 0.84f), new Vector2(1f, 0.90f));
        Text subtitle = CreateLabel(subtitleHolder, "NavigationModalSubtitle",
            "선택하지 않아도 진행할 수 있습니다 — 다섯 효과 모두 비활성 상태로 유지됩니다.");
        subtitle.fontSize = 15;
        subtitle.raycastTarget = false;

        for (int i = 0; i < NavigationOptionOrder.Length; i++)
        {
            float top = 0.82f - i * 0.1525f;
            float bottom = top - 0.13f;

            RectTransform rowPanel = CreatePanel(modal, $"NavigationRow{i}", new Color(1f, 1f, 1f, 0.08f));
            SetAnchors(rowPanel, new Vector2(0.02f, bottom), new Vector2(0.98f, top));

            RectTransform textHolder = NewHolder(rowPanel, "Text", new Vector2(0f, 0f), new Vector2(0.72f, 1f));
            Text label = CreateLabel(textHolder, "Label", "");
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 15;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false;

            RectTransform buttonHolder = CreatePanel(rowPanel, "SelectButton", new Color(1f, 1f, 1f, 0.25f));
            SetAnchors(buttonHolder, new Vector2(0.75f, 0.15f), new Vector2(0.98f, 0.85f));
            Button rowButton = buttonHolder.gameObject.AddComponent<Button>();
            int capturedIndex = i;
            rowButton.onClick.AddListener(() => OnNavigationOptionClicked(capturedIndex));
            Text buttonLabel = CreateLabel(buttonHolder, "SelectButtonLabel", "선택");
            buttonLabel.fontSize = 16;
            buttonLabel.raycastTarget = false;

            navigationRowTexts[i] = label;
            navigationRowButtons[i] = rowButton;
            navigationRowButtonLabels[i] = buttonLabel;
        }

        RectTransform closeHolder = CreatePanel(modal, "NavigationModalClose", new Color(1f, 1f, 1f, 0.2f));
        SetAnchors(closeHolder, new Vector2(0.40f, 0.005f), new Vector2(0.60f, 0.055f));
        Button closeButton = closeHolder.gameObject.AddComponent<Button>();
        closeButton.onClick.AddListener(OnNavigationCloseClicked);
        Text closeLabel = CreateLabel(closeHolder, "NavigationModalCloseLabel", "닫기");
        closeLabel.fontSize = 16;
        closeLabel.raycastTarget = false;

        navigationModalPanel = modal.gameObject;
        navigationModalPanel.SetActive(false);
    }

    static RectTransform NewHolder(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetAnchors(rect, min, max);
        return rect;
    }

    void OnNavigationButtonClicked()
    {
        if (navigationModalPanel == null) return;

        bool nowVisible = !navigationModalPanel.activeSelf;
        navigationModalPanel.SetActive(nowVisible);
        if (nowVisible) RefreshNavigationModalContent();
    }

    void OnNavigationCloseClicked()
    {
        if (navigationModalPanel != null) navigationModalPanel.SetActive(false);
    }

    // 되돌릴 수 없다 — NavigationState.TrySelect 자체가 이미 골랐으면 실패한다(HashSet
    // 한번잠금과 같은 성격). 여기 가드는 UI에서 버튼을 안 보이게/비활성화하는 것뿐이고
    // 실제 방지는 TrySelect가 한다(코드 경로로도 재선택 불가).
    void OnNavigationOptionClicked(int index)
    {
        NavigationState state = PlayerContext.Local?.NavigationState;
        if (state == null || state.HasChosen) return;

        state.TrySelect(NavigationOptionOrder[index]);
        RefreshNavigationModalContent();
        navigationModalPanel.SetActive(false);
    }

    void RefreshNavigationModalContent()
    {
        NavigationState state = PlayerContext.Local?.NavigationState;
        bool hasChosen = state != null && state.HasChosen;
        NavigationChoice choice = state != null ? state.Choice : NavigationChoice.None;

        for (int i = 0; i < NavigationOptionOrder.Length; i++)
        {
            bool isChosenRow = hasChosen && choice == NavigationOptionOrder[i];

            string body = $"{NavigationOptionNames[i]}\n{NavigationOptionDescriptions[i]}";
            if (isChosenRow) body += "\n▶ 선택됨 — 되돌릴 수 없습니다";
            navigationRowTexts[i].text = body;

            if (navigationRowButtons[i] != null) navigationRowButtons[i].interactable = !hasChosen;
            if (navigationRowButtonLabels[i] != null)
                navigationRowButtonLabels[i].text = isChosenRow ? "선택됨" : hasChosen ? "선택 불가" : "선택";
        }
    }

    // 원작 순서(Trig_T_Ability_hero_Conditions) 그대로: 이미 샀는가 → 포인트가 충분한가
    // (모자라면 아무것도 안 바뀌고 리턴 — 실패 경로가 포인트를 먹으면 안 된다) → 차감 → Unlock.
    // TrySpendTraitPoints가 확인+차감을 한 호출로 묶어서 그 사이 다른 소비가 못 끼어든다.
    void OnTraitButtonClicked()
    {
        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) return;

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null) return;

        UnitTraitData trait = identity.Data.trait;
        if (trait == null) return;

        // 방어적 재확인 — 버튼이 non-interactable이라 정상 경로로는 여기까지 안 온다.
        // RefreshTraitButton의 같은 검사와 짝(transformIntoUnit이 채워지기 전엔 여기서 막힌다).
        if (trait.isTransformType && trait.transformIntoUnit == null) return;

        if (!single.TryGetComponent(out OwnedByPlayer owner)) return;

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        UnitUpgrades upgrades = context != null ? context.UnitUpgrades : null;
        if (upgrades == null) return;

        // 06번⑤(반복구매형, 아카이누) — 다른 25개는 이미 언락됐으면 여기서 막지만,
        // 이 하나만 언락 뒤에도 계속 구매 가능하다(원작: 몇 번이든 다시 살 수 있다).
        if (upgrades.IsUnlocked(trait) && !trait.isRepeatablePurchase) return;

        if (!upgrades.TrySpendTraitPoints(trait.costTraitPoints))
        {
            // 플레이어가 보고 행동을 바꿀 수 있는 실패라 화면에 띄운다(PlayerNotification.cs
            // 상단 코멘트의 기준 그대로) — 예전엔 Debug.Log라 콘솔에만 남았다.
            PlayerNotification.Show(owner.OwnerId, "특성 포인트가 부족합니다!");
            return;
        }

        // 반복구매형은 이미 unlockedTraits에 있어 Unlock()이 그냥 no-op(HashSet.Add가
        // false를 돌려줄 뿐)이다 — 실제 "몇 번째 구매인가"는 아래 카운터가 센다.
        upgrades.Unlock(trait);
        if (trait.isRepeatablePurchase) upgrades.IncrementRepeatablePurchase(trait);

        // Unlock 이후에 실행한다 — 실행이 실패해도(스포너 못 찾음 등) 언락 자체는 이미
        // 되돌릴 수 없으니(HashSet.Add) 순서를 바꿔봤자 의미가 없고, 오히려 언락 전에
        // 유닛을 먼저 없애면 실패 시 포인트도 나가고 유닛도 사라지는 최악의 경우가 된다.
        if (trait.isTransformType)
        {
            ExecuteTransform(identity, trait, owner.OwnerId);
        }

        // 06번⑥(순수스탯형, 타시기 전용) — 스킬승급·능력교체와 달리 "언락 상태를 매
        // 프레임 읽는" 지속 효과가 아니라 구매 시점에 딱 한 번 실행하는 지급이다.
        if (trait.heroXpGrant > 0 || trait.purchasedStatGrantEach > 0)
        {
            ExecuteStatGrant(single, trait);
        }

        // 다음 정기 갱신을 안 기다리고 바로 라벨을 다시 그린다.
        lastTraitButtonPoints = int.MinValue;
    }

    // 06번⑥ 실행 — 선택된 그 유닛 인스턴스에만 적용한다(ExecuteTransform과 같은 범위,
    // 레인 전체로 안 퍼뜨린다 — 이 트레잇은 "그 유닛을 골라 사는" 구매고, 도움소
    // 「능력치 증가」(GrantHeroStatIncreaseToLane, 레인 전체 브로드캐스트)와는 성격이 다르다).
    void ExecuteStatGrant(Selectable single, UnitTraitData trait)
    {
        if (!single.TryGetComponent(out UnitAttacker attacker)) return;

        if (trait.heroXpGrant > 0) attacker.AddHeroXp(trait.heroXpGrant);

        if (trait.purchasedStatGrantEach > 0)
        {
            attacker.AddPurchasedStat(0, trait.purchasedStatGrantEach);
            attacker.AddPurchasedStat(1, trait.purchasedStatGrantEach);
            attacker.AddPurchasedStat(2, trait.purchasedStatGrantEach);
        }
    }

    // 06번③ 변신 실행. CombineSystem.TryCombine의 뼈대(재료 UnitIdentity.Consume() → 결과
    // UnitSpawner.Spawn())를 "재료 여러 개"에서 "자기 자신 하나"로 좁혀 재사용한다(PM 지시
    // 2026-09-05) — CombineSystem 자체는 만지지 않는다(레시피 목록 기반이라 이 경로와 모양이
    // 안 맞고, 그 파일은 구현담당1 소유일 수 있다).
    //
    // 원작(Trig_T_Ability_hero_Actions, #011 H097→H0B1 아오키지·#016 H091→H093 쵸파)은
    // RemoveUnit 후 CreateNUnitsAtLoc으로 새 유닛을 만들고 GetHeroXP/GetHeroStatBJ로 뽑은
    // 경험치·STR/AGI/INT를 새 유닛에 되돌린다. **우리는 그 축(레벨·능력치) 자체가 없어서
    // 이어받을 게 없다** — 대신 우리 쪽에서 실제로 이어져야 하는 건:
    //   · 소유자(ownerId)  — Consume()이 옛 것을 지우고, Spawn(..., ownerId)이 새 것을 같은
    //     플레이어 소유로 만든다.
    //   · 위치            — 옛 유닛이 서 있던 자리 근처(NavMesh 샘플링, CombineSystem.
    //     ResolveResultPosition과 같은 이유 — 새 유닛의 이동 능력이 옛 유닛과 다를 수 있어
    //     areaMask를 새로 잰다)에 새 유닛을 놓는다.
    //   · 인벤토리 등록    — Consume()이 옛 것을 UnitInventory에서 빼고, Spawn()이 새 것을
    //     넣는다(둘 다 기존 경로 그대로, 새 코드 없음).
    //   · 선택 상태        — 옛 유닛이 선택돼 있었으니 새 유닛도 선택해 넘긴다. 안 하면
    //     변신하자마자 선택이 풀려 방금 바뀐 유닛의 상태(체력바 등)를 못 본다.
    // 버리는 것: 전투 상태(UnitCombat의 추적 대상·이동 명령·홀딩)는 새 유닛이 항상 Idle로
    // 시작한다 — 원작도 경험치·능력치 말고는 아무것도 안 옮긴다.
    //
    // transformIntoUnit은 사장님 배정 전까지 null이라(H0B1·H097→H0B1, H091→H093이 우리
    // 로스터의 어떤 유닛이 될지 미정) 이 메서드는 지금 절대 안 불린다 — 호출부(위)가
    // transformIntoUnit==null이면 구매 자체를 막는다.
    void ExecuteTransform(UnitIdentity oldIdentity, UnitTraitData trait, int ownerId)
    {
        UnitSpawner spawner = Spawner;
        if (spawner == null)
        {
            // 포인트는 이미 나갔다(Unlock 이전 순서 주석 참고) — 되돌리지 않는다. 조합·
            // 뽑기 등 다른 소비 경로도 스포너가 없으면 이 이상은 실패로 남긴다.
            Debug.LogWarning("GameHud: UnitSpawner를 찾지 못해 변신을 실행하지 못했습니다 " +
                              "(특성 포인트는 이미 차감됐습니다).", this);
            return;
        }

        Vector3 oldPosition = oldIdentity.transform.position;
        int areaMask = UnitSpawner.ComputeAreaMask(trait.transformIntoUnit.movementAbility);
        Vector3 spawnPosition = NavMesh.SamplePosition(oldPosition, out NavMeshHit hit, TransformSampleRadius, areaMask)
            ? hit.position
            : oldPosition;

        oldIdentity.Consume();

        GameObject newUnit = spawner.Spawn(trait.transformIntoUnit, spawnPosition, ownerId);
        if (newUnit != null && newUnit.TryGetComponent(out Selectable newSelectable))
        {
            Selection?.SelectOnly(newSelectable);
        }
    }

    // CombineSystem.ResultSampleRadius와 같은 값·같은 이유 — 자리가 NavMesh 경계에 살짝
    // 걸쳐 있어도 실제로 밟을 수 있는 땅 근처로 붙인다.
    const float TransformSampleRadius = 4f;

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
    // 조합표와 같은 색을 쓴다. 여기서 따로 정의하면 같은 등급이 화면마다 달라 보인다.
    static Color GetGradeColor(UnitGrade grade)
    {
        Color color = grade.Color();
        color.a = 0.9f;
        return color;
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

        // 인터페이스 변수라 != null이 유니티 == 오버로드를 안 거친다 — as Object로 진짜 파괴 여부를 본다.
        if (currentShop as Object != null)
        {
            int logicalIndex = index < shopLogicalSlotIndex.Length ? shopLogicalSlotIndex[index] : -1;
            string tooltip = logicalIndex >= 0 ? currentShop.GetSlotTooltip(logicalIndex) : null;
            if (string.IsNullOrEmpty(tooltip)) { HideCombineTooltip(); return; }
            ShowTooltip(tooltip, cardRect);
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

        // 영원 등급 등 requiredSaveCount 조합식은 왜 안 되는지 알 수 있어야 한다 — 조용히
        // 실패하면(카드만 흐려짐) 버그로 보인다(PM 지시 2026-09-05).
        if (recipe.requiredSaveCount > 0)
            tooltipBuilder.Append("\n(클리어 ").Append(recipe.requiredSaveCount).Append("회 필요)");

        // maxRound도 같은 이유로 표시한다 — 지금은 제한됨_강보명 1개뿐이지만 minRound는
        // 전부 0이라 아직 아무 데도 안 뜬다(값이 생기면 그때 저절로 뜬다).
        if (recipe.minRound > 0)
            tooltipBuilder.Append("\n(R").Append(recipe.minRound).Append("부터)");
        if (recipe.maxRound > 0)
            tooltipBuilder.Append("\n(R").Append(recipe.maxRound).Append("까지만)");

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

    // 유닛 명령 자리(공격/정지/모으기/정렬 + 맨 아랫줄은 조합 결과). 0~2번·12번은 고정
    // placeholder로 인터랙션을 꺼둔다(정지/모으기/정렬만 켜짐). 나머지는 RefreshUnitCommandCards가 채운다.
    static readonly Color UnitCommandDefaultColor = new Color(1f, 1f, 1f, 0.12f);

    void BuildUnitCommandGrid(RectTransform parent)
    {
        GridLayoutGroup grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        // 4행(50) 높이 그대로 5행에 나눠 담느라 칸 높이를 줄인다: (50*4 + 6*3) / 5 - 6 = 38.8.
        // 패널 크기는 그대로 두면서 정렬(C) 칸 하나를 더 넣기 위한 계산이라 임의로 줄인 값이 아니다.
        grid.cellSize = new Vector2(90f, 38.8f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CommandColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < CommandSlotCount; i++)
        {
            BuildUnitCommandSlot(i, parent);

            if (i < UnitOnlyCommandLabels.Length)
            {
                unitCommandSlotNames[i].text = UnitOnlyCommandLabels[i];
                // 정지·모으기는 동작이 붙었다. 공격은 아직 없어서 눌리지 않게 둔다.
                unitCommandSlotButtons[i].interactable = i == HoldCommandSlot || i == GatherCommandSlot;
            }
            else if (i == AlignCommandSlot)
            {
                unitCommandSlotNames[i].text = AlignCommandLabel;
                unitCommandSlotButtons[i].interactable = true;
            }
            else
            {
                // 조합 결과가 들어올 칸. 채워지기 전까지는 보이지 않게 둔다.
                unitCommandSlotBackgrounds[i].color = Color.clear;
            }
        }
    }

    // 공격·정지·모으기·정렬 네 칸. 유닛에게만 의미가 있어서 건물을 고르면 통째로 감춘다.
    void SetUnitOnlyCommandsVisible(bool visible)
    {
        for (int i = 0; i < UnitOnlyCommandLabels.Length; i++)
        {
            unitCommandSlotNames[i].text = visible ? UnitOnlyCommandLabels[i] : "";
            unitCommandSlotBackgrounds[i].color = visible ? UnitCommandDefaultColor : Color.clear;
            unitCommandSlotButtons[i].interactable =
                visible && (i == HoldCommandSlot || i == GatherCommandSlot);
        }

        unitCommandSlotNames[AlignCommandSlot].text = visible ? AlignCommandLabel : "";
        unitCommandSlotBackgrounds[AlignCommandSlot].color = visible ? UnitCommandDefaultColor : Color.clear;
        unitCommandSlotButtons[AlignCommandSlot].interactable = visible;
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

    // 0 공격은 아직 없다. 1 정지 = 홀드(H), 2 모으기 = 같은 이름 불러모으기(V).
    static readonly string[] UnitOnlyCommandLabels = { "공격", "정지 (H)", "모으기 (V)" };

    const int HoldCommandSlot = 1;
    const int GatherCommandSlot = 2;

    // 조합·상점 9칸(3~11) 뒤에 새로 붙은 13번째 칸(인덱스 12) — 정렬(C).
    // 0~2번처럼 앞자리에 끼워 넣으면 UnitCommandResultSlotOrder가 이미 3~11을 전부 쓰고 있어 겹친다.
    const int AlignCommandSlot = 12;
    const string AlignCommandLabel = "정렬 (C)";

    void OnUnitCommandSlotClicked(int index)
    {
        // 단축키와 같은 함수를 부른다 — 두 곳에 따로 구현하면 한쪽만 고쳐진다.
        if (index == HoldCommandSlot || index == GatherCommandSlot || index == AlignCommandSlot)
        {
            SelectionManager selection = Selection;
            if (selection == null || selection.Selected.Count == 0) return;

            if (index == HoldCommandSlot) UnitCommands.ToggleHold(selection.Selected);
            else if (index == GatherCommandSlot) UnitCommands.Gather(selection.Selected);
            else UnitCommands.SendToPen(selection.Selected);
            return;
        }

        if (currentShop as Object != null)
        {
            OnShopSlotClicked(index);
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
        else
        {
            // ⚠️ 2026-09-05: CanCombineNow는 자원만 보고 spawner==null·result.prefab==null은
            // 안 봐서, 그 경우 버튼은 켜져 있는데 눌러도 여기로 빠져 완전 무반응이었다
            // ("조용한 실패" #11). 구체적 원인은 CombineSystem.TryCombine이 이미
            // Debug.LogWarning으로 남긴다(배선 오류라 플레이어가 할 수 있는 게 없다) —
            // 여기서는 "눌렀는데 안 됐다"는 것만 화면에 알린다.
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "지금은 조합할 수 없습니다.");
        }
    }

    // 상점 칸 클릭. targetKind == None(마나포션, 도박 굴리기, 강화 구매 등)은 위치·대상이 필요
    // 없어 그 자리에서 바로 실행하고, 나머지는 커서로 지점/유닛을 찍을 때까지 기다린다
    // (RefreshShopTargeting). "대상이 필요한가"는 상점이 GetSlotView로 스스로 답한다 —
    // GameHud는 어떤 상점·어떤 칸인지 몰라도 된다.
    void OnShopSlotClicked(int visualIndex)
    {
        if (currentShop as Object == null) return;

        int logicalIndex = visualIndex >= 0 && visualIndex < shopLogicalSlotIndex.Length
            ? shopLogicalSlotIndex[visualIndex] : -1;
        if (logicalIndex < 0) return;

        LaneShopSlotView view = currentShop.GetSlotView(logicalIndex);
        if (string.IsNullOrEmpty(view.label) || !view.available) return;

        if (view.targetKind == LaneShopTargetKind.None)
        {
            // ⚠️ 2026-09-05: TryUse가 false여도 예전엔 그냥 끝났다 — 눌렀는데 아무 일도
            // 안 일어난 것처럼 보였다("조용한 실패" #10). ILaneShop.TryUse가 이제 실패
            // 사유를 out으로 돌려준다(상점 4곳이 이미 알고 있던 사유를 그대로 올려보낸다) —
            // 사유가 없으면(배선 오류 등, 플레이어가 봐도 못 고침) 일반 문구로 대신한다.
            if (currentShop.TryUse(logicalIndex, default, out string reason)) RefreshShopAffordability();
            else PlayerNotification.Show(LocalPlayer.LocalPlayerId, reason ?? "지금은 사용할 수 없습니다.");
            return;
        }

        pendingShop = currentShop;
        pendingSlotIndex = logicalIndex;
        pendingTargetKind = view.targetKind;
        targetingStartFrame = Time.frameCount;
    }

    // 칸을 고른 뒤 다음 클릭을 기다린다. 우클릭이면 취소. uGUI 위 클릭(다른 버튼 등)은
    // SelectionManager와 같은 이유로 무시한다 — 칸 클릭 그 자체가 targetingStartFrame
    // 이전 프레임이라 같은 클릭이 대상 지정으로 다시 잡히는 일은 없다.
    // 대상 지정 상태는 실제 실행(TryUse) 전에 먼저 지운다 — 실패해도 커서 대기 상태가 안 남는다.
    void RefreshShopTargeting()
    {
        // 인터페이스 변수로 == null을 하면 유니티의 == 오버로드를 안 거친다. 상점 오브젝트가
        // 파괴돼도 이 검사를 통과하고, 다음 줄에서 MissingReferenceException이 난다.
        // (오늘 맵 생성이 통째로 죽었던 것과 같은 함정이다.)
        if (pendingSlotIndex < 0 || pendingShop as Object == null) { pendingSlotIndex = -1; return; }
        if (Mouse.current == null) { pendingSlotIndex = -1; return; }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            pendingSlotIndex = -1;
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.frameCount <= targetingStartFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        ILaneShop shop = pendingShop;
        int index = pendingSlotIndex;
        LaneShopTargetKind kind = pendingTargetKind;
        pendingSlotIndex = -1;

        // ⚠️ 2026-09-05: 아무것도 안 맞으면 예전엔 그냥 취소됐다("조용한 실패" #10) — 대상
        // 지정 모드로 들어갔다가 빈 허공을 눌러서 조용히 풀리면, 방금 그게 취소인지 실패인지
        // 플레이어가 구분할 수 없었다. "대상을 못 찾음"과 "대상은 찾았는데 실행이 안 됨"을
        // 갈라서 알린다 — 플레이어가 할 행동이 다르다(다시 조준 vs 자원/조건 확인).
        if (!WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "대상을 찾을 수 없습니다.");
            return;
        }

        bool used;
        string reason;
        if (kind == LaneShopTargetKind.Unit)
        {
            // 연금술(자기 유닛)은 Selectable로 잡히지만, 흡수(적 유닛)의 대상인 EnemyDummy는
            // Selectable이 없다(적한테 그걸 붙이면 드래그 선택·명령키에 같이 걸린다) — 그래서
            // Selectable을 먼저 보고, 없으면 EnemyDummy로 한 번 더 본다.
            GameObject targetObject = null;
            if (hit.collider.TryGetComponent(out Selectable selectable)) targetObject = selectable.gameObject;
            else if (hit.collider.TryGetComponent(out EnemyDummy enemyTarget)) targetObject = enemyTarget.gameObject;

            if (targetObject == null)
            {
                PlayerNotification.Show(LocalPlayer.LocalPlayerId, "대상으로 쓸 수 없습니다.");
                return;
            }

            used = shop.TryUse(index, LaneShopTarget.OnUnit(targetObject), out reason);
        }
        else
        {
            used = shop.TryUse(index, LaneShopTarget.AtPoint(hit.point), out reason);
        }

        if (used) RefreshShopAffordability();
        else PlayerNotification.Show(LocalPlayer.LocalPlayerId, reason ?? "지금은 사용할 수 없습니다.");
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

        ILaneShop shop = null;
        UnitData selectedData = null;

        if (count == 1)
        {
            Selectable single = selection.Selected[0];
            if (single != null)
            {
                if (single.TryGetComponent(out ILaneShop shopComponent))
                    shop = shopComponent;
                else if (single.TryGetComponent(out UnitIdentity identity))
                    selectedData = identity.Data;
            }
        }

        // 레인 상점이 선택된 동안은 조합 카드 로직(조합 레시피 캐시·흐림 처리)을 아예 건드리지 않는다 —
        // 12칸의 내용·클릭 의미만 상점 칸으로 바뀐다.
        if (shop != currentShop)
        {
            currentShop = shop;
            pendingSlotIndex = -1;
            RebuildShopSlots(shop);

            if (shop == null)
            {
                // 상점 모드에서 빠져나온 직후 — 직전과 같은 유닛을 다시 선택해도
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
                RefreshShopAffordability();
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

    // 상점 칸도 조합 결과와 같은 9칸 순서(UnitCommandResultSlotOrder)를 그대로 재사용한다.
    // 0~2번(공격/정지/모으기)은 건물에 쓸 수 없는 명령이라 상점을 고르면 감춘다 —
    // 도박소를 눌렀는데 "모으기"가 떠 있으면 누를 수 있는 것처럼 보인다.
    // 논리 인덱스(상점 쪽 슬롯 번호)만 기억해두고, 이름·색은 RefreshShopAffordability가 채운다 —
    // GetSlotView가 이미 값을 캐시해서 돌려주므로(Docs/design/LANE_SHOP.md) 여기서 또 캐시할 필요가 없다.
    void RebuildShopSlots(ILaneShop shop)
    {
        for (int i = 0; i < UnitCommandResultSlotOrder.Length; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            shopLogicalSlotIndex[slot] = -1;
            unitCommandSlotNames[slot].text = "";
            unitCommandSlotBackgrounds[slot].color = Color.clear;
        }

        // 0~2·12번(공격/정지/모으기/정렬)은 상점 칸이 아니다 — 기본값 0이 "논리 슬롯 0"으로
        // 읽히지 않게 여기서도 -1로 씻어둔다. 안 씻으면 그 칸에 마우스를 올렸을 때
        // 상점의 0번 슬롯 툴팁이 엉뚱하게 뜬다.
        shopLogicalSlotIndex[0] = -1;
        shopLogicalSlotIndex[HoldCommandSlot] = -1;
        shopLogicalSlotIndex[GatherCommandSlot] = -1;
        shopLogicalSlotIndex[AlignCommandSlot] = -1;

        SetUnitOnlyCommandsVisible(shop == null);

        if (shop == null) return;

        int shown = Mathf.Min(shop.SlotCount, UnitCommandResultSlotOrder.Length);

        for (int i = 0; i < shown; i++)
        {
            shopLogicalSlotIndex[UnitCommandResultSlotOrder[i]] = i;
        }

        RefreshShopAffordability();
    }

    // 0.4초 주기로 다시 불린다. 라벨/색이 안 바뀌었으면(GetSlotView가 캐시해서 돌려주는 값이라
    // 대부분 그렇다) 매번 새로 대입해도 실제로 값이 같으면 Text/Image가 리빌드를 다시 안 한다 —
    // label이 null/빈 문자열이면 이 칸은 빈 칸이다(도박소가 줄 맞추려고 끼워 넣는 Empty 등).
    void RefreshShopAffordability()
    {
        if (currentShop as Object == null) return;

        for (int i = 0; i < UnitCommandResultSlotOrder.Length; i++)
        {
            int slot = UnitCommandResultSlotOrder[i];
            int logicalIndex = shopLogicalSlotIndex[slot];
            if (logicalIndex < 0) continue;

            LaneShopSlotView view = currentShop.GetSlotView(logicalIndex);

            if (string.IsNullOrEmpty(view.label))
            {
                unitCommandSlotNames[slot].text = "";
                unitCommandSlotBackgrounds[slot].color = Color.clear;
                continue;
            }

            unitCommandSlotNames[slot].text = view.label;

            Color color = view.color;
            color.a = view.available ? color.a : 0.35f;
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

        int totalEnemies = EnemyDummy.Active.Count;

        // 데스카운트는 이제 레인마다 따로 돌아서 대표할 수 있는 전역 숫자가 하나가 아니다
        // (2026-09-03) — 그 값을 헤더에서 뺐다. 그대로 두면 레인 하나의 값만 보이는데
        // 다른 레인 것처럼 읽혀서, 안 보여주는 것보다 더 나쁜 잘못된 정보가 된다.
        bool changed = !teamPanelInitialized || totalEnemies != lastTotalEnemyCount;

        for (int i = 0; i < TeamSlotCount; i++)
        {
            PlayerContext context = PlayerContext.GetOccupied(i);
            slotHas[i] = context != null;
            slotDead[i] = context != null && context.IsDead;
            slotEnemy[i] = context != null ? EnemyDummy.CountInLane(context.PlayerId) : 0;
            slotGold[i] = context != null && context.GoldWallet != null ? context.GoldWallet.Gold : 0;
            slotWood[i] = context != null && context.ResourceWallet != null ? context.ResourceWallet.Get(ResourceType.Wood) : 0;

            if (slotHas[i] != lastSlotHasContext[i]
                || slotDead[i] != lastSlotDead[i]
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

        teamPanelBuilder.Clear();
        teamPanelBuilder.Append("유닛 카운트 ").Append(totalEnemies);

        for (int i = 0; i < TeamSlotCount; i++)
        {
            lastSlotHasContext[i] = slotHas[i];
            lastSlotDead[i] = slotDead[i];
            lastSlotEnemyCount[i] = slotEnemy[i];
            lastSlotGold[i] = slotGold[i];
            lastSlotWood[i] = slotWood[i];

            teamPanelBuilder.Append('\n');

            bool isLocal = slotHas[i] && i == LocalPlayer.LocalPlayerId;
            if (isLocal) teamPanelBuilder.Append("<b><color=#FFD54A>");

            teamPanelBuilder.Append("플레이어 ").Append(i + 1);
            if (slotDead[i])
            {
                teamPanelBuilder.Append(" | 사망");
            }
            else if (slotHas[i])
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

        // 진행 중인 스토리 이름은 안 띄운다 — 상단 바가 이미 붐빈다.
        // 남은 시간은 대기 시간이라 필요하다.
        storyText.text = running
            ? ""
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
