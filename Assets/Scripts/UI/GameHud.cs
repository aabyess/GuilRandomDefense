using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

// 화면 하단 상시 HUD (원랜디/워크래프트3 스타일). 좌: 미니맵 자리, 중앙: 선택 유닛 정보, 우: 명령 카드 그리드 자리.
// 프리팹 루트에 이 스크립트 하나만 붙여두면 Awake에서 전체 uGUI 계층을 스스로 구성한다
// (에디터 없이 만든 프리팹이 손으로 짠 UI 계층 때문에 깨지는 걸 피하기 위한 구조).
public class GameHud : MonoBehaviour
{
    // 12칸(공격/정지/모으기 + 조합·상점 9칸) 뒤에 정렬(C) 한 칸을 더 붙여 13칸.
    // 조합·상점 9칸(UnitCommandResultSlotOrder)은 도박소가 정확히 9칸을 쓰므로 하나도 못 줄인다.
    // ── HUD 색·간격 규칙 (2026-09-23, 사장님 "하단이 UI적으로 너무 안 이쁜데") ──
    // 값이 자리마다 제각각이던 걸 **세 단계**로 통일한다: 판 → 칸 → 버튼 순으로 밝아진다.
    // 하단 바는 **불투명**이다. 반투명(옛 0.75)이면 나무 바닥·건물이 글자 뒤로 비쳐 안 읽힌다 —
    // 원작(워크3)도 하단은 비치지 않는 판이다.
    static readonly Color PanelColor = new Color(0.09f, 0.10f, 0.13f, 1f);   // 하단 바
    static readonly Color SlotColor = new Color(0.16f, 0.18f, 0.23f, 1f);    // 칸(미니맵·정보·명령)
    static readonly Color ButtonColor = new Color(0.25f, 0.28f, 0.35f, 1f);  // 버튼
    static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.22f);        // 칸 테두리
    // 3D 화면과 하단 바를 가르는 선. 판보다 확실히 밝아야 경계로 읽힌다.
    static readonly Color BarEdgeColor = new Color(0.62f, 0.70f, 0.88f, 1f);
    const float BorderThickness = 1.5f;
    const float BarEdgeThickness = 3f;

    // 명령 격자: **4열**이라 공격·정지·모으기·정렬 넷이 첫 줄에 정확히 들어간다
    // (예전엔 3열 13칸이라 정렬만 다섯째 줄에 혼자 떨어져 있었다 — 사장님 지적).
    // 칸은 정사각 38 — 패널 높이(캔버스 기준 약 214)에 4줄(38×4 + 6×3 = 170)이 들어가는 크기다.
    const int CommandSlotCount = 16;

    // 하단 바 높이(화면 비율)와 미니맵 칸 윗변(화면 비율). 미니맵만 하단 바 위로 솟는다(BuildUI의 MinimapPanel 주석).
    // MinimapTop 고르는 법 — 1920×1080 기준:
    //   0.43 (가) 약 442×442 정사각 · 맵 배율 약 2배 · 왼쪽 아래 3D 화면을 약 442×230px 더 가린다
    //   0.32 (나) 약 442×330 · 배율 약 1.5배 · 가림 절반
    //   0.22 (라) 하단 바 안(옛 모양, 약 442×214)  ← 지금
    //
    // 🔴 2026-09-24 사장님 「미니맵 크기가 너무 큰데?」 → (가) 0.43에서 (라) 0.22로 되돌렸다.
    //    (가)는 구현담당2가 「화면 가림이 늘어나니 사장님 확인이 필요하면 받으라」고 경고한 안인데
    //    PM이 **안 여쭙고 승인**했다. 팀원이 보낸 스크린샷만 보고 「통과」라고 했지, 3D 화면을
    //    얼마나 가리는지는 실제 플레이 화면에서 판단하지 않았다.
    //    교훈: **화면을 덮는 변경은 만든 사람 말고 보는 사람이 판정한다.**
    // 사장님이 「너무 작다」고 하시면 이 숫자 하나만 올린다. 미니맵 그림은 칸 비율을 따라가므로(MinimapCamera) 다른 곳은 안 고친다.
    const float BottomBarHeight = 0.22f;
    const float MinimapTop = 0.22f;
    const int CommandColumns = 4;
    const int TeamSlotCount = 4;
    const int MaxSelectionCards = 12;
    const int SelectionCardColumns = 4;   // 유닛 정보 칸이 좁아져 6열은 넘친다 (12칸 = 4열 3행)
    const int MaxInventoryEntries = 16;
    const int MaxItemInventorySlots = 8;

    [SerializeField] SelectionManager selectionManager;

    TMP_Text unitInfoText;
    Image unitInfoPortrait;
    TMP_Text unitInfoPortraitInitial;
    GameObject unitInfoPortraitSlotObject;
    TMP_Text goldWoodText;

    // 미니맵 위 위습 칸 — 왜 이 모양인지는 BuildWispSlots 주석에 있다.
    // 52px인 이유: 44px로 처음 찍었더니 「랜덤유닛」이 칸을 넘쳐 **화면 왼쪽 끝에서 잘렸다**
    // (1920×1080 플레이 캡처). 자동 축소는 최소 글자 크기까지만 줄어들지, 칸에 맞춰 잘라 주지 않는다.
    const float WispSlotSize = 52f;      // 정사각형 한 변(픽셀). 가로·세로에 같은 값을 넣는 것이 정사각형의 근거다
    const float WispSlotGap = 3f;
    const int WispSlotsPerRow = 9;       // Assets/Data/Wisps 종류 수(9)와 같다 — 오늘은 늘 한 줄이다
    const int MaxWispSlots = 18;         // 종류가 늘면 위로 한 줄 더. 그보다 늘면 경고를 찍는다
    readonly List<WispSlot> wispSlots = new List<WispSlot>();
    readonly Dictionary<WispData, List<Wisp>> wispsByType = new Dictionary<WispData, List<Wisp>>();
    readonly List<WispData> wispTypeOrder = new List<WispData>();
    readonly Dictionary<WispData, int> wispCycle = new Dictionary<WispData, int>();
    RtsCameraController wispCamera;
    bool warnedWispOverflow;
    float nextWispCountTime;

    /// <summary>위습 칸 하나. 가리키는 종류는 위습이 늘고 줄 때마다 다시 배정된다.</summary>
    class WispSlot
    {
        public GameObject root;
        public Image background;
        public TMP_Text nameText;
        public TMP_Text countText;
        public WispData data;
    }
    TMP_Text roundTimeText;
    TMP_Text teamPanelText;
    RectTransform rightColumn;   // 팀 패널 + 보유 아이템을 위에서부터 쌓는 오른쪽 열(RightColumn())
    TMP_Text storyText;
    TMP_Text inventoryText;
    GameObject inventoryPanelObject;

    GameObject unitCardsPanel;
    readonly GameObject[] cardRoots = new GameObject[MaxSelectionCards];
    readonly Image[] cardBackgrounds = new Image[MaxSelectionCards];
    readonly TMP_Text[] cardNames = new TMP_Text[MaxSelectionCards];
    readonly TMP_Text[] cardOverflowTexts = new TMP_Text[MaxSelectionCards];
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
    TMP_Text traitButtonText;
    Button traitButtonComponent;
    UnitTraitData lastTraitButtonTrait;
    bool lastTraitButtonUnlocked;
    int lastTraitButtonPoints = int.MinValue;
    int lastTraitButtonRepeatCount = int.MinValue;

    // 로빈(H098) 전용 대상 지정 대기 상태 — RefreshShopTargeting(pendingShop류)과 같은
    // 모양이지만 ILaneShop이 아니라서 별도로 둔다(WorldPick.TryHit은 그대로 재사용).
    // pendingTraitTarget이 null이면 대기 중이 아니다.
    UnitTraitData pendingTraitTarget;
    PlayerContext pendingTraitContext;
    int pendingTraitTargetingStartFrame;

    // 05번 「고대의 배」도박 능력 버튼들(2026-09-06 목록화) — 특성강화 버튼과 같은 이유로
    // 같은 열(선택 시 뜨는 전용 버튼)에 둔다. UnitData.gambleOptions 항목 수만큼 뜬다.
    // 슬롯 3개를 미리 만들어두고 항목 수만큼만 켠다 — 지금 알려진 목록 최대 크기(h05Y의
    // A023·A0OD·A0OC)에 맞춘 것이지 "나중에 늘 것 같아서" 여유를 둔 게 아니다
    // (RewardDistributor.startingSpecialUnit과 같은 원칙).
    const int GambleButtonSlotCount = 3;
    readonly GameObject[] gambleButtonPanels = new GameObject[GambleButtonSlotCount];
    readonly TMP_Text[] gambleButtonTexts = new TMP_Text[GambleButtonSlotCount];
    readonly Button[] gambleButtonComponents = new Button[GambleButtonSlotCount];
    readonly int[] lastGambleButtonWood = new int[GambleButtonSlotCount];

    // "유닛 판매" 버튼(2026-09-06, PM 지시) — 같은 이유로 같은 열, 고대의 배 버튼 바로
    // 아래. UnitData.sellRewardWisp/sellRewardTraitPoints 둘 다 비어있지 않을 때만 뜬다
    // (트레잇·고대의배 버튼과 같은 관례 — "보상이 없으면 버튼 자체가 없다").
    GameObject sellButtonPanel;
    TMP_Text sellButtonText;
    Button sellButtonComponent;
    UnitData lastSellButtonUnit;

    // "희귀함 리롤"(A0VX, UNIQUE_REROLE_AND_SELL_FAMILY.md) 버튼(2026-09-07) — 위 세 버튼과
    // 달리 UnitData 자산 필드가 아니라 런타임 컴포넌트(UniqueRerollAbility, 도박 성공 스폰
    // 시점에 그 인스턴스에만 붙는다)의 유무로 뜬다 — 대상이 "가챠에서 나온 그 어떤 유닛"이라
    // 특정 자산에 고정할 수 없다(GamblingShop.TryRollUnit 참고). 05번 열이 0.60~0.95를 이미
    // 다 채워서, 그 바로 아래 빈 자리(0.54~0.59)에 둔다.
    GameObject rerollButtonPanel;
    TMP_Text rerollButtonText;
    Button rerollButtonComponent;
    UniqueRerollAbility lastRerollButtonAbility;

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
        "등급 특수함 이상 유닛 로스터 편입 시 랜덤위습 +1\n(원작 포인트값 100 초과 근사, RewardDistributor.unionWisp 씬 배선 필요)",
        "「다른세계 도박」 실패 시 행운의 토큰 +1 (1개→2개)",
        "해루석 피해 250만→300만·마나 700→600\n버스터콜 재사용 100초→66초·마나 500→333",
        "아이템 도박 확률 풀이 13종으로 축소",
    };

    GameObject navigationButtonPanel;
    RectTransform topBarButtons;   // 상단 바 오른쪽 버튼 줄(메뉴·동맹·대화) — 항법 버튼도 여기 선다
    TMP_Text navigationButtonText;
    bool navigationButtonTextInitialized;
    bool lastNavigationHasChosen;
    NavigationChoice lastNavigationChoice = NavigationChoice.None;

    GameObject navigationModalPanel;
    readonly TMP_Text[] navigationRowTexts = new TMP_Text[5];
    readonly Button[] navigationRowButtons = new Button[5];
    readonly TMP_Text[] navigationRowButtonLabels = new TMP_Text[5];

    // 조합 카드(레시피) 12칸. 유닛 카드와 같은 패턴 — 미리 만들어두고 내용만 바꾼다.
    const float RecipeRefreshInterval = 0.4f;
    static readonly List<CombineRecipe> EmptyRecipes = new List<CombineRecipe>();

    // 조합 카드/도움소 스킬 칸 위에 뜨는 툴팁 — 종류가 둘이어도 오브젝트는 하나만 공유해서
    // 위치·내용만 바꾼다(두 번째를 만들면 캔버스 리빌드가 늘어난다).
    readonly StringBuilder tooltipBuilder = new StringBuilder(256);
    GameObject combineTooltipObject;
    TMP_Text combineTooltipText;

    // 호버 중인 칸의 툴팁은 마우스가 그 위에 머무는 동안 주기적으로 다시 그린다 —
    // 도움소 스킬의 "재사용까지 N초"처럼 시간이 지나면 바뀌는 값이 있어서다.
    const float TooltipRefreshInterval = 0.2f;
    int hoveredCommandSlotIndex = -1;
    float nextTooltipRefreshTime;

    // 유닛 명령 그리드(13칸). 0~2번은 공격/정지/모으기, 12번은 정렬(C) 고정 placeholder라 절대 안 건드림.
    // 나머지 칸(3~11) 중 맨 아랫줄부터 왼쪽→오른쪽, 넘치면 그 윗줄로 이어지는 순서로 "선택한 유닛이
    // 무엇이 되는가"(조합 결과)를 채운다. 한 유닛이 최대 4개 레시피의 첫 재료라 이 정도면 충분하다.
    // 조합·상점 9칸을 4열 격자의 **아래 줄부터** 채운다. 12칸(4~15) 중 9칸만 쓰므로
    // 남는 셋(4·5·6)은 둘째 줄 왼쪽에 빈칸으로 남는다 — 빈칸이 위쪽 한 곳에 모여야 격자가
    // 덜 어수선하다.
    static readonly int[] UnitCommandResultSlotOrder = { 12, 13, 14, 15, 8, 9, 10, 11, 7 };

    readonly GameObject[] unitCommandSlotRoots = new GameObject[CommandSlotCount];
    readonly Image[] unitCommandSlotBackgrounds = new Image[CommandSlotCount];
    readonly TMP_Text[] unitCommandSlotNames = new TMP_Text[CommandSlotCount];
    readonly Button[] unitCommandSlotButtons = new Button[CommandSlotCount];
    readonly TMP_Text[] unitCommandSlotHotkeys = new TMP_Text[CommandSlotCount];
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
    bool lastPreparing;

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

    // 아이템 인벤토리 표시(2026-09-09, PM 지시) — 도박에 당첨돼도 화면에 아무것도 안 뜨는
    // 문제(토스트·로그·사운드 0건) 대응. 위 UnitInventory 패널과 같은 결(dirty 플래그 +
    // OnInventoryChanged 구독, 매 프레임 폴링 안 함)이지만 종류별 개수 목록 한 줄이 아니라
    // 항목별로 호버 툴팁(tooltipText)이 필요해 슬롯을 여러 개 만든다(BuildSelectionCards의
    // 고정 풀 관례와 같다 — 최대 개수만 미리 만들어두고 안 쓰는 칸은 숨긴다).
    GameObject itemInventoryTitleObject;
    readonly GameObject[] itemInventoryRowRoots = new GameObject[MaxItemInventorySlots];
    readonly TMP_Text[] itemInventoryRowTexts = new TMP_Text[MaxItemInventorySlots];
    readonly ItemData[] itemInventoryRowItems = new ItemData[MaxItemInventorySlots];
    readonly Dictionary<ItemData, int> itemInventoryCounts = new Dictionary<ItemData, int>();
    readonly List<ItemData> itemInventoryKeys = new List<ItemData>();
    ItemInventory subscribedItemInventory;
    bool itemInventoryDirty = true;

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

    // h0BS(메타몽) 판매→아이템 도박 결과를 받는다.
    //
    // 🔴 2026-09-09 정정 — 여기가 씬 전역 1개를 잡고 있었다. 씬의 ItemInventory는 플레이어 0
    //    슬롯에 하나뿐이라, **누가 도박에 이겨도 아이템이 전부 플레이어 0에게** 들어갔다
    //    (형제인 ItemGambleState는 4개인데 받는 그릇만 하나였다). 원작은 ItemGet이
    //    `UnitAddItem(v, ...)`으로 그 유닛에게 직접 넣고 획득 메시지도 그 소유자에게만 띄운다.
    //    이제 PlayerContext.ItemInventory(플레이어별)를 먼저 본다.
    //
    // ⚠️ 전역 조회를 남겨 둔 이유: 플레이어별 컴포넌트는 MapGenerator.RepairPlayerParts가
    //    붙인다. 맵을 아직 안 돌린 씬에서는 1~3번 슬롯이 비어 있어, 그대로 두면 아이템이
    //    조용히 사라진다. 배선 전에는 예전과 같이 동작하게 두고(회귀 없음), 맵 생성을
    //    한 번 돌리면 자동으로 플레이어별로 갈린다.
    ItemInventory itemInventory;
    ItemInventory ItemInventoryRef => itemInventory != null
        ? itemInventory
        : itemInventory = FindFirstObjectByType<ItemInventory>();

    ItemInventory InventoryOf(PlayerContext context)
    {
        if (context != null && context.ItemInventory != null) return context.ItemInventory;
        return ItemInventoryRef;
    }

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
        RefreshItemInventoryPanel();
        RefreshShopTargeting();
        RefreshTraitTargeting();
        RefreshHoveredTooltip();
        RefreshTraitButton();
        RefreshGambleButtons();
        RefreshSellButton();
        RefreshNavigationButton();
        RefreshRerollButton();
    }

    void OnDestroy()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnInventoryChanged -= OnLocalInventoryChanged;
        }

        if (subscribedItemInventory != null)
        {
            subscribedItemInventory.OnInventoryChanged -= OnItemInventoryChanged;
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

        RectTransform bar = CreatePanel(transform, "BottomBar", PanelColor);
        SetAnchors(bar, new Vector2(0f, 0f), new Vector2(1f, BottomBarHeight));

        // 3D 화면과 갈리는 경계선. 판이 불투명해도 위쪽 경계가 밋밋하면 화면에 얹힌 게 아니라
        // 잘린 것처럼 보인다 — 밝은 선 한 줄이 "여기부터 UI"를 읽히게 한다.
        CreateBorderStrip(bar, BarEdgeColor, new Vector2(0f, 1f), new Vector2(1f, 1f),
                          new Vector2(0f, -BarEdgeThickness), Vector2.zero);

        // 원작 배치: [미니맵] [조합 카드] [선택 유닛 정보] [유닛 명령(공격/정지/모으기/스킬)]
        // 미니맵 폭은 0.14 → 0.23으로 넓혔다(사장님 지시 2026-09-07: "미니맵이 가로로 너무 좁다").
        // 남는 폭은 오른쪽 명령 카드 그리드에서 뺐다 — 그쪽은 3열(90px + 간격 6px = 282px)만
        // 필요한데 634px을 쓰고 있어서 절반 이상이 빈 공간이었다.
        // 패널 배경 알파를 0.05~0.08로 두면 3D 화면 위에서 사실상 안 보여서, 선택 정보·명령
        // 버튼이 "배경 없이 떠 있는" 것처럼 보였다(사장님 스크린샷, 2026-09-23). 세 칸 다
        // BottomBar 자체(검정 0.75)보다는 옅되 눈에 들어오는 값으로 올린다.
        // 🔴 2026-09-23 미니맵 칸만 하단 바 위로 솟게 했다(PM 확정 (가)). 맵이 세로로 길어(3175×3715) 칸 **높이**가 배율을
        //    정하는데, 칸이 하단 바 높이(약 214px)에 갇혀 있어 맵이 작고 좌우가 비었다. 폭 0.23은 사장님 09-07 지시
        //    (「미니맵이 가로로 너무 좁다」)라 그대로 두고 **높이만** 키운다 — WC3 미니맵 틀도 콘솔 위로 솟아 있다.
        //    그래서 부모를 하단 바가 아니라 HUD 루트로 두고 화면 비율로 잡는다(아래변·좌우는 하단 바 안 자리와 같다).
        //    초상화·정보 칸·명령 격자는 하단 바 안이라 안 움직인다.
        RectTransform minimapPanel = CreatePanel(transform, "MinimapPanel", SlotColor);
        SetAnchors(minimapPanel, new Vector2(0.01f, BottomBarHeight * 0.05f), new Vector2(0.24f, MinimapTop));
        BuildMinimap(minimapPanel);
        AddPanelBorder(minimapPanel, BorderColor, BorderThickness);

        // 🔴 미니맵 바로 위 위습 개수 (사장님 지시 2026-09-24: 「랜덤위습도 시간지나서 추가되면
        //    미니맵 위쪽에 위습 몇개 있는지 뜨게 해주고 원랜디처럼」).
        //    원작도 미니맵 위에 내 위습 수가 붙어 있다. 라운드 보상으로 위습이 늘어나는데
        //    지금은 맵을 훑어 세야만 알 수 있었다.
        //    ⚠️ 하단 바가 아니라 HUD 루트에 붙인다 — 미니맵 칸 높이(MinimapTop)를 바꿔도 따라오게
        //    아래변을 MinimapTop에 맞춘다. 숫자를 박으면 오늘처럼 미니맵을 옮길 때 어긋난다.
        BuildWispSlots();

        RectTransform infoPanel = CreatePanel(bar, "UnitInfoPanel", SlotColor);
        SetAnchors(infoPanel, new Vector2(0.25f, 0.05f), new Vector2(0.81f, 0.95f));

        RectTransform portraitSlot = CreatePanel(infoPanel, "UnitInfoPortraitSlot", new Color(1f, 1f, 1f, 0.08f));
        SetAnchors(portraitSlot, new Vector2(0f, 0f), new Vector2(0.22f, 1f));
        AddPanelBorder(portraitSlot, BorderColor, BorderThickness);
        unitInfoPortraitSlotObject = portraitSlot.gameObject;

        GameObject portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObj.transform.SetParent(portraitSlot, false);
        RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.08f, 0.08f);
        portraitRect.anchorMax = new Vector2(0.92f, 0.92f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;
        unitInfoPortrait = portraitObj.GetComponent<Image>();
        unitInfoPortrait.sprite = null;
        unitInfoPortrait.color = SlotColor;
        unitInfoPortrait.raycastTarget = false;

        // 초상화 아트가 아직 없다. 빈 사각형으로 두면 "미완성"으로 보인다는 지적(사장님
        // 2026-09-23)에 따라, 등급색 바탕 + 유닛 이름 첫 글자로 채워 최소한 "누구인지"가
        // 읽히게 한다. 프리팹을 RenderTexture로 찍어 진짜 초상을 만드는 안은 PM 검토 중이다.
        unitInfoPortraitInitial = CreateLabel(portraitObj.transform, "Initial", "");
        unitInfoPortraitInitial.fontSize = 44;
        unitInfoPortraitInitial.fontStyle = FontStyles.Bold;
        unitInfoPortraitInitial.raycastTarget = false;

        RectTransform infoTextSlot = CreatePanel(infoPanel, "UnitInfoTextSlot", Color.clear);
        SetAnchors(infoTextSlot, new Vector2(0.24f, 0f), new Vector2(1f, 1f));
        unitInfoText = CreateLabel(infoTextSlot, "UnitInfoText", "선택된 유닛 없음");
        unitInfoText.alignment = TextAlignmentOptions.TopLeft;
        unitInfoText.fontSize = 26;
        unitInfoText.lineSpacing = 1.15f;
        unitInfoText.textWrappingMode = TextWrappingModes.Normal;

        BuildSelectionCards(infoPanel);

        RectTransform commandPanel = CreatePanel(bar, "UnitCommandPanel", SlotColor);
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
        BuildRerollButton();
        BuildItemInventoryPanel();
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
        inventoryText.alignment = TextAlignmentOptions.TopLeft;
        inventoryText.fontSize = 18;
        inventoryText.lineSpacing = 1.1f;
        inventoryText.textWrappingMode = TextWrappingModes.Normal;
        inventoryText.overflowMode = TextOverflowModes.Overflow;
    }

    // 상단 바가 이미 골드·목재·라운드·타이머로 차 있어 그 아래 별도 줄로 뺀다.
    // 팀 현황판(우측 상단)과 겹치지 않게 좌측 절반만 쓴다.
    void BuildStoryPanel()
    {
        RectTransform storyPanel = CreatePanel(transform, "StoryPanel", Color.clear);
        SetAnchors(storyPanel, new Vector2(0.01f, 0.90f), new Vector2(0.5f, 0.95f));

        storyText = CreateLabel(storyPanel, "StoryText", "");
        storyText.alignment = TextAlignmentOptions.Left;
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
        goldWoodText.alignment = TextAlignmentOptions.Left;
        goldWoodText.fontSize = 22;

        RectTransform roundPanel = CreatePanel(topBar, "RoundPanel", Color.clear);
        SetAnchors(roundPanel, new Vector2(0.36f, 0f), new Vector2(0.64f, 1f));
        roundTimeText = CreateLabel(roundPanel, "RoundTimeText", "라운드 -   남은시간 -");
        roundTimeText.fontSize = 22;

        RectTransform menuButtonsPanel = CreatePanel(topBar, "TopBarButtons", Color.clear);
        topBarButtons = menuButtonsPanel;
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

    static TMP_Text CreateTopBarButton(Transform parent, string name, string label, float width = 90f)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        obj.GetComponent<LayoutElement>().preferredWidth = width;

        TMP_Text text = CreateLabel(obj.transform, name + "Label", label);
        text.fontSize = 18;
        text.raycastTarget = false;
        return text;
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
            // 로빈(H098) 전용 — 눌러도 즉시 안 사지고 대상 지정 모드로 들어간다는 걸
            // 미리 알린다(RefreshTraitTargeting 참고).
            string suffix = trait.targetsOtherUnit ? " — 클릭 후 대상 지정" : "";
            traitButtonText.text = $"특성강화{suffix}\n({trait.costTraitPoints}pt, 보유 {points}pt)";
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

            TMP_Text label = CreateLabel(panel, $"GambleButtonText{i}", "");
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

        // MP: 멀티 클라는 호스트에 요청만 보낸다(목재·확률·소모·소환은 호스트가). 싱글·호스트는 아래 본체 그대로.
        if (!GameAuthority.IsServer) { NetCommands.RequestHudUnitAction(NetHudAction.Gamble, single, index); return; }

        ExecuteGambleOn(single, index);
    }

    // MP: 버튼(위)과 멀티 호스트가 받은 클라 요청(NetCommands)이 같이 쓰는 본체 — 줄 내용은 그대로다.
    public void ExecuteGambleOn(Selectable single, int index)
    {
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
             identity.Data.sellRewardWood <= 0 && identity.Data.sellTriggersItemGamblePool == null &&
             identity.Data.sellRewardEveryNSells <= 0))
        { HideSellButton(); return; }

        sellButtonPanel.SetActive(true);

        if (identity.Data == lastSellButtonUnit) return;
        lastSellButtonUnit = identity.Data;

        UnitData data = identity.Data;
        string wispPart = data.sellRewardWisp != null
            ? data.sellRewardWispChance >= 1f
                ? $"{data.sellRewardWisp.wispName} {data.sellRewardWispCount}기"
                : $"{data.sellRewardWisp.wispName} {data.sellRewardWispCount}기({data.sellRewardWispChance:P0})"
            : null;
        string woodPart = data.sellRewardWood > 0
            ? data.sellRewardWoodChance >= 1f
                ? $"목재 {data.sellRewardWood}"
                : $"목재 {data.sellRewardWood}({data.sellRewardWoodChance:P0})"
            : null;
        string pointPart = data.sellRewardTraitPoints > 0
            ? $"특성포인트 {data.sellRewardTraitPoints}"
            : null;
        string gamblePart = data.sellTriggersItemGamblePool != null
            ? "아이템 도박 1회"
            : null;
        string everyNPart = data.sellRewardEveryNSells > 0 && data.sellRewardEveryNWisp != null
            ? $"{data.sellRewardEveryNSells}회 판매마다 {data.sellRewardEveryNWisp.wispName} 1기(누적형, 플레이어 공유)"
            : null;

        StringBuilder rewardDesc = new StringBuilder();
        foreach (string part in new[] { wispPart, woodPart, pointPart, gamblePart, everyNPart })
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

        // MP: 멀티 클라는 호스트에 요청만 보낸다. 싱글·호스트는 아래 본체 그대로.
        if (!GameAuthority.IsServer) { NetCommands.RequestHudUnitAction(NetHudAction.Sell, single, 0); return; }

        ExecuteSellOn(single);
    }

    // MP: 버튼(위)과 멀티 호스트가 받은 클라 요청(NetCommands)이 같이 쓰는 본체 — 줄 내용은 그대로다.
    public void ExecuteSellOn(Selectable single)
    {
        if (single == null || !single.TryGetComponent(out UnitIdentity identity) || identity.Data == null ||
            (identity.Data.sellRewardWisp == null && identity.Data.sellRewardTraitPoints <= 0 &&
             identity.Data.sellRewardWood <= 0 && identity.Data.sellTriggersItemGamblePool == null &&
             identity.Data.sellRewardEveryNSells <= 0)) return;

        if (!single.TryGetComponent(out OwnedByPlayer owner)) return;

        PlayerContext context = PlayerContext.Get(owner.OwnerId);
        if (context != null)
        {
            if (identity.Data.sellRewardTraitPoints > 0)
                context.UnitUpgrades?.AddTraitPoints(identity.Data.sellRewardTraitPoints);

            // 등급별 판매보상 4단계(UNIQUE_SELL_6TIER_FULL.md) — 위습·목재 각각 독립
            // 확률(둘 다 기본값 1=확정이라 기존 h05X류는 항상 나가던 대로 그대로 나간다).
            if (identity.Data.sellRewardWisp != null && RewardDistributor.Instance != null &&
                Random.value < identity.Data.sellRewardWispChance)
            {
                List<WispReward> reward = new List<WispReward>
                {
                    new WispReward { wisp = identity.Data.sellRewardWisp, count = identity.Data.sellRewardWispCount }
                };
                RewardDistributor.Instance.GrantWisps(context, reward);
            }

            if (identity.Data.sellRewardWood > 0 && context.ResourceWallet != null &&
                Random.value < identity.Data.sellRewardWoodChance)
            {
                context.ResourceWallet.Add(ResourceType.Wood, identity.Data.sellRewardWood);
            }

            // 흔함 9종 판매 누적(A09G) — 플레이어 전체 공유 카운터가 N번째에 도달할 때만
            // 위습을 준다(UnitUpgrades.RegisterCommonSell 참고). RewardDistributor.
            // GrantWisps는 count를 1로 고정 — 이 보상은 항상 위습 1기다.
            if (identity.Data.sellRewardEveryNSells > 0 && identity.Data.sellRewardEveryNWisp != null &&
                RewardDistributor.Instance != null &&
                (context.UnitUpgrades?.RegisterCommonSell(identity.Data.sellRewardEveryNSells) ?? false))
            {
                List<WispReward> reward = new List<WispReward>
                {
                    new WispReward { wisp = identity.Data.sellRewardEveryNWisp, count = 1 }
                };
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
                InventoryOf(context)?.Add(wonItem);

                // 원작 ItemGet: DisplayTextToPlayer(GetOwningPlayer(v), ..., "아이템 : "+GetItemName(it)+"획득!")
                // — 그 소유자에게만 띄운다. 당첨이 조용해 플레이어가 뭘 얻었는지 몰랐다(PM 지시 2026-09-09).
                PlayerNotification.Show(owner.OwnerId, $"아이템 : {wonItem.itemName} 획득!");
            }
        }

        // 보상 지급 뒤에 소모한다 — Consume이 오브젝트를 파괴하므로 그 전에 owner/보상을
        // 전부 읽어둬야 한다(위에서 이미 다 읽었다).
        identity.Consume();
    }

    // "희귀함 리롤"(A0VX) 버튼 — 위 세 버튼과 같은 열의 빈 자리(0.54~0.59, 05번 열 바로
    // 아래)를 쓴다. 목재가 모자라거나 한도를 소진했어도 항상 눌리게 둔다(고대의 배 버튼과
    // 같은 이유 — "조건 미달"과 "실패"를 원작처럼 다른 문구로만 구분한다, interactable로
    // 미리 막지 않는다).
    void BuildRerollButton()
    {
        RectTransform panel = CreatePanel(transform, "RerollButtonPanel", new Color(1f, 1f, 1f, 0.15f));
        SetAnchors(panel, new Vector2(0.51f, 0.54f), new Vector2(0.70f, 0.59f));

        Button button = panel.gameObject.AddComponent<Button>();
        button.onClick.AddListener(OnRerollButtonClicked);

        rerollButtonText = CreateLabel(panel, "RerollButtonText", "");
        rerollButtonText.fontSize = 16;
        rerollButtonText.raycastTarget = false;

        rerollButtonPanel = panel.gameObject;
        rerollButtonComponent = button;
        rerollButtonPanel.SetActive(false);
    }

    // 단일 선택 + 그 인스턴스에 UniqueRerollAbility가 붙어 있을 때만 뜬다(UnitData 자산이
    // 아니라 런타임 컴포넌트 — GamblingShop.TryRollUnit이 붙인다).
    void RefreshRerollButton()
    {
        if (rerollButtonPanel == null) return;

        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) { HideRerollButton(); return; }

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UniqueRerollAbility reroll) ||
            !single.TryGetComponent(out OwnedByPlayer owner))
        { HideRerollButton(); return; }

        rerollButtonPanel.SetActive(true);

        if (reroll == lastRerollButtonAbility) return;
        lastRerollButtonAbility = reroll;

        UniqueRerollState state = PlayerContext.Get(owner.OwnerId)?.UniqueRerollState;
        int used = state != null ? state.AttemptsUsed : 0;
        int limit = state != null ? state.Limit : 0;
        float failChance = state != null ? state.FailChancePercent : 0f;

        rerollButtonText.text = $"희귀함 리롤\n(목재 {reroll.WoodCost} 소모, 실패확률 {failChance:F0}%, " +
                                 $"남은 횟수 {Mathf.Max(0, limit - used)})";
    }

    void HideRerollButton()
    {
        if (rerollButtonPanel != null && rerollButtonPanel.activeSelf) rerollButtonPanel.SetActive(false);
        lastRerollButtonAbility = null;
    }

    // 원작 Trig_unique_rerole_Actions 대응 — 실패 메시지("목재가 부족합니다!"/"리롤회수를
    // 소진하였습니다."/"리롤을 실패하였습니다.")와 성공 메시지 전부 UniqueRerollAbility.
    // TryCast가 만들어 돌려준다. 실패해도(한도·목재 미달 제외) 유닛은 안 죽으므로 selection이
    // 그대로 유효하다 — Consume은 성공 분기에서만 일어난다.
    void OnRerollButtonClicked()
    {
        if (BlockedOnMultiplayerClient()) return; // MP
        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) return;

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UniqueRerollAbility reroll) ||
            !single.TryGetComponent(out OwnedByPlayer owner)) return;

        reroll.TryCast(out string message);
        if (message != null) PlayerNotification.Show(owner.OwnerId, message);
    }

    // 아이템 인벤토리 패널(2026-09-09, PM 지시) — TeamPanel 바로 아래에 둔다(2026-09-23부터
    // RightColumn 레이아웃이 팀 패널 높이에 맞춰 붙인다). 유닛 선택과 무관하게 항상 보이는 패널이라,
    // 단일 선택 시에만 뜨는 05번 열(0.51~0.70)이 아니라 TeamPanel과 같은 열에
    // 쌓는다. 칸마다 EventTrigger로 호버 시 tooltipText를 띄운다(ShowTooltip/HideCombineTooltip
    // 재사용 — 조합 카드 툴팁과 같은 함수, 새 툴팁 시스템을 만들지 않는다).
    void BuildItemInventoryPanel()
    {
        // 팀 패널 바로 밑에 붙는다 — 위치는 RightColumn의 레이아웃이 정한다(화면 비율로 박지 않는다).
        RectTransform title = CreatePanel(RightColumn(), "ItemInventoryTitlePanel", new Color(0f, 0f, 0f, 0.6f));
        SetLayoutHeight(title, ItemTitleHeight);
        itemInventoryTitleObject = title.gameObject;

        TMP_Text titleText = CreateLabel(title, "ItemInventoryTitleText", "보유 아이템");
        titleText.fontSize = 18;
        titleText.raycastTarget = false;

        for (int i = 0; i < MaxItemInventorySlots; i++)
        {
            RectTransform row = CreatePanel(RightColumn(), $"ItemInventoryRow{i}", new Color(1f, 1f, 1f, 0.15f));
            SetLayoutHeight(row, ItemRowHeight);
            itemInventoryRowRoots[i] = row.gameObject;

            TMP_Text label = CreateLabel(row, $"ItemInventoryRowText{i}", "");
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            itemInventoryRowTexts[i] = label;

            int capturedIndex = i;
            EventTrigger trigger = row.gameObject.AddComponent<EventTrigger>();
            AddTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnItemInventoryRowHoverEnter(capturedIndex));
            AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => HideCombineTooltip());

            row.gameObject.SetActive(false);
        }
    }

    void OnItemInventoryRowHoverEnter(int index)
    {
        if (index < 0 || index >= MaxItemInventorySlots || itemInventoryRowRoots[index] == null) return;

        ItemData item = itemInventoryRowItems[index];
        if (item == null) return;

        string text = !string.IsNullOrEmpty(item.tooltipText) ? item.tooltipText : item.itemName;
        ShowTooltip(text, (RectTransform)itemInventoryRowRoots[index].transform);
    }

    // PlayerContext.Local이 없으면 패널 자체를 숨긴다(UnitInventory 패널과 같은 관례).
    // InventoryOf(local)을 써서 맵 생성 전(플레이어별 컴포넌트 미배선) 폴백까지 그대로 탄다.
    void RefreshItemInventoryPanel()
    {
        if (itemInventoryTitleObject == null) return;

        PlayerContext local = PlayerContext.Local;
        ItemInventory inventory = local != null ? InventoryOf(local) : null;

        if (inventory != subscribedItemInventory)
        {
            if (subscribedItemInventory != null)
            {
                subscribedItemInventory.OnInventoryChanged -= OnItemInventoryChanged;
            }

            subscribedItemInventory = inventory;

            if (subscribedItemInventory != null)
            {
                subscribedItemInventory.OnInventoryChanged += OnItemInventoryChanged;
            }

            itemInventoryDirty = true;
        }

        bool visible = inventory != null;
        if (itemInventoryTitleObject.activeSelf != visible)
        {
            itemInventoryTitleObject.SetActive(visible);
        }

        if (!visible)
        {
            for (int i = 0; i < MaxItemInventorySlots; i++)
            {
                if (itemInventoryRowRoots[i] != null && itemInventoryRowRoots[i].activeSelf)
                {
                    itemInventoryRowRoots[i].SetActive(false);
                }
            }
            return;
        }

        if (!itemInventoryDirty) return;

        itemInventoryDirty = false;
        RebuildItemInventoryRows(inventory);
    }

    void OnItemInventoryChanged()
    {
        itemInventoryDirty = true;
    }

    // 이름순 정렬 + 종류별 "이름 xN". 슬롯보다 종류가 많으면 마지막 칸에 "외 N종 더"를 덧붙인다
    // (RebuildInventoryText의 "외 N종" 관례와 같다).
    void RebuildItemInventoryRows(ItemInventory inventory)
    {
        itemInventoryCounts.Clear();
        foreach (ItemData item in inventory.Items)
        {
            if (item == null) continue;
            itemInventoryCounts.TryGetValue(item, out int count);
            itemInventoryCounts[item] = count + 1;
        }

        itemInventoryKeys.Clear();
        foreach (ItemData item in itemInventoryCounts.Keys)
        {
            itemInventoryKeys.Add(item);
        }
        itemInventoryKeys.Sort((a, b) => string.CompareOrdinal(a.itemName, b.itemName));

        int shown = Mathf.Min(itemInventoryKeys.Count, MaxItemInventorySlots);
        for (int i = 0; i < MaxItemInventorySlots; i++)
        {
            bool used = i < shown;
            if (itemInventoryRowRoots[i].activeSelf != used)
            {
                itemInventoryRowRoots[i].SetActive(used);
            }

            if (!used)
            {
                itemInventoryRowItems[i] = null;
                continue;
            }

            ItemData item = itemInventoryKeys[i];
            itemInventoryRowItems[i] = item;
            itemInventoryRowTexts[i].text = $"{item.itemName} x{itemInventoryCounts[item]}";
        }

        int remaining = itemInventoryKeys.Count - shown;
        if (remaining > 0 && shown > 0)
        {
            itemInventoryRowTexts[shown - 1].text += $" 외 {remaining}종 더";
        }
    }

    // 항법 지속 버튼 — 유닛 선택과 무관하게 항상 보인다. 안 골랐으면 "항법 선택",
    // 골랐으면 "항법: <이름>"으로 바뀐다(되돌릴 수 없다는 걸 상시 노출). 고른 뒤에도 숨기지 않는다 —
    // 되돌릴 수 없는 선택이라 뭘 골랐는지 계속 보여야 한다(PM 확정 2026-09-23).
    // 자리: 상단 바 메뉴·동맹·대화 줄의 맨 앞(PM 확정 2026-09-23). 예전엔 화면 가운데 위(0.51~0.70 × 0.84~0.89)에
    // 테두리 없는 반투명 판으로 떠 있어 「내용 없는 미완성 박스」로 읽혔다(1920×1080 실측).
    // 글자가 「항법: ④ 도움소 강화」까지 길어지므로 다른 버튼(90)보다 넓게 두고, 넘치면 글자를 줄인다.
    void BuildNavigationUI()
    {
        navigationButtonText = CreateTopBarButton(topBarButtons, "NavigationButton", "항법 선택", 210f);
        navigationButtonText.enableAutoSizing = true;
        navigationButtonText.fontSizeMin = 12f;
        navigationButtonText.fontSizeMax = 18f;
        navigationButtonText.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject panel = navigationButtonText.transform.parent.gameObject;
        panel.transform.SetAsFirstSibling();   // 메뉴·동맹·대화는 원작 자리(오른쪽 끝) 그대로
        panel.GetComponent<Button>().onClick.AddListener(OnNavigationButtonClicked);

        navigationButtonPanel = panel;

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
        TMP_Text title = CreateLabel(titleHolder, "NavigationModalTitle",
            "항법(진행 루트) 선택 — 플레이어당 평생 1회, 되돌릴 수 없습니다");
        title.fontSize = 20;
        title.color = new Color(1f, 0.55f, 0.35f);
        title.raycastTarget = false;

        RectTransform subtitleHolder = NewHolder(modal, "NavigationModalSubtitleHolder", new Vector2(0f, 0.84f), new Vector2(1f, 0.90f));
        TMP_Text subtitle = CreateLabel(subtitleHolder, "NavigationModalSubtitle",
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
            TMP_Text label = CreateLabel(textHolder, "Label", "");
            label.alignment = TextAlignmentOptions.Left;
            label.fontSize = 15;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            RectTransform buttonHolder = CreatePanel(rowPanel, "SelectButton", new Color(1f, 1f, 1f, 0.25f));
            SetAnchors(buttonHolder, new Vector2(0.75f, 0.15f), new Vector2(0.98f, 0.85f));
            Button rowButton = buttonHolder.gameObject.AddComponent<Button>();
            int capturedIndex = i;
            rowButton.onClick.AddListener(() => OnNavigationOptionClicked(capturedIndex));
            TMP_Text buttonLabel = CreateLabel(buttonHolder, "SelectButtonLabel", "선택");
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
        TMP_Text closeLabel = CreateLabel(closeHolder, "NavigationModalCloseLabel", "닫기");
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

        // MP: 멀티 클라는 호스트에 요청만(호스트가 요청자 슬롯의 NavigationState로 고른다 — 여기 Local을 그대로 쓰면
        //     방장 것이 된다). 고른 결과는 NetPlayer.Navigation으로 돌아와 이 창이 갱신된다.
        if (!GameAuthority.IsServer)
        {
            NetCommands.RequestNavigation(NavigationOptionOrder[index]);
            navigationModalPanel.SetActive(false);
            return;
        }

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

        // MP: 멀티 클라 — 대상을 찍는 특성(로빈)은 「대상 클릭 대기」만 여기서(복제된 포인트로 검사) 하고 실행은
        //     RefreshTraitTargeting이 요청으로 보낸다. 그 밖의 특성은 호스트에 요청만. 싱글·호스트는 아래 본체 그대로.
        bool mpTargeted = single != null && single.TryGetComponent(out UnitIdentity mpIdentity)
            && mpIdentity.Data != null && mpIdentity.Data.trait != null && mpIdentity.Data.trait.targetsOtherUnit;
        if (!GameAuthority.IsServer && !mpTargeted) { NetCommands.RequestHudUnitAction(NetHudAction.Trait, single, 0); return; }

        ExecuteTraitOn(single);
    }

    // MP: 버튼(위)과 멀티 호스트가 받은 클라 요청(NetCommands)이 같이 쓰는 본체 — 줄 내용은 그대로다.
    public void ExecuteTraitOn(Selectable single)
    {
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

        // 로빈(H098) 전용 — 대상이 이 유닛(구매자) 자신이 아니라 플레이어가 다음 클릭으로
        // 찍는 다른 유닛이다(원작 GetSpellTargetUnit()). 비용은 대상을 실제로 찍은 뒤에만
        // 나간다(원작도 캐스트가 완료돼야, 즉 대상이 정해져야 비용을 뗀다) — 여기서는
        // 아직 아무것도 소모하지 않고 대상 대기 모드로만 들어간다.
        if (trait.targetsOtherUnit)
        {
            if (upgrades.IsUnlocked(trait)) return;
            if (upgrades.TraitPoints < trait.costTraitPoints)
            {
                PlayerNotification.Show(owner.OwnerId, "특성 포인트가 부족합니다!");
                return;
            }

            pendingTraitTarget = trait;
            pendingTraitContext = context;
            pendingTraitTargetingStartFrame = Time.frameCount;
            PlayerNotification.Show(owner.OwnerId, "대상 유닛을 클릭하세요.");
            return;
        }

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

        // 우솝(G.O.D, H09B) 전용 — 산 사람의 UnitUpgrades(HashSet)가 아니라 게임 전체에
        // 하나뿐인 플래그를 켠다. 도움소(SupportShop)가 이걸 읽어 4명 전원의 "독약" 스킬을
        // 레벨2로 올린다 — targetUnit(=이 유닛) 자신에게는 원작에도 효과가 없다.
        if (trait.triggersUsoppDockhouseBoost)
        {
            UsoppDockhouseTrait.Activate();
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
    // 경험치·STR/AGI/INT를 새 유닛에 되돌린다. 우리 쪽 대응 축은 heroXp/purchasedStat
    // (UnitAttacker) — Consume() 전에 옛 유닛에서 읽어(지운 뒤엔 0만 남는다) 새 유닛에
    // CopyProgressionFrom으로 되돌린다(아래). 그 외 실제로 이어져야 하는 건:
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
    // ⚠️ 특성 구매 이력(unlockedTraits)·공격타입 업그레이드 레벨(UnitUpgrades.
    // LevelForAttackType)·A0LZ 자가강화 레벨(selfUpgradeLevel)도 이월돼야 하는지는 원작
    // 확인 전까지 넣지 않는다(리서치담당 질의 대기, PM 전달).
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

        // ⚠️ Consume() 전에 읽는다 — 지운 뒤에 읽으면 0만 남는다(UnitIdentity.Consume이
        // 이 컴포넌트가 붙은 게임오브젝트를 없앤다).
        oldIdentity.TryGetComponent(out UnitAttacker oldAttacker);

        oldIdentity.Consume();

        GameObject newUnit = spawner.Spawn(trait.transformIntoUnit, spawnPosition, ownerId);
        if (newUnit != null)
        {
            if (oldAttacker != null && newUnit.TryGetComponent(out UnitAttacker newAttacker))
            {
                newAttacker.CopyProgressionFrom(oldAttacker);
            }

            if (newUnit.TryGetComponent(out Selectable newSelectable))
            {
                Selection?.SelectOnly(newSelectable);
            }
        }
    }

    // CombineSystem.ResultSampleRadius와 같은 값·같은 이유 — 자리가 NavMesh 경계에 살짝
    // 걸쳐 있어도 실제로 밟을 수 있는 땅 근처로 붙인다.
    // 맵이 WorldScale.Value배가 됐으므로 이 표본 반경도 같이 커진다(2026-09-23) —
    // 세계 좌표 거리라 유닛 크기가 아니라 맵 배율을 따라간다.
    const float TransformSampleRadius = 4f * WorldScale.Value;

    // 오른쪽 열: 상단 바 바로 밑(0.95)에서 아래로 팀 패널 → 「보유 아이템」 제목 → 아이템 칸을 **내용만큼** 쌓는다.
    // 예전엔 셋이 각자 화면 비율 앵커로 박혀 있어서, 하나를 줄이면 나머지가 제자리에 떠 있었다
    // (팀 패널을 0.86으로 줄이자 0.70~0.86이 비고 「보유 아이템」만 화면 중간에 혼자 남음, 09-23).
    // 꺼진 칸(SetActive false)은 레이아웃 그룹이 건너뛰므로 아이템이 없으면 제목 밑이 바로 끝난다.
    // 열 자체엔 Image가 없어 클릭을 막지 않는다. 아래 끝 0.23은 하단 바(0.22) 바로 위다.
    RectTransform RightColumn()
    {
        if (rightColumn != null) return rightColumn;

        GameObject obj = new GameObject("RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
        obj.transform.SetParent(transform, false);
        rightColumn = (RectTransform)obj.transform;
        SetAnchors(rightColumn, new Vector2(0.71f, 0.23f), new Vector2(0.99f, 0.95f));

        VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = ItemRowGap;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rightColumn;
    }

    // 옛 앵커 값을 캔버스 기준 높이(1080)로 옮긴 것 — 제목 0.05, 칸 0.045, 칸 사이 0.005.
    const float ItemTitleHeight = 54f, ItemRowHeight = 48.6f, ItemRowGap = 5.4f;

    static void SetLayoutHeight(RectTransform rect, float height)
    {
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    void BuildTeamPanel()
    {
        // 원작 멀티보드다 — 원작에 있는 정보라 끄지 않는다([[follow-original-everywhere]]).
        // 다만 내용은 몇 줄인데 상자가 화면 높이의 25%(0.70~0.95)를 먹어서 아래 「보유 아이템」과
        // 겹쳤다(사장님 화면 2026-09-23). **끄지 말고 내용 높이에 맞춘다**(PM 지시).
        // 🔴 높이를 숫자로 박지 않는다. 0.86으로 박았더니 줄이 5줄(유닛 카운트 + 플레이어 4)이라
        //    「플레이어 4」가 상자 밑으로 흘러나왔다(09-23 1920×1080 실측). 줄 수·플레이어 수가 바뀌어도
        //    맞게, 패널의 레이아웃 그룹이 글자의 preferredHeight로 높이를 정한다(RightColumn 참고).
        RectTransform teamPanel = CreatePanel(RightColumn(), "TeamPanel", new Color(0f, 0f, 0f, 0.6f));
        VerticalLayoutGroup fit = teamPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        fit.padding = new RectOffset(8, 8, 4, 6);
        fit.childControlWidth = true;
        fit.childControlHeight = true;
        fit.childForceExpandWidth = true;
        fit.childForceExpandHeight = false;

        teamPanelText = CreateLabel(teamPanel, "TeamPanelText", "");
        teamPanelText.alignment = TextAlignmentOptions.TopLeft;
        teamPanelText.fontSize = 20;
        teamPanelText.lineSpacing = 1.1f;
        teamPanelText.textWrappingMode = TextWrappingModes.NoWrap;
        teamPanelText.overflowMode = TextOverflowModes.Overflow;
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

        TMP_Text nameText = CreateLabel(card.transform, "Name", "");
        nameText.raycastTarget = false;
        nameText.fontSize = 11;
        nameText.alignment = TextAlignmentOptions.Top;
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0.35f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;

        TMP_Text overflowText = CreateLabel(card.transform, "Overflow", "");
        overflowText.raycastTarget = false;
        overflowText.fontSize = 14;
        overflowText.fontStyle = FontStyles.Bold;
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
        combineTooltipText.alignment = TextAlignmentOptions.TopLeft;
        combineTooltipText.textWrappingMode = TextWrappingModes.Normal;

        combineTooltipObject = tooltipObj;
        combineTooltipObject.SetActive(false);
    }

    // 유닛 명령 자리(공격/정지/모으기/정렬 + 맨 아랫줄은 조합 결과). 0~2번·12번은 고정
    // placeholder로 인터랙션을 꺼둔다(정지/모으기/정렬만 켜짐). 나머지는 RefreshUnitCommandCards가 채운다.
    // 알파 0.12는 UnitCommandPanel 배경(검정 0.35) 위에서 거의 안 보여 "버튼이 떠 있다"는
    // 인상을 줬다(2026-09-23 사장님 지적) — 패널보다 밝게 올려 칸 경계가 보이게 한다.
    static readonly Color UnitCommandDefaultColor = ButtonColor;

    void BuildUnitCommandGrid(RectTransform parent)
    {
        GridLayoutGroup grid = parent.gameObject.AddComponent<GridLayoutGroup>();
        // 4행(50) 높이 그대로 5행에 나눠 담느라 칸 높이를 줄인다: (50*4 + 6*3) / 5 - 6 = 38.8.
        // 패널 크기는 그대로 두면서 정렬(C) 칸 하나를 더 넣기 위한 계산이라 임의로 줄인 값이 아니다.
        grid.cellSize = new Vector2(38f, 38f);   // 정사각 — 워크3식 명령 버튼
        grid.spacing = new Vector2(6f, 6f);
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CommandColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < CommandSlotCount; i++)
        {
            BuildUnitCommandSlot(i, parent);

            if (i < UnitOnlyCommandLabels.Length)
            {
                unitCommandSlotNames[i].text = UnitOnlyCommandLabels[i];
                unitCommandSlotHotkeys[i].text = UnitOnlyCommandHotkeys[i];
                unitCommandSlotButtons[i].interactable = true;
            }
            else if (i == AlignCommandSlot)
            {
                unitCommandSlotNames[i].text = AlignCommandLabel;
                unitCommandSlotHotkeys[i].text = AlignCommandHotkey;
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
    bool unitOnlyCommandsShown = true;

    void SetUnitOnlyCommandsVisible(bool visible)
    {
        unitOnlyCommandsShown = visible;
        for (int i = 0; i < UnitOnlyCommandLabels.Length; i++)
        {
            unitCommandSlotNames[i].text = visible ? UnitOnlyCommandLabels[i] : "";
            unitCommandSlotHotkeys[i].text = visible ? UnitOnlyCommandHotkeys[i] : "";
            unitCommandSlotBackgrounds[i].color = visible ? UnitCommandDefaultColor : Color.clear;
            unitCommandSlotButtons[i].interactable = visible;
        }

        unitCommandSlotNames[AlignCommandSlot].text = visible ? AlignCommandLabel : "";
        unitCommandSlotHotkeys[AlignCommandSlot].text = visible ? AlignCommandHotkey : "";
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

        AddPanelBorder((RectTransform)card.transform, BorderColor, BorderThickness);

        TMP_Text nameText = CreateLabel(card.transform, "Name", "");
        nameText.raycastTarget = false;
        // 🔴 접으면 안 된다 — 38짜리 정사각 버튼에서 "공/격" "모/으/기"처럼 세로로 쪼개진다
        //    (사장님 화면 2026-09-23). 칸에 안 들어가면 **접지 말고 글씨를 줄인다.**
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 8f;
        nameText.fontSizeMax = 13f;
        // 아래 30%는 단축키 라벨 자리라 이름이 거기까지 내려오지 않게 비운다.
        nameText.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        nameText.rectTransform.anchorMax = Vector2.one;
        nameText.rectTransform.offsetMin = new Vector2(2f, 0f);
        nameText.rectTransform.offsetMax = new Vector2(-2f, -2f);

        // 단축키는 오른쪽 아래 구석에 작게. 이름과 겹치지 않게 아래 30%만 쓴다.
        TMP_Text hotkeyText = CreateLabel(card.transform, "Hotkey", "");
        hotkeyText.raycastTarget = false;
        hotkeyText.fontSize = 9;
        hotkeyText.alignment = TextAlignmentOptions.BottomRight;
        hotkeyText.color = new Color(1f, 1f, 1f, 0.7f);
        hotkeyText.textWrappingMode = TextWrappingModes.NoWrap;
        hotkeyText.rectTransform.anchorMin = Vector2.zero;
        hotkeyText.rectTransform.anchorMax = new Vector2(1f, 0.3f);
        hotkeyText.rectTransform.offsetMin = new Vector2(2f, 2f);
        hotkeyText.rectTransform.offsetMax = new Vector2(-3f, 0f);
        unitCommandSlotHotkeys[index] = hotkeyText;

        unitCommandSlotRoots[index] = card;
        unitCommandSlotBackgrounds[index] = background;
        unitCommandSlotNames[index] = nameText;
        unitCommandSlotButtons[index] = button;
    }

    // 2026-09-26 베타 피드백으로 워크3 배치: 0 공격(A — 다음 클릭이 대상) · 1 정지(S) · 2 홀드(H — 자리 지키며
    // 사거리 안 적은 친다) · 3 모으기(V) · 4 정렬(C). 조합·상점은 7~15(UnitCommandResultSlotOrder)라 4~6은 비어 있었다.
    // 이름과 단축키를 나눠 둔다 — 단축키는 버튼 오른쪽 아래 구석에 작게 따로 그린다
    // (한 줄에 "정지 (H)"로 붙여 쓰면 38짜리 정사각 버튼에서 두 줄로 접혀 뭉개진다).
    static readonly string[] UnitOnlyCommandLabels = { "공격", "정지", "홀드", "모으기" };
    static readonly string[] UnitOnlyCommandHotkeys = { "A", "S", "H", "V" };

    const int AttackCommandSlot = 0;
    const int StopCommandSlot = 1;
    const int HoldCommandSlot = 2;
    const int GatherCommandSlot = 3;
    const int AlignCommandSlot = 4;
    const string AlignCommandLabel = "정렬";
    const string AlignCommandHotkey = "C";

    // MP: 멀티 클라에서 호스트 판정이 필요한 버튼은 요청 RPC가 생길 때까지 막는다 — 누르면 클라 로컬 상태만
    //     바뀌어 화면이 거짓말을 한다(설계 §6). 싱글·호스트는 IsServer라 항상 false.
    static bool BlockedOnMultiplayerClient()
    {
        if (GameAuthority.IsServer) return false;
        PlayerNotification.Show(LocalPlayer.LocalPlayerId, "같이 하기에서는 아직 쓸 수 없습니다(다음 단계에서 열립니다).");
        return true;
    }

    void OnUnitCommandSlotClicked(int index)
    {
        // 단축키와 같은 함수를 부른다 — 두 곳에 따로 구현하면 한쪽만 고쳐진다.
        if (index >= AttackCommandSlot && index <= AlignCommandSlot && currentShop as Object == null)
        {
            SelectionManager selection = Selection;
            if (selection == null || selection.Selected.Count == 0) return;

            if (index == AttackCommandSlot) selection.BeginAttackTargeting();
            else if (index == StopCommandSlot) UnitCommands.Stop(selection.Selected);
            else if (index == HoldCommandSlot) UnitCommands.Hold(selection.Selected);
            else if (index == GatherCommandSlot) UnitCommands.Gather(selection.Selected);
            else UnitCommands.SendToPen(selection.Selected);
            return;
        }

        // MP: 위 유닛 명령은 UnitCommands가, 아래 상점은 UseShop이, 조합은 아래 분기가 클라면 호스트에 요청을 보낸다.

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

        // 원작 [조합]은 유닛 능력 — 결과가 누른 유닛 자리에 나온다. 선택 첫 유닛을 시전 유닛으로 본다.
        SelectionManager casterSelection = Selection;
        Vector3? casterPosition = casterSelection != null && casterSelection.Selected.Count > 0 && casterSelection.Selected[0] != null
            ? casterSelection.Selected[0].transform.position
            : (Vector3?)null;

        // MP: 멀티 클라는 조합을 호스트에 요청만 한다(재료 소모·결과 소환은 호스트, 결과는 거울로 돌아온다).
        if (!GameAuthority.IsServer)
        {
            NetCommands.RequestCombine(system, recipe, casterPosition);
            HideCombineTooltip();
            return;
        }

        if (system.TryCombine(recipe, casterPosition))
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
    // MP: 상점 실행은 여기 한 곳으로 모은다. 멀티 클라는 호스트에 요청만 보내고 보낸 것으로 성공 처리한다 —
    //     실패 사유는 호스트가 TryUse를 돌린 뒤 알림으로 돌려준다. 싱글·호스트는 그대로 TryUse.
    static bool UseShop(ILaneShop shop, int index, LaneShopTarget target, out string reason)
    {
        if (!GameAuthority.IsServer) { reason = null; return NetCommands.RequestShopUse(shop, index, target); }
        return shop.TryUse(index, target, out reason);
    }

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
            if (UseShop(currentShop, logicalIndex, default, out string reason)) RefreshShopAffordability(); // MP: UseShop
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

            used = UseShop(shop, index, LaneShopTarget.OnUnit(targetObject), out reason); // MP: UseShop
        }
        else
        {
            used = UseShop(shop, index, LaneShopTarget.AtPoint(hit.point), out reason); // MP: UseShop
        }

        if (used) RefreshShopAffordability();
        else PlayerNotification.Show(LocalPlayer.LocalPlayerId, reason ?? "지금은 사용할 수 없습니다.");
    }

    // 로빈(H098) 전용 — RefreshShopTargeting과 같은 모양(칸을 고른 뒤 다음 클릭을 기다리다
    // WorldPick.TryHit으로 대상을 찍는다)이지만 ILaneShop이 아니라 별도로 둔다. 우클릭이면
    // 취소, 대상이 없으면 실패 메시지 — 상점 쪽과 같은 UX. 비용은 대상이 실제로 정해진
    // 이 시점에야 나간다(원작이 캐스트 완료 시점에만 자원을 떼는 것과 같다).
    void RefreshTraitTargeting()
    {
        if (pendingTraitTarget == null) return;
        if (Mouse.current == null) { pendingTraitTarget = null; pendingTraitContext = null; return; }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            pendingTraitTarget = null;
            pendingTraitContext = null;
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.frameCount <= pendingTraitTargetingStartFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        UnitTraitData trait = pendingTraitTarget;
        PlayerContext context = pendingTraitContext;
        pendingTraitTarget = null;
        pendingTraitContext = null;

        int notifyPlayerId = context != null ? context.PlayerId : LocalPlayer.LocalPlayerId;

        if (!WorldPick.TryHit(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            PlayerNotification.Show(notifyPlayerId, "대상을 찾을 수 없습니다.");
            return;
        }

        // 원작 대상 제한 확인 결과: 없음(아이템 툴팁 "어떠한 유닛이든", atar='air,
        // invulnerable,organic,ground') — 소유주 제한도 못 찾아 우리도 안 건다. 유닛이기만
        // 하면(적 EnemyDummy 포함, 원작이 막는다는 근거가 없어 안 막는다) 통과시킨다.
        if (!hit.collider.TryGetComponent(out UnitIdentity targetIdentity))
        {
            PlayerNotification.Show(notifyPlayerId, "대상으로 쓸 수 없습니다.");
            return;
        }

        // MP: 멀티 클라는 대상까지 골랐으면 호스트에 요청(호스트가 요청자 슬롯의 특성 포인트로 아래 본체를 돈다).
        if (!GameAuthority.IsServer) { NetCommands.RequestTraitTarget(trait, targetIdentity); lastTraitButtonPoints = int.MinValue; return; }

        ExecuteTraitTargetOn(trait, context, targetIdentity, notifyPlayerId);
    }

    // MP: 대상 지정 특성의 실행 본체 — 로컬 클릭(위)과 멀티 호스트가 받은 요청이 같이 쓴다. 줄 내용은 그대로다.
    public void ExecuteTraitTargetOn(UnitTraitData trait, PlayerContext context, UnitIdentity targetIdentity, int notifyPlayerId)
    {
        UnitUpgrades upgrades = context != null ? context.UnitUpgrades : null;
        if (upgrades == null || upgrades.IsUnlocked(trait)) return;

        if (!upgrades.TrySpendTraitPoints(trait.costTraitPoints))
        {
            PlayerNotification.Show(notifyPlayerId, "특성 포인트가 부족합니다!");
            return;
        }

        upgrades.Unlock(trait);

        // 원작 A0FL(영구 블링크)+A0ZP(시각효과 전용, 날개 부착물) 부여 — 지금은 표시만
        // 한다(UnitIdentity.hasRobinWingBlessing 주석 참고, MovementAbility.Teleport
        // 로직이 아직 없어 실제 블링크 자체는 못 켠다).
        targetIdentity.hasRobinWingBlessing = true;

        lastTraitButtonPoints = int.MinValue;
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

        // 2026-09-26: 아무것도 안 골랐으면(살펴보기 중 포함) 공격·정지·홀드·모으기·정렬을 감춘다 —
        // 누를 대상이 없는데 떠 있으면 헷갈린다(워크3도 선택이 없으면 명령 카드가 빈다).
        bool wantUnitCommands = shop == null && count > 0;
        if (wantUnitCommands != unitOnlyCommandsShown)
        {
            unitOnlyCommandsShown = wantUnitCommands;
            SetUnitOnlyCommandsVisible(wantUnitCommands);
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
        shopLogicalSlotIndex[AttackCommandSlot] = -1;
        shopLogicalSlotIndex[StopCommandSlot] = -1;
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
        if (unitInfoPortraitSlotObject != null) unitInfoPortraitSlotObject.SetActive(true);

        if (count == 0)
        {
            // 살펴보기(적·조합표 인형 — 조작 불가, 정보만). 친구 베타 피드백 ⑤(2026-09-26).
            GameObject inspect = InspectTarget.Current;
            if (inspect != null && ShowInspectInfo(inspect)) return;
            unitInfoText.text = "선택된 유닛 없음";
            SetUnitInfoPortrait(null);
            return;
        }

        Selectable first = selection.Selected[0];
        if (first == null)
        {
            unitInfoText.text = "선택된 유닛 없음";
            SetUnitInfoPortrait(null);
            return;
        }

        first.TryGetComponent(out UnitIdentity identity);
        first.TryGetComponent(out UnitAttacker attacker);
        UnitData data = identity != null ? identity.Data : null;

        SetUnitInfoPortrait(data);

        // 위습은 UnitIdentity가 없어서 예전엔 오브젝트 이름이 그대로 떴다 — 화면에
        // **「WispPrefab(Clone)」**이라고 나왔다(2026-09-24 플레이 캡처). 사장님이 보는 이름은
        // 위습 에셋의 이름이어야 하고, 등급색도 그 위습이 뽑는 등급을 따라야 한다.
        first.TryGetComponent(out Wisp selectedWisp);
        WispData wispData = selectedWisp != null ? selectedWisp.Data : null;

        string unitName = data != null ? data.unitName
                        : wispData != null ? wispData.wispName
                        : first.name;
        string grade = data != null ? data.grade.KoreanName()
                     : wispData != null ? $"{wispData.targetGrade.KoreanName()} 뽑기"
                     : "-";
        string gradeColorHex = ColorUtility.ToHtmlStringRGB(
            data != null ? GetGradeColor(data.grade)
            : wispData != null ? GetGradeColor(wispData.targetGrade)
            : Color.white);
        // 플레이어 유닛에 아직 별도 체력 컴포넌트가 없어, UnitData의 기준 hp를 표시한다(실시간 값 아님).
        string hp = data != null ? data.hp.ToString("F0") : "-";
        string attackPower = attacker != null ? attacker.AttackDamage.ToString("F0") : "-";
        string attackRange = attacker != null ? attacker.AttackRange.ToString("F1") : "-";
        string attackSpeed = attacker != null && attacker.AttackInterval > 0f
            ? (1f / attacker.AttackInterval).ToString("F2")
            : "-";

        // 마나·방어력은 PM 요청에 있었으나 UnitData/UnitAttacker에 그 필드 자체가 없다
        // (플레이어 유닛은 마나를 소모하지 않고, 방어력 감폭은 EnemyDummy 전용 축이다) —
        // 없는 값을 지어내지 않고 실제로 있는 축만 표시한다(2026-09-23, PM 보고 예정).
        unitInfoText.text =
            $"<color=#{gradeColorHex}>{unitName}</color>\n등급: {grade}\n체력: {hp}\n공격력: {attackPower}\n사거리: {attackRange}\n공격속도: {attackSpeed}/s";
    }

    static string ArmorTypeName(ArmorType type)
    {
        switch (type)
        {
            case ArmorType.Normal: return "일반";
            case ArmorType.Large: return "대형";
            case ArmorType.Fort: return "요새";
            case ArmorType.Hero: return "영웅";
            default: return "-";
        }
    }

    // 살펴보기 정보 — 적은 실시간 체력·방어, 조합표 인형은 그 유닛의 기준 스탯. 둘 다 「조작 불가」를 밝힌다.
    bool ShowInspectInfo(GameObject target)
    {
        if (target.TryGetComponent(out EnemyDummy enemy))
        {
            SetUnitInfoPortrait(null);
            string enemyName = enemy.Data != null && !string.IsNullOrEmpty(enemy.Data.enemyName) ? enemy.Data.enemyName : enemy.name;
            string tag = enemy.IsBoss ? "보스" : "적";
            unitInfoText.text =
                $"<color=#FF6B6B>{enemyName}</color>  <size=80%>({tag} · 조작 불가)</size>\n" +
                $"체력: {Mathf.Max(0f, enemy.Hp):F0} / {enemy.MaxHp:F0}\n" +
                $"방어력: {enemy.EffectiveArmor:F1} ({ArmorTypeName(enemy.ArmorType)})\n" +
                $"이동속도: {enemy.MoveSpeed:F0}";
            return true;
        }
        if (target.TryGetComponent(out DollInfo doll) && doll.Unit != null)
        {
            UnitData data = doll.Unit;
            SetUnitInfoPortrait(data);
            string hex = ColorUtility.ToHtmlStringRGB(GetGradeColor(data.grade));
            unitInfoText.text =
                $"<color=#{hex}>{data.unitName}</color>  <size=80%>(조합표 · 조작 불가)</size>\n등급: {data.grade.KoreanName()}\n체력: {data.hp:F0}\n" +
                $"공격력: {data.attackPower:F0}\n사거리: {data.attackRange:F1}\n공격속도: {data.attackSpeed:F2}/s";
            return true;
        }
        return false;
    }

    // 초상화 아트가 없으므로 등급 색 배경만 채운다 — BuildCard(다중 선택 카드)와 같은 관례.
    void SetUnitInfoPortrait(UnitData data)
    {
        if (unitInfoPortrait == null) return;

        if (data == null)
        {
            unitInfoPortrait.color = SlotColor;
            if (unitInfoPortraitInitial != null) unitInfoPortraitInitial.text = "";
            return;
        }

        Color grade = GetGradeColor(data.grade);
        // 바탕은 등급색을 어둡게 깔고, 글자는 등급색 그대로 — 대비가 있어야 읽힌다.
        unitInfoPortrait.color = new Color(grade.r * 0.35f, grade.g * 0.35f, grade.b * 0.35f, 1f);
        if (unitInfoPortraitInitial == null) return;

        unitInfoPortraitInitial.text = string.IsNullOrEmpty(data.unitName)
            ? "" : data.unitName.Substring(0, 1);
        unitInfoPortraitInitial.color = grade;
    }

    // 다중 선택. 카드 자체는 BuildSelectionCards에서 미리 만들어뒀고, 여기서는 내용과
    // 활성 상태만 바꾼다. 이전 프레임과 같은 대상 구성이면 아무것도 다시 안 그린다.
    void ShowCardGrid(SelectionManager selection)
    {
        unitInfoText.gameObject.SetActive(false);
        if (unitInfoPortraitSlotObject != null) unitInfoPortraitSlotObject.SetActive(false);
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

        // 🔴 라운드 사이 준비시간에는 roundTimer가 안 돌아서 계속 "남은시간 0.0s"로 보였다
        //    (사장님 화면 2026-09-23). RoundManager.PreRoundTimeLeft가 이미 있었는데
        //    **읽는 곳이 프로젝트에 한 군데도 없었다** — 값만 있고 화면에 닿질 않았다.
        bool preparing = rm != null && rm.IsWaitingForNextRound;
        float shown = rm == null ? 0f : (preparing ? rm.PreRoundTimeLeft : rm.RoundTimeLeft);

        // MP: 멀티 클라의 RoundManager는 멈춰 있다(판정은 호스트) — 호스트가 NetGameState에 실어 보낸 값을 쓴다
        //     (PM 결정 (b): RoundManager는 안 건드린다). 싱글·호스트는 IsServer라 이 블록을 지나친다.
        if (!GameAuthority.IsServer && NetGameState.Instance != null)
        {
            round = NetGameState.Instance.Round;
            preparing = NetGameState.Instance.Preparing;
            shown = NetGameState.Instance.TimeLeft;
        }
        int timeTenths = Mathf.RoundToInt(shown * 10f);

        if (round != lastRound || timeTenths != lastTimeTenths || preparing != lastPreparing)
        {
            lastRound = round;
            lastTimeTenths = timeTenths;
            lastPreparing = preparing;
            roundTimeText.text = rm == null
                ? "라운드 -   남은시간 -"
                : preparing
                    ? $"라운드 {round}   준비 {timeTenths / 10f:F1}s"
                    : $"라운드 {round}   남은시간 {timeTenths / 10f:F1}s";
        }

        RefreshWispCount();
    }

    /// <summary>
    /// 미니맵 바로 위에 위습 종류별 정사각형 칸을 만든다.
    /// 사장님 지시 2026-09-24: 「래덤위습 뜨는것도 정사각형으로 해주고 왼쪽 미니맵 위에 배치해줘
    /// 그리고 클릭하면 해당위치로 이동하게해주고」.
    ///
    /// ⚠️ 정사각형은 **비율 앵커로는 못 만든다.** anchorMin/Max를 0.01~0.05 같은 화면 비율로
    /// 주면 창이 넓어질 때 가로만 늘어나 직사각형이 된다(처음 만든 「위습 N」 띠가 그랬다).
    /// 그래서 한 점(0.01, MinimapTop)에 고정하고 **픽셀 크기를 직접** 준다 —
    /// sizeDelta에 가로·세로 같은 값을 넣는 것이 정사각형의 근거다.
    /// 붙는 자리는 여전히 MinimapTop이라 미니맵을 옮기면 같이 따라온다.
    ///
    /// 왜 종류별 한 칸인가: 위습은 종류에 따라 나오는 것이 다르다(랜덤유닛·흔함 선택·초월…).
    /// 개수만 「위습 5」로 합치면 **무엇이 5개인지 모른다.** 배경은 그 위습이 뽑는 등급색이라
    /// 조합판·이름표에서 쓰는 색과 같은 뜻으로 읽힌다.
    /// </summary>
    void BuildWispSlots()
    {
        for (int i = 0; i < MaxWispSlots; i++)
        {
            int row = i / WispSlotsPerRow;
            int col = i % WispSlotsPerRow;

            RectTransform slot = CreatePanel(transform, $"WispSlot{i}", SlotColor);
            slot.anchorMin = slot.anchorMax = new Vector2(0.01f, MinimapTop);
            slot.pivot = Vector2.zero;   // 왼쪽 아래 모서리 기준 — 칸이 미니맵 위로 쌓인다
            slot.anchoredPosition = new Vector2(col * (WispSlotSize + WispSlotGap),
                                                row * (WispSlotSize + WispSlotGap));
            slot.sizeDelta = new Vector2(WispSlotSize, WispSlotSize);
            AddPanelBorder(slot, BorderColor, BorderThickness);

            // 이름은 글자 수가 종류마다 달라(「초월」 2자 ~ 「백수생활선택」 6자) 자동 축소를 켠다.
            // 44px 칸에 고정 크기를 쓰면 긴 이름이 잘려 무슨 위습인지 알 수 없다.
            TMP_Text nameText = CreateLabel(slot, "Name", "");
            SetAnchors((RectTransform)nameText.transform, new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.98f));
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 7f;
            nameText.fontSizeMax = 12f;
            // 줄바꿈을 **켠다.** NoWrap이면 「백수생활선택」 같은 긴 이름이 한 줄로 뻗어
            // 칸 밖으로 나가고, 왼쪽 끝 칸은 화면 밖으로 잘린다(44px 판에서 실제로 그랬다).
            nameText.textWrappingMode = TextWrappingModes.Normal;
            nameText.overflowMode = TextOverflowModes.Truncate;
            nameText.color = Color.black;   // 배경이 등급색(밝은 편)이라 검정이 읽힌다
            nameText.raycastTarget = false;

            TMP_Text countText = CreateLabel(slot, "Count", "");
            SetAnchors((RectTransform)countText.transform, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.44f));
            countText.fontSize = 19f;
            countText.fontStyle = FontStyles.Bold;
            countText.color = Color.black;
            countText.raycastTarget = false;

            int index = i;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(() => OnWispSlotClicked(index));
            slot.gameObject.SetActive(false);   // 그 종류가 하나도 없으면 칸을 아예 안 보인다

            wispSlots.Add(new WispSlot
            {
                root = slot.gameObject,
                background = slot.GetComponent<Image>(),
                nameText = nameText,
                countText = countText,
            });
        }
    }

    // 위습 칸 갱신. 매 프레임 맵을 훑으면 비싸니 0.5초에 한 번만 센다 —
    // 위습은 라운드 보상으로 늘고 포탈에 넣으면 줄어드는, 초 단위로 안 바뀌는 값이다
    // (DebugHud.CountOwnedWisps와 같은 주기·같은 방식).
    void RefreshWispCount()
    {
        if (wispSlots.Count == 0) return;
        if (Time.unscaledTime < nextWispCountTime) return;
        nextWispCountTime = Time.unscaledTime + 0.5f;

        int localPlayerId = LocalPlayer.LocalPlayerId;
        foreach (List<Wisp> bucket in wispsByType.Values) bucket.Clear();
        wispTypeOrder.Clear();

        foreach (Wisp wisp in FindObjectsByType<Wisp>(FindObjectsSortMode.None))
        {
            // IsConsumed를 빼는 이유: Destroy는 프레임 끝에야 처리돼서, 포탈에 들어간 위습이
            // 한 프레임 더 잡힌다. 그걸 세면 개수가 잠깐 하나 많게 보인다.
            if (wisp == null || wisp.Data == null || wisp.IsConsumed) continue;
            if (wisp.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != localPlayerId) continue;

            if (!wispsByType.TryGetValue(wisp.Data, out List<Wisp> bucket))
            {
                bucket = new List<Wisp>();
                wispsByType[wisp.Data] = bucket;
            }
            bucket.Add(wisp);
        }

        foreach (KeyValuePair<WispData, List<Wisp>> pair in wispsByType)
            if (pair.Value.Count > 0) wispTypeOrder.Add(pair.Key);

        // 칸 자리가 0.5초마다 바뀌면 누르려던 칸이 손가락 아래에서 도망간다. 등급 순으로 고정한다.
        wispTypeOrder.Sort(CompareWispTypes);

        for (int i = 0; i < wispSlots.Count; i++)
        {
            WispSlot slot = wispSlots[i];
            if (i >= wispTypeOrder.Count)
            {
                slot.data = null;
                if (slot.root.activeSelf) slot.root.SetActive(false);
                continue;
            }

            WispData data = wispTypeOrder[i];
            slot.data = data;
            slot.background.color = data.targetGrade.Color();
            slot.nameText.text = ShortWispName(data.wispName);
            slot.countText.text = wispsByType[data].Count.ToString();
            if (!slot.root.activeSelf) slot.root.SetActive(true);
        }

        // 칸보다 종류가 많으면 남는 종류는 **화면에 아예 안 나온다.** 조용히 넘기면
        // 「내 위습이 사라졌다」로 보이므로 한 번은 찍는다.
        if (wispTypeOrder.Count > wispSlots.Count && !warnedWispOverflow)
        {
            warnedWispOverflow = true;
            Debug.LogWarning($"[HUD] 위습 종류가 {wispTypeOrder.Count}가지인데 칸은 {wispSlots.Count}개뿐입니다 — " +
                             $"{wispTypeOrder.Count - wispSlots.Count}가지가 미니맵 위에 안 보입니다. " +
                             "GameHud.MaxWispSlots를 늘리세요.");
        }
    }

    static int CompareWispTypes(WispData a, WispData b)
    {
        int tierCompare = a.targetGrade.Tier().CompareTo(b.targetGrade.Tier());
        return tierCompare != 0 ? tierCompare : string.CompareOrdinal(a.wispName, b.wispName);
    }

    // 「랜덤유닛 위습」→「랜덤유닛」. 44px 칸에 「위습」을 아홉 번 쓰는 건 자리 낭비다.
    static string ShortWispName(string wispName)
    {
        if (string.IsNullOrEmpty(wispName)) return "위습";
        string name = wispName.Replace("위습", "").Trim();
        return name.Length == 0 ? "위습" : name;
    }

    /// <summary>
    /// 위습 칸 클릭 — 그 위습이 있는 곳으로 화면을 옮기고 그 위습을 선택한다.
    /// 선택까지 하는 이유: 화면만 옮기면 사장님이 위습을 또 찾아 클릭해야 한다. 원작도
    /// 아이콘을 누르면 선택된다. 선택돼 있으면 곧바로 포탈에 우클릭할 수 있다.
    /// </summary>
    void OnWispSlotClicked(int index)
    {
        if (index < 0 || index >= wispSlots.Count) return;
        WispData data = wispSlots[index].data;
        if (data == null || !wispsByType.TryGetValue(data, out List<Wisp> bucket) || bucket.Count == 0) return;

        // 같은 종류가 여럿이면 누를 때마다 다음 위습으로 간다. 늘 첫 번째만 보여주면
        // 포탈에 하나씩 넣는 동안 나머지를 찾을 길이 없다.
        // ⚠️ 셀 때마다 접는다 — 위습이 소모돼 목록이 줄면 저장해 둔 번호가 범위를 넘는다.
        wispCycle.TryGetValue(data, out int cycle);
        cycle %= bucket.Count;
        wispCycle[data] = (cycle + 1) % bucket.Count;

        Wisp wisp = bucket[cycle];
        if (wisp == null) return;

        if (wispCamera == null) wispCamera = FindFirstObjectByType<RtsCameraController>();
        if (wispCamera != null) wispCamera.MoveTo(wisp.transform.position);

        if (selectionManager != null && wisp.TryGetComponent(out Selectable selectable))
            selectionManager.SelectOnly(selectable);
    }

    // 팀 현황판 값은 자주 안 바뀌므로(적/골드/목재), 이전 프레임과 비교해 실제로 바뀐 경우에만
    // StringBuilder를 다시 채운다.
    void RefreshTeamPanel()
    {
        if (teamPanelText == null) return;

        // Active.Count가 아니라 CountInLanes()다 — 물범·해왕류처럼 레인 밖에 선 것들을 빼야
        // 아랫줄의 「플레이어 N | 적 M」들과 합이 맞는다. 자세한 사고 경위는 CountInLanes 주석.
        int totalEnemies = EnemyDummy.CountInLanes();

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
        bool running = hasStory && story.HasRunningStory; // MP: 클라는 호스트 값(StoryManager.HasRunningStory)
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

    // 9-slice 스프라이트가 없어 모서리 4개를 얇은 Image 띠로 겹쳐 테두리처럼 보이게 한다.
    // 미니맵·초상화 칸처럼 "이 영역이 하나의 칸"임을 배경 알파만으로는 못 알아볼 때 쓴다.
    static void AddPanelBorder(RectTransform parent, Color color, float thickness)
    {
        CreateBorderStrip(parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        CreateBorderStrip(parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        CreateBorderStrip(parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        CreateBorderStrip(parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);
    }

    static void CreateBorderStrip(RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        obj.GetComponent<Image>().raycastTarget = false;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static TMP_Text CreateLabel(Transform parent, string name, string content)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 4f);
        rect.offsetMax = new Vector2(-8f, -4f);

        TMP_Text text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content;
        if (UiFont != null) text.font = UiFont;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    /// <summary>
    /// HUD 글꼴 — 프리텐다드 TMP 에셋(SIL OFL, Assets/Fonts). 레거시 <c>Text</c>에서 TMP로
    /// 옮긴 이유는 화질이다(사장님 지적 2026-09-23 "하단에 글씨 화질이 안 좋은데"):
    /// 레거시는 fontSize별 비트맵을 구워 쓰므로 캔버스가 확대되면 그대로 늘어나 번지고,
    /// TMP는 SDF라 배율과 무관하게 또렷하다.
    ///
    /// ⚠️ 에셋은 `Tools/HUD/한글 TMP 폰트 에셋 생성` 메뉴로 굽는다(KoreanFontAssetBuilder).
    /// 아직 안 구웠으면 null이고, 그때는 TMP 기본 글꼴로 떨어져 **한글이 네모로 보인다** —
    /// 조용히 넘어가면 원인을 못 찾으므로 경고를 한 번 남긴다.
    /// Resources.Load를 쓰는 이유: GameHud는 런타임 코드라 AssetDatabase를 못 쓰고,
    /// 씬은 맵 생성기가 다시 만들어서 인스펙터 참조도 못 건다.
    /// </summary>
    static TMP_FontAsset uiFont;
    static bool uiFontChecked;

    /// <summary>HUD 바깥(유닛 이름표 등)에서도 같은 글꼴을 쓰도록 연 접근자.</summary>
    public static TMP_FontAsset UiFontAsset => UiFont;

    static TMP_FontAsset UiFont
    {
        get
        {
            if (uiFontChecked) return uiFont;
            uiFontChecked = true;

            uiFont = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Regular SDF");
            if (uiFont == null)
                Debug.LogWarning("[HUD] 한글 TMP 폰트 에셋을 못 찾았습니다 — 메뉴 " +
                                 "'Tools/HUD/한글 TMP 폰트 에셋 생성'을 한 번 실행하세요. " +
                                 "그때까지 한글이 네모로 보입니다.");
            return uiFont;
        }
    }

    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
