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
    // 2026-09-29 워크3 콘솔 개편(PM 승인): 남색 계열 → 어두운 금속 + 금테. 세 단계(판 → 칸 → 버튼) 규칙은 그대로.
    static readonly Color PanelColor = new Color(0.10f, 0.09f, 0.08f, 1f);   // 하단 바(어두운 청동회색)
    static readonly Color SlotColor = new Color(0.05f, 0.05f, 0.06f, 1f);    // 칸(미니맵·정보·명령) — 판보다 어둡게 파인 칸
    static readonly Color ButtonColor = new Color(0.20f, 0.18f, 0.15f, 1f);  // 버튼
    static readonly Color BorderColor = new Color(0.78f, 0.62f, 0.30f, 1f);  // 칸 테두리(금테)
    static readonly Color BorderInnerColor = new Color(0.40f, 0.30f, 0.12f, 1f);   // 콘솔 칸 안쪽 한 줄(짙은 금) — 테두리가 두 겹으로 읽힌다
    // 3D 화면과 하단 바를 가르는 선. 판보다 확실히 밝아야 경계로 읽힌다.
    static readonly Color BarEdgeColor = new Color(0.78f, 0.62f, 0.30f, 1f);
    const float BorderThickness = 1.5f;
    const float BarEdgeThickness = 3f;

    // 명령 격자: 워크3 명령 카드 **4열 × 3줄 = 12칸**(09-29 콘솔 개편).
    //   1줄 이동 M · 정지 S · 홀드 H · 공격 A / 2줄 모으기 V · 정렬 C · (빈칸) · 판매 / 3줄 조합 결과 4칸.
    //   조합식 207개를 첫 재료별로 세면 한 유닛 결과는 최대 4개(09-29 실측)라 3줄 4칸에 다 들어간다.
    //   칸 크기는 박지 않는다 — 패널 크기에서 계산한다(FitGrid). 38px 칸이 패널 가운데 작게 몰려 글씨가 안 보였다(사장님 09-29).
    const int CommandSlotCount = 12;
    const int CommandRows = 3;

    // 콘솔 오른쪽 두 칸은 **고정 폭**(캔버스 px)으로 오른쪽 끝에 붙인다. 비율로 박으면 4:3에서 명령칸이 줄어 칸 글씨가 깨진다.
    //   가운데 정보칸이 남는 폭을 받는다. 1920 기준: 미니맵 19~461 | 초상 472~672 | 정보 680~1266 | 아이템 1274~1444 | 명령 1452~1912.
    const float ConsoleMargin = 8f;
    const float CommandPanelWidth = 460f;
    const float ItemPanelWidth = 170f;
    // 09-29 사장님 「미니맵 양옆 검은 여백 없애고 가운데를 넓혀」: 왼쪽 세 칸(미니맵·초상·정보)은 ConsoleLeft의 가로 레이아웃이 줄 세운다.
    //   미니맵 칸 폭 = 칸 높이 × 땅 비율(MinimapCamera.GroundAspect) — 칸이 곧 그림이라 검은 띠가 없다. 초상은 높이 × PortraitAspect.
    //   남는 폭은 정보·카드 칸이 받는다. 폭은 땅을 잰 뒤·칸 높이가 바뀔 때 RefreshConsoleLayout이 다시 잡는다(첫 그림 전 Update에서).
    const float PortraitAspect = 0.94f;   // 200 × 213(1920 기준) — 워크3 초상 비율
    const float MinimapInset = 5f;        // 금테 안쪽 여백(BuildUI의 MinimapArea)
    const float ConsoleGap = 8f;

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
    // 2026-10-03 사장님 인게임 사진 기준: 콘솔 높이 27%(y 73~100%). 옛 22%에서 키웠다(3D 화면이 그만큼 더 가려진다 — 보고 항목).
    const float BottomBarHeight = 0.27f;
    const float MinimapTop = 0.27f;
    const int CommandColumns = 4;
    const int TeamSlotCount = 4;
    const int MaxSelectionCards = 12;
    const int SelectionCardColumns = 6;   // 워크3처럼 6열 × 2줄(09-29 콘솔 개편 — 정보칸이 초상화와 갈라져 넓어졌다)
    const int SelectionCardRows = 2;
    const int MaxInventoryEntries = 16;
    const int MaxItemInventorySlots = 6;   // 원작 영웅 인벤토리 2×3(사장님 확정 10-03) — ItemInventory.MaxItems와 같은 값

    [SerializeField] SelectionManager selectionManager;

    TMP_Text unitInfoText;
    Image unitInfoPortrait;
    TMP_Text unitInfoPortraitInitial;
    RawImage unitInfoPortraitModel;   // 초상화(09-26 사장님 요청): 실제 모델이 Idle로 서 있는 RenderTexture(PortraitStage)
    GameObject unitInfoPortraitSlotObject;
    Image portraitHpBar, portraitMpBar;          // 초상 아래 체력·마나 바(채움 비율로 그린다)
    TMP_Text portraitHpText, portraitMpText;
    GameObject unitStatRows;                     // 정보칸 「공격력/방어/상태」 줄(사진 서식) — 유닛 한 기를 고를 때만
    TMP_Text unitDamageText, unitArmorText, unitStatusText;
    TMP_Text goldText;
    TMP_Text woodText;
    TMP_Text foodText;        // 고기 칸 = 원작 FOOD_USED = 우리 특성 포인트(사장님 확정 10-03)
    TMP_Text manaText;        // 원작 상단 바엔 없다 — 도움소 스킬이 쓰는 플레이어 마나(사장님 10-02 요청)라 시계 옆에 작게 둔다
    TMP_Text roundTimerTitle; // 우상단 타이머 창 제목(원작 「현재레벨->」)
    int lastFood = int.MinValue;

    // 미니맵 위 위습 칸 — 왜 이 모양인지는 BuildWispSlots 주석에 있다.
    // 52px인 이유: 44px로 처음 찍었더니 「랜덤유닛」이 칸을 넘쳐 **화면 왼쪽 끝에서 잘렸다**
    // (1920×1080 플레이 캡처). 자동 축소는 최소 글자 크기까지만 줄어들지, 칸에 맞춰 잘라 주지 않는다.
    const float WispSlotSize = 52f;      // 정사각형 한 변(픽셀). 가로·세로에 같은 값을 넣는 것이 정사각형의 근거다
    const float WispSlotGap = 3f;
    // 한 줄 칸 수는 미니맵 칸 폭에 맞춘다(09-29 — 미니맵이 땅 비율로 좁아져 9칸 한 줄이 초상 위까지 넘었다). LayoutWispSlots 참고.
    const int WispSlotsPerRow = 9;       // 첫 배치값(Assets/Data/Wisps 종류 수 9). 실제 줄 폭은 LayoutWispSlots가 다시 정한다
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
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text countText;
        public WispData data;
    }
    TMP_Text roundTimeText;
    TMP_Text teamPanelText;
    RectTransform rightColumn;   // 팀 패널을 위에서부터 쌓는 오른쪽 열(RightColumn()). 보유 아이템은 09-29부터 콘솔 안
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
    readonly int[] slotFull = new int[TeamSlotCount];
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
    // 09-29 사장님: 떠 있던 판매 버튼(상단 메뉴 왼쪽 아래)을 없애고 명령 카드 한 칸으로만 둔다(SellCommandSlot).
    UnitData lastSellButtonUnit;
    bool sellSlotShown;
    bool sellSlotEnabled;
    string sellSlotTooltip;

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
        "일반 몹 강화 코드 레벨 영구 +2 (배율 0.90배→1.00배)\n대가(원딜): 제한됨·초월·불멸·영원 중 한 기를 얻으면 그 네 등급 조합이 잠김",
        "등급 특수함 이상 유닛 로스터 편입 시 랜덤위습 +1\n(원작 포인트값 100 초과 근사, RewardDistributor.unionWisp 씬 배선 필요)",
        "「다른세계 도박」 실패 시 행운의 토큰 +1 (1개→2개)",
        "해루석 피해 250만→300만·마나 700→600\n버스터콜 재사용 100초→66초·마나 500→333",
        "아이템 도박 확률 풀이 13종으로 축소",
    };

    GameObject navigationButtonPanel;
    RectTransform topBarButtons;   // 상단 바 왼쪽 버튼 줄(퀘스트·메뉴·동맹·대화) — 항법 버튼도 여기 선다
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
    // 09-29 4×3: 상점(도박소 9칸)은 유닛 명령이 숨으니 3줄 → 2줄 → 1줄 오른쪽 끝 순. 조합 결과는 3줄 4칸만(UnitRecipeSlotOrder).
    static readonly int[] UnitCommandResultSlotOrder = { 8, 9, 10, 11, 4, 5, 6, 7, 3 };
    static readonly int[] UnitRecipeSlotOrder = { 8, 9, 10, 11 };

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
    float pendingTargetRadius;       // 지점 칸의 반경 — 찍는 동안 커서에 범위 원(TargetAreaIndicator)

    // 상점 칸 단축키(10-02 사장님 「건물 누르고 q 누르고 왼쪽 클릭」). 칸 위치의 워크3 격자 키가 기본이고,
    // 상점이 LaneShopSlotView.hotkey로 따로 정하면 그 키를 쓴다. RefreshShopAffordability가 채운다.
    static readonly char[] ShopGridHotkeys = { 'Q', 'W', 'E', 'R', 'A', 'S', 'D', 'F', 'Z', 'X', 'C', 'V' };
    readonly char[] shopSlotHotkeys = new char[CommandSlotCount];

    // SelectionManager가 읽는다 — 상점 건물을 고른 동안은 유닛 명령 키(A·S·H·V·C·M)를 안 받고,
    // 상점 칸 대상을 찍는 클릭은 선택 클릭으로 안 친다(찍고 나서 건물 선택이 풀리지 않게).
    public static bool ShopSelected { get; private set; }
    public static bool ShopTargetingPending { get; private set; }
    public static int ShopClickConsumedFrame { get; private set; } = -1;
    int targetingStartFrame;        // 칸을 고른 바로 그 클릭이 대상 클릭으로 다시 잡히지 않게

    // 값이 안 바뀌면 문자열을 새로 만들지 않기 위한 마지막 표시값 캐시.
    int lastGold = int.MinValue;
    int lastWood = int.MinValue;
    int lastMana = int.MinValue;
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
    GameObject itemInventoryTitleObject;   // 09-29부터 콘솔 안 아이템 격자(2×4) — 이름은 옛 「제목 줄」 그대로
    RectTransform itemInventoryParent;     // 콘솔의 아이템 칸(BuildUI)
    readonly GameObject[] itemInventoryRowRoots = new GameObject[MaxItemInventorySlots];
    readonly TMP_Text[] itemInventoryRowTexts = new TMP_Text[MaxItemInventorySlots];
    readonly ItemData[] itemInventoryRowItems = new ItemData[MaxItemInventorySlots];
    readonly Dictionary<ItemData, int> itemInventoryCounts = new Dictionary<ItemData, int>();
    readonly List<ItemData> itemInventoryKeys = new List<ItemData>();
    ItemInventory subscribedItemInventory;
    bool itemInventoryDirty = true;

    bool teamPanelInitialized;
    bool teamPanelCollapsed;   // 점수판 접기(사진의 ▼) — 로컬 보기 설정
    int lastTotalEnemyCount = int.MinValue;
    int lastDeathLimit = int.MinValue;   // 팀 현황판 제목의 패배 한계(41R에 내려가면 다시 그린다)
    string lastDifficultyLabel;   // 팀 현황판 머리 줄의 난이도 — 고르기 전엔 null(안 보임)
    readonly int[] lastSlotEnemyCount = new int[TeamSlotCount];
    readonly int[] lastSlotGold = new int[TeamSlotCount];
    readonly int[] lastSlotFull = new int[TeamSlotCount];
    readonly string[] slotNameCache = new string[TeamSlotCount];   // 점수판 이름 칸(칭호+닉네임) — 1초마다 다시 읽는다(문자열 할당을 프레임마다 안 하려고)
    float nextSlotNameRefresh;
    bool lastFullVisible;
    readonly int[] lastSlotWood = new int[TeamSlotCount];
    readonly int[] slotGrace = new int[TeamSlotCount];       // MP: 끊김 유예 남은 초(0 = 연결됨)
    readonly int[] lastSlotGrace = new int[TeamSlotCount];
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
        // 상단 바 「메뉴 (F10)」 — 워크3 기본 단축키
        if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame && gameMenu != null)
        {
            if (gameMenu.activeSelf) CloseGameMenu(); else OpenGameMenu();
        }
        RefreshConsoleLayout();
        RefreshFitGrids();
        RefreshSelectionPanel();
        RefreshTopBar();
        RefreshHeroButtons();
        RefreshTeamPanel();
        RefreshStoryPanel();
        RefreshUnitCommandCards();
        RefreshInventoryPanel();
        RefreshItemInventoryPanel();
        RefreshShopHotkeys();
        RefreshShopTargeting();
        RefreshTargetArea();
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
        // 사진의 회색 돌벽 콘솔 — 돌 타일(직접 그린 근사, Tools/ui/gen_ui_skin.py)을 깐다. 그림이 없으면 옛 청동회색.
        Sprite stone = UiSkin.Get("stone_tile");
        if (stone != null)
        {
            Image barImage = bar.GetComponent<Image>();
            barImage.sprite = stone;
            barImage.type = Image.Type.Tiled;
            barImage.color = Color.white;
        }

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
        // 09-29 워크3 콘솔(사장님 「미니맵이 칸 규격과 안 맞는다」): 미니맵 칸도 다른 칸과 같이 **하단 바 안 5~95%** + 금테.
        //    옛 칸은 HUD 루트에 붙어 윗변이 바 윗선(0.22)과 같아 선 위로 삐져나와 보였다. MinimapTop(0.22)은 이제 위습 칸 바닥만 정한다.
        //    그림은 칸에 늘려 채우지 않고 **땅 비율 그대로 가운데**(BuildMinimap의 AspectRatioFitter) — 늘려 채우면 모자란 축을
        //    카메라가 땅 밖까지 찍어 왼쪽에 빈 남색 띠(바다 판 밖 배경)가 생겼다.
        float commandRight = ConsoleMargin;
        float commandLeft = commandRight + CommandPanelWidth;
        float itemRight = commandLeft + ConsoleMargin;
        float itemLeft = itemRight + ItemPanelWidth;
        float infoRight = itemLeft + ConsoleMargin;

        consoleLeft = (RectTransform)new GameObject("ConsoleLeft", typeof(RectTransform)).transform;
        consoleLeft.SetParent(bar, false);
        SetRightAnchored(consoleLeft, 0.01f, infoRight);
        HorizontalLayoutGroup row = consoleLeft.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = ConsoleGap;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        RectTransform minimapPanel = CreatePanel(consoleLeft, "MinimapPanel", SlotColor);
        minimapLayout = minimapPanel.gameObject.AddComponent<LayoutElement>();
        minimapLayout.preferredWidth = 442f;   // 땅을 재기 전 자리값(옛 폭) — RefreshConsoleLayout이 곧 덮는다
        minimapLayout.flexibleWidth = 0f;
        AddConsoleFrame(minimapPanel);
        RectTransform minimapArea = CreatePanel(minimapPanel, "MinimapArea", Color.clear);
        minimapArea.GetComponent<Image>().raycastTarget = false;
        SetAnchors(minimapArea, Vector2.zero, Vector2.one);
        minimapArea.offsetMin = new Vector2(MinimapInset, MinimapInset);
        minimapArea.offsetMax = new Vector2(-MinimapInset, -MinimapInset);
        minimapCamera = BuildMinimap(minimapArea);

        // 🔴 미니맵 바로 위 위습 개수 (사장님 지시 2026-09-24: 「랜덤위습도 시간지나서 추가되면
        //    미니맵 위쪽에 위습 몇개 있는지 뜨게 해주고 원랜디처럼」).
        //    원작도 미니맵 위에 내 위습 수가 붙어 있다. 라운드 보상으로 위습이 늘어나는데
        //    지금은 맵을 훑어 세야만 알 수 있었다.
        //    ⚠️ 하단 바가 아니라 HUD 루트에 붙인다 — 미니맵 칸 높이(MinimapTop)를 바꿔도 따라오게
        //    아래변을 MinimapTop에 맞춘다. 숫자를 박으면 오늘처럼 미니맵을 옮길 때 어긋난다.
        BuildWispSlots();

        // 미니맵 오른쪽 둥근 단추 5개(기능 없는 자리표시)는 사장님 10-03 지시로 뺐다.

        // 09-29 워크3 콘솔: [미니맵] [초상화] [정보·카드] [아이템 2×4] [명령 4×3]. 오른쪽 두 칸은 고정 폭(ConsoleMargin 주석).
        RectTransform portraitSlot = CreatePanel(consoleLeft, "UnitInfoPortraitSlot", SlotColor);
        portraitLayout = portraitSlot.gameObject.AddComponent<LayoutElement>();
        portraitLayout.preferredWidth = 200f;
        portraitLayout.flexibleWidth = 0f;
        AddConsoleFrame(portraitSlot);
        unitInfoPortraitSlotObject = portraitSlot.gameObject;
        portraitHpBar = BuildPortraitBar(portraitSlot, "PortraitHpBar", "bar_hp", new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.18f), out portraitHpText);
        portraitMpBar = BuildPortraitBar(portraitSlot, "PortraitMpBar", "bar_mp", new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.09f), out portraitMpText);

        GameObject portraitObj = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObj.transform.SetParent(portraitSlot, false);
        RectTransform portraitRect = portraitObj.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.04f, 0.19f);    // 아래 15%는 체력·마나 바 자리(사진: 초상 아래 「77777 / 77777」 · 「180 / 180」)
        portraitRect.anchorMax = new Vector2(0.96f, 0.96f);
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;
        unitInfoPortrait = portraitObj.GetComponent<Image>();
        unitInfoPortrait.sprite = null;
        unitInfoPortrait.color = SlotColor;
        unitInfoPortrait.raycastTarget = false;

        // 초상화 아트가 아직 없다. 빈 사각형으로 두면 "미완성"으로 보인다는 지적(사장님
        // 2026-09-23)에 따라, 등급색 바탕 + 유닛 이름 첫 글자로 채워 최소한 "누구인지"가
        // 읽히게 한다.
        unitInfoPortraitInitial = CreateLabel(portraitObj.transform, "Initial", "");
        unitInfoPortraitInitial.fontSize = 44;
        unitInfoPortraitInitial.fontStyle = FontStyles.Bold;
        unitInfoPortraitInitial.raycastTarget = false;

        // 초상화(09-26 사장님 「원랜디처럼 유닛 서 있는 거」): 모델이 있으면 글자 대신 이 칸에 실제 모델(Idle)을 찍어 보인다.
        GameObject modelObj = new GameObject("PortraitModel", typeof(RectTransform), typeof(RawImage));
        modelObj.transform.SetParent(portraitObj.transform, false);
        RectTransform modelRect = modelObj.GetComponent<RectTransform>();
        modelRect.anchorMin = Vector2.zero;
        modelRect.anchorMax = Vector2.one;
        modelRect.offsetMin = Vector2.zero;
        modelRect.offsetMax = Vector2.zero;
        unitInfoPortraitModel = modelObj.GetComponent<RawImage>();
        unitInfoPortraitModel.raycastTarget = false;
        modelObj.SetActive(false);

        RectTransform infoPanel = CreatePanel(consoleLeft, "UnitInfoPanel", SlotColor);
        LayoutElement infoLayout = infoPanel.gameObject.AddComponent<LayoutElement>();
        infoLayout.minWidth = 0f;
        infoLayout.flexibleWidth = 1f;   // 미니맵·초상이 가져가고 남는 폭 전부
        AddConsoleFrame(infoPanel);

        RectTransform infoTextSlot = CreatePanel(infoPanel, "UnitInfoTextSlot", Color.clear);
        SetAnchors(infoTextSlot, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.93f));
        unitInfoText = CreateLabel(infoTextSlot, "UnitInfoText", "선택된 유닛 없음");
        unitInfoText.alignment = TextAlignmentOptions.TopLeft;
        unitInfoText.fontSize = 24;
        unitInfoText.lineSpacing = 1.15f;
        unitInfoText.textWrappingMode = TextWrappingModes.Normal;

        // 사진의 정보칸 서식: 이름 줄 아래 「공격 아이콘 + 데미지:」 · 「방어 아이콘 + 아머:」 · 「상태:」(버프 아이콘 줄 자리).
        RectTransform statRows = CreatePanel(infoPanel, "UnitStatRows", Color.clear);
        statRows.GetComponent<Image>().raycastTarget = false;
        SetAnchors(statRows, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.70f));
        VerticalLayoutGroup statColumn = statRows.gameObject.AddComponent<VerticalLayoutGroup>();
        statColumn.spacing = 6f;
        statColumn.childControlWidth = true;
        statColumn.childControlHeight = true;
        statColumn.childForceExpandWidth = true;
        statColumn.childForceExpandHeight = true;
        unitDamageText = BuildStatRow(statRows, "icon_attack");
        unitArmorText = BuildStatRow(statRows, "icon_armor");
        unitStatusText = BuildStatRow(statRows, null);
        unitStatRows = statRows.gameObject;
        unitStatRows.SetActive(false);

        BuildSelectionCards(infoPanel);

        RectTransform itemPanel = CreatePanel(bar, "ItemInventoryPanel", SlotColor);
        SetFixedRight(itemPanel, itemRight, ItemPanelWidth);
        // 사진의 엠블럼 자리(인벤토리 6칸 뒤 문장) — 직접 그린 근사 그림. 칸은 반투명이라 문장이 비친다.
        Sprite emblem = UiSkin.Get("inventory_emblem");
        if (emblem != null) { Image itemPanelImage = itemPanel.GetComponent<Image>(); itemPanelImage.sprite = emblem; itemPanelImage.type = Image.Type.Simple; itemPanelImage.color = Color.white; }
        AddConsoleFrame(itemPanel);
        itemInventoryParent = itemPanel;

        RectTransform commandPanel = CreatePanel(bar, "UnitCommandPanel", SlotColor);
        SetFixedRight(commandPanel, commandRight, CommandPanelWidth);
        AddConsoleFrame(commandPanel);
        BuildUnitCommandGrid(commandPanel);

        BuildTopBar();
        BuildHeroButtons();
        BuildStoryPanel();
        BuildTeamPanel();
        BuildTraitButton();
        BuildGambleButtons();
        BuildNavigationUI();
        BuildRerollButton();
        BuildItemInventoryPanel();
        // 왼쪽 유닛 목록은 만들지 않는다. 화면 절반을 덮는데, 무엇을 들고 있는지는
        // 아래 명령 카드 그리드가 이미 보여준다. (F1 디버그 HUD에도 같은 목록이 있다.)

        // 툴팁은 맨 마지막에 만들어야 형제 순서상 가장 나중에 그려져서(항상 위) 다른 패널에 안 가려진다.
        BuildCombineTooltip();
        BuildGameMenu();   // 메뉴 창은 툴팁보다도 위(화면 전체를 덮는다)
    }

    // ───────────── 「메뉴」(PM 09-26: 혼자 하기에도 판을 그만둘 길 · 네트 판은 재접속 B안의 [나가기] 확인 창과 합침) ─────────────
    //   메뉴:   [계속하기] / [처음 화면으로]
    //   확인:   싱글 「처음 화면으로 돌아가면 이번 판은 끝납니다. 돌아갈까요?」
    //           친구 「나가면 유닛이 모두 사라집니다. 나갈까요?」(PM 확정 — [나가기]는 원작 Gone 즉시, 끊김 유예 없음)
    //           방장 「방장이 나가면 모두의 판이 끝납니다. 나갈까요?」
    //   게임은 멈추지 않는다(워크3 같이 하기처럼 — 이 게임엔 일시정지 자체가 없다).

    GameObject gameMenu;
    // 패배 창(DefeatOverlay)이 메뉴와 겹치지 않게 메뉴가 열렸는지 알려 준다(09-29 배포판 피드백). 창 여닫는 길이 여럿이라 상태를 따로 들지 않고 창을 본다.
    static GameObject openableGameMenu;
    public static bool IsGameMenuOpen => openableGameMenu != null && openableGameMenu.activeInHierarchy;
    TMP_Text gameMenuMessage;
    GameObject gameMenuMainButtons;
    GameObject gameMenuConfirmButtons;
    TMP_Text gameMenuConfirmLabel;
    TMP_Text gameMenuSoundLabel;

    void RefreshSoundLabel() => gameMenuSoundLabel.text = GameSound.Enabled ? "소리 끄기" : "소리 켜기";

    public void ToggleSound()
    {
        GameSound.Enabled = !GameSound.Enabled;
        RefreshSoundLabel();
        Debug.Log($"[HUD] 메뉴: 소리 {(GameSound.Enabled ? "켬" : "끔")}");
    }

    void BuildGameMenu()
    {
        RectTransform dim = CreatePanel(transform, "GameMenu", new Color(0f, 0f, 0f, 0.55f));
        SetAnchors(dim, Vector2.zero, Vector2.one);
        gameMenu = dim.gameObject;
        openableGameMenu = gameMenu;

        RectTransform card = CreatePanel(dim, "Card", new Color(0.13f, 0.16f, 0.23f, 0.97f));
        SetAnchors(card, new Vector2(0.29f, 0.38f), new Vector2(0.71f, 0.64f));   // 가장 긴 문구가 한 줄에 들어가는 폭
        AddPanelBorder(card, BorderColor, BorderThickness);

        gameMenuMessage = CreateLabel(card, "Message", "메뉴");
        SetAnchors((RectTransform)gameMenuMessage.transform, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.95f));
        gameMenuMessage.fontSize = 26;

        gameMenuMainButtons = CreateRow(card, "MainButtons");
        CreateMenuButton(gameMenuMainButtons.transform, "ContinueButton", "계속하기", new Color(0.20f, 0.52f, 0.86f, 1f), new Vector2(0.04f, 0f), new Vector2(0.33f, 1f), CloseGameMenu);
        // 소리 켜기/끄기(PM 09-27 — 설정 창이 없어 메뉴 한 줄. GameSound가 PlayerPrefs로 기억한다)
        gameMenuSoundLabel = CreateMenuButton(gameMenuMainButtons.transform, "SoundButton", "", new Color(0.26f, 0.32f, 0.44f, 1f), new Vector2(0.355f, 0f), new Vector2(0.645f, 1f), ToggleSound);
        CreateMenuButton(gameMenuMainButtons.transform, "HomeButton", "처음 화면으로", new Color(0.26f, 0.32f, 0.44f, 1f), new Vector2(0.67f, 0f), new Vector2(0.96f, 1f), ShowGameMenuConfirm);

        gameMenuConfirmButtons = CreateRow(card, "ConfirmButtons");
        gameMenuConfirmLabel = CreateMenuButton(gameMenuConfirmButtons.transform, "ConfirmButton", "나가기", new Color(0.55f, 0.22f, 0.24f, 1f), new Vector2(0.05f, 0f), new Vector2(0.48f, 1f), ConfirmLeaveGame);
        CreateMenuButton(gameMenuConfirmButtons.transform, "CancelButton", "취소", new Color(0.26f, 0.32f, 0.44f, 1f), new Vector2(0.52f, 0f), new Vector2(0.95f, 1f), CloseGameMenu);

        gameMenu.SetActive(false);
    }

    static GameObject CreateRow(RectTransform card, string name)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(card, false);
        SetAnchors((RectTransform)row.transform, new Vector2(0f, 0.12f), new Vector2(1f, 0.42f));
        return row;
    }

    static TMP_Text CreateMenuButton(Transform parent, string name, string label, Color color, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = CreatePanel(parent, name, color);
        SetAnchors(rect, min, max);
        rect.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        TMP_Text text = CreateLabel(rect, name + "Label", label);
        text.fontSize = 24;
        text.raycastTarget = false;
        return text;
    }

    public void OpenGameMenu()
    {
        gameMenuMessage.text = "메뉴";
        RefreshSoundLabel();
        gameMenuMainButtons.SetActive(true);
        gameMenuConfirmButtons.SetActive(false);
        gameMenu.SetActive(true);
        gameMenu.transform.SetAsLastSibling();
    }

    void CloseGameMenu() => gameMenu.SetActive(false);

    public void ShowGameMenuConfirm()
    {
        if (!gameMenu.activeSelf) OpenGameMenu();
        NetLauncher launcher = NetLauncher.Instance;   // MP: 네트 판이면 창구가 있다(혼자 하기는 창구를 없애고 시작한다)
        bool online = launcher != null && launcher.InRoom;
        gameMenuMessage.text = !online ? "처음 화면으로 돌아가면 이번 판은 끝납니다. 돌아갈까요?"
            : launcher.IsHost ? "방장이 나가면 모두의 판이 끝납니다. 나갈까요?"
            : "나가면 유닛이 모두 사라집니다. 나갈까요?";
        gameMenuConfirmLabel.text = online ? "나가기" : "처음 화면으로";
        gameMenuMainButtons.SetActive(false);
        gameMenuConfirmButtons.SetActive(true);
    }

    void ConfirmLeaveGame()
    {
        gameMenu.SetActive(false);
        NetLauncher launcher = NetLauncher.Instance;
        if (launcher != null && launcher.InRoom)
        {
            launcher.Leave();   // MP: 방장은 방을 닫고, 친구는 [나가기] 예고 뒤 나간다 — 둘 다 첫 화면으로
            return;
        }
        // 혼자 하기: 첫 화면(빌드의 0번 씬 NetBoot)으로. 게임 씬만 있는 빌드(에디터에서 게임 씬을 바로 켠 경우 등)면 이 판을 다시 시작한다.
        int current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        int target = current != 0 && UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings > 1 ? 0 : current;
        Debug.Log($"[HUD] 메뉴: 처음 화면으로 — 씬 {target}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(target);
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

    // 사장님 인게임 사진(Docs/reference/ui/원랜디_인게임_01.png) 비율: 상단 바는 화면 맨 위 한 줄(높이 ≈3.2%),
    // 왼쪽 버튼 「퀘스트·메뉴(F10)·동맹(F11)·대화(F12)」 · 가운데 낮밤 시계 · 오른쪽 금화·나무·고기(=특성 포인트) 아이콘+숫자 + 맵 이름.
    // 라운드 시간은 이제 우상단 타이머 창(RightColumn 맨 위, 사진의 「보스 제한시간」 자리) — 문서 UI_ORIGINAL_STYLE.md ⑤.
    const float TopBarBottom = 0.968f;

    void BuildTopBar()
    {
        RectTransform topBar = CreatePanel(transform, "TopBar", new Color(0f, 0f, 0f, 0.55f));
        SetAnchors(topBar, new Vector2(0f, TopBarBottom), new Vector2(1f, 1f));

        // 왼쪽 버튼 줄(항법 버튼도 여기 — 우리만의 기능이라 원작 4버튼 뒤에 붙인다)
        RectTransform menuButtonsPanel = CreatePanel(topBar, "TopBarButtons", Color.clear);
        topBarButtons = menuButtonsPanel;
        SetAnchors(menuButtonsPanel, new Vector2(0f, 0.04f), new Vector2(0.455f, 0.96f));
        HorizontalLayoutGroup layout = menuButtonsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // 퀘스트: 원작 맵이 퀘스트 로그를 안 쓴다(j에 CreateQuest 0건) → 버튼만 두고 흐리게(사진의 첫 버튼도 흐림).
        TMP_Text questLabel = CreateTopBarButton(menuButtonsPanel, "QuestButton", "퀘스트", 130f);
        questLabel.alpha = 0.45f;
        questLabel.transform.parent.GetComponent<Button>().interactable = false;
        TMP_Text menuLabel = CreateTopBarButton(menuButtonsPanel, "MenuButton", "메뉴 (F10)", 130f);
        menuLabel.transform.parent.GetComponent<Button>().onClick.AddListener(OpenGameMenu);
        // 동맹/대화는 동작 없음 — 원작 배치만 재현한다. 메뉴는 [계속하기]/[처음 화면으로](BuildGameMenu).
        CreateTopBarButton(menuButtonsPanel, "AllianceButton", "동맹 (F11)", 130f);
        CreateTopBarButton(menuButtonsPanel, "ChatButton", "대화 (F12)", 130f);

        // 가운데 낮밤 시계(장식)
        RectTransform clock = CreatePanel(topBar, "ClockOrb", Color.clear);
        SetAnchors(clock, new Vector2(0.47f, -0.55f), new Vector2(0.53f, 1f));
        clock.GetComponent<Image>().raycastTarget = false;
        Image clockImage = clock.GetComponent<Image>();
        if (UiSkin.Apply(clockImage, "clock_orb")) clockImage.preserveAspect = true;

        // 플레이어 마나(원작 상단 바엔 없음 — 도움소 스킬용) 시계 왼쪽에 작게
        manaText = CreateTopBarResource(topBar, "ManaPanel", null, 0.395f, 0.465f, new Color(0.45f, 0.65f, 1f));

        // 자원 셋: 금화·나무·고기 (아이콘 + 숫자)
        goldText = CreateTopBarResource(topBar, "GoldPanel", "icon_gold", 0.575f, 0.685f, Color.white);
        woodText = CreateTopBarResource(topBar, "WoodPanel", "icon_lumber", 0.695f, 0.80f, Color.white);
        foodText = CreateTopBarResource(topBar, "FoodPanel", "icon_food", 0.81f, 0.90f, Color.white);

        // 맵 이름(사진 「원랜디 시즌 3」 자리 — 사장님 확정: 「구랜디」)
        RectTransform mapName = CreatePanel(topBar, "MapNamePanel", Color.clear);
        SetAnchors(mapName, new Vector2(0.905f, 0.08f), new Vector2(0.995f, 0.92f));
        TMP_Text mapLabel = CreateLabel(mapName, "MapNameText", "구랜디");
        mapLabel.fontSize = 18;
        mapLabel.color = new Color(0.5f, 0.88f, 1f);

        // 우상단 스택(RightColumn): 라운드 타이머 창 → 보스·신세계 타이머 창 → (BuildTeamPanel) 점수판. 사진의 「보스 제한시간」/「유닛 카운트」 자리.
        RectTransform roundWindow = CreatePanel(RightColumn(), "RoundTimerWindow", new Color(0.11f, 0.05f, 0.07f, 0.94f));
        UiSkin.Apply(roundWindow.GetComponent<Image>(), "timer_frame_9s", new Color(0.11f, 0.05f, 0.07f, 0.94f));
        roundWindow.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        roundTimerTitle = CreateLabel(roundWindow, "RoundTimerTitle", "현재레벨->");
        SetAnchors(roundTimerTitle.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.62f, 1f));
        roundTimerTitle.alignment = TextAlignmentOptions.Left;
        roundTimerTitle.fontSize = 20;
        roundTimerTitle.color = new Color(1f, 0.28f, 0.28f);
        roundTimeText = CreateLabel(roundWindow, "RoundTimeText", "-");
        SetAnchors(roundTimeText.rectTransform, new Vector2(0.55f, 0f), new Vector2(0.96f, 1f));
        roundTimeText.alignment = TextAlignmentOptions.Right;
        roundTimeText.fontSize = 22;

        // 보스 제한시간·신세계 대기 타이머(원작 타이머 창 제목, 알림 묶음 3)도 같은 스택에 — 켜질 때만 보인다.
        RectTransform extraTimerPanel = CreatePanel(RightColumn(), "ExtraTimerPanel", new Color(0.11f, 0.05f, 0.07f, 0.94f));
        UiSkin.Apply(extraTimerPanel.GetComponent<Image>(), "timer_frame_9s", new Color(0.11f, 0.05f, 0.07f, 0.94f));
        extraTimerPanel.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
        extraTimerText = CreateLabel(extraTimerPanel, "ExtraTimerText", "");
        SetAnchors(extraTimerText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
        extraTimerText.alignment = TextAlignmentOptions.Left;
        extraTimerText.fontSize = 18;
        extraTimerText.textWrappingMode = TextWrappingModes.NoWrap;
        extraTimerObject = extraTimerPanel.gameObject;
        extraTimerObject.SetActive(false);
    }

    // 상단 바 자원 칸: 어두운 칸 + 왼쪽 아이콘 + 오른쪽 정렬 숫자. 아이콘 이름이 null이면 글자만(마나).
    static TMP_Text CreateTopBarResource(RectTransform topBar, string name, string iconName, float x0, float x1, Color textColor)
    {
        RectTransform panel = CreatePanel(topBar, name, new Color(0.02f, 0.04f, 0.09f, 0.95f));
        SetAnchors(panel, new Vector2(x0, 0.08f), new Vector2(x1, 0.92f));
        UiSkin.Apply(panel.GetComponent<Image>(), "topbar_resource_9s", new Color(0.02f, 0.04f, 0.09f, 0.95f));
        panel.GetComponent<Image>().raycastTarget = false;
        if (iconName != null)
        {
            RectTransform icon = CreatePanel(panel, "Icon", Color.clear);
            SetAnchors(icon, new Vector2(0.03f, 0.1f), new Vector2(0.2f, 0.9f));
            Image iconImage = icon.GetComponent<Image>();
            iconImage.raycastTarget = false;
            if (UiSkin.Apply(iconImage, iconName)) iconImage.preserveAspect = true;
        }
        TMP_Text label = CreateLabel(panel, "Value", "0");
        SetAnchors(label.rectTransform, new Vector2(iconName != null ? 0.2f : 0.05f, 0f), new Vector2(0.95f, 1f));
        label.alignment = TextAlignmentOptions.Right;
        label.fontSize = 20;
        label.color = textColor;
        label.raycastTarget = false;
        return label;
    }

    static TMP_Text CreateTopBarButton(Transform parent, string name, string label, float width = 90f)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        if (!UiSkin.Apply(image, "topbar_button_9s", new Color(1f, 1f, 1f, 0.15f))) image.color = new Color(1f, 1f, 1f, 0.15f);
        obj.GetComponent<LayoutElement>().preferredWidth = width;

        TMP_Text text = CreateLabel(obj.transform, name + "Label", label);
        text.fontSize = 18;
        text.color = new Color(0.81f, 0.88f, 1f);
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

            gambleButtonTexts[i].text = $"{data.DisplayName} 시전({option.abilityId})\n(목재 {option.woodCost} 소모, 보유 {wood}, 성공 {option.successChance:P0})";
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

    // "유닛 판매"(2026-09-06, PM 지시) — 09-29 사장님 지시로 떠 있던 버튼(상단 메뉴 왼쪽 아래 0.51~0.70 × 0.78~0.83)을
    // 없애고 명령 카드 한 칸(SellCommandSlot)으로만 둔다. 유닛을 고르면 보이고, 한 기 + 판매 보상이 있을 때만 눌린다.
    // 보상 문구는 옛 버튼 글자에 있던 것을 칸 툴팁으로 옮겼다. 파는 본체(ExecuteSellOn)·확인 없음은 그대로.
    static bool IsSellable(UnitData data) =>
        data != null && (data.sellRewardWisp != null || data.sellRewardTraitPoints > 0 || data.sellRewardWood > 0 ||
                         data.sellTriggersItemGamblePool != null || data.sellRewardEveryNSells > 0);

    void RefreshSellButton()
    {
        if (unitCommandSlotRoots[SellCommandSlot] == null) return;
        // 상점을 고른 동안 이 칸은 상점 칸이다(RebuildShopSlots) — 건드리지 않는다.
        if (currentShop as Object != null) { sellSlotShown = false; lastSellButtonUnit = null; return; }

        SelectionManager selection = Selection;
        int count = selection != null ? selection.Selected.Count : 0;
        bool anyUnit = false;
        for (int i = 0; i < count && !anyUnit; i++)
            anyUnit = selection.Selected[i] != null && selection.Selected[i].GetComponent<UnitIdentity>() != null;
        if (!anyUnit) { HideSellButton(); return; }

        UnitData data = count == 1 && selection.Selected[0].TryGetComponent(out UnitIdentity identity) ? identity.Data : null;
        bool sellable = IsSellable(data);
        if (sellSlotShown && sellable == sellSlotEnabled && data == lastSellButtonUnit) return;
        sellSlotShown = true;
        sellSlotEnabled = sellable;
        lastSellButtonUnit = data;

        unitCommandSlotNames[SellCommandSlot].text = "판매";
        unitCommandSlotHotkeys[SellCommandSlot].text = "";
        Color color = UnitCommandDefaultColor;
        color.a = sellable ? 1f : 0.35f;   // 조합 칸과 같은 관례 — 못 누르면 흐리게
        unitCommandSlotBackgrounds[SellCommandSlot].color = color;
        // 바탕만 흐리면 흰 글씨가 그대로라 눌리는 칸처럼 보였다(09-29 촬영) — 글씨도 흐리게.
        unitCommandSlotNames[SellCommandSlot].color = sellable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        unitCommandSlotButtons[SellCommandSlot].interactable = sellable;

        if (!sellable)
        {
            sellSlotTooltip = count == 1 ? "판매할 수 없는 유닛입니다(판매 보상이 없습니다)." : "판매는 한 기만 골랐을 때 됩니다.";
            return;
        }

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
        sellSlotTooltip = $"판매\n({rewardDesc})";
    }

    void HideSellButton()
    {
        lastSellButtonUnit = null;
        if (!sellSlotShown) return;
        sellSlotShown = false;
        sellSlotEnabled = false;
        unitCommandSlotNames[SellCommandSlot].text = "";
        unitCommandSlotNames[SellCommandSlot].color = Color.white;
        unitCommandSlotBackgrounds[SellCommandSlot].color = Color.clear;
        unitCommandSlotButtons[SellCommandSlot].interactable = false;
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
            int seller = owner.OwnerId;
            bool chancy = identity.Data.sellRewardWispChance < 1f;   // 안흔함(A0B8) — 원작 50% 성공/실패 문구가 있다
            if (identity.Data.sellRewardWisp != null && RewardDistributor.Instance != null &&
                Random.value < identity.Data.sellRewardWispChance)
            {
                List<WispReward> reward = new List<WispReward>
                {
                    new WispReward { wisp = identity.Data.sellRewardWisp, count = identity.Data.sellRewardWispCount }
                };
                RewardDistributor.Instance.GrantWisps(context, reward);
                // 원작 j:13162(본인 4초) — 알림 묶음 13/13, GAP 109
                if (chancy) PlayerNotification.Show(seller, "<color=#FF8200>안흔함 판매성공!</color>     <color=#FFD700>1기의 랜덤위습 획득!</color>", 4f);
            }
            else if (chancy && identity.Data.sellRewardWisp != null)
                PlayerNotification.Show(seller, "<color=#FF0000>안흔함 판매실패! ㅠㅠ</color>", 4f);   // j:13164

            if (identity.Data.sellRewardWood > 0 && context.ResourceWallet != null &&
                Random.value < identity.Data.sellRewardWoodChance)
            {
                context.ResourceWallet.Add(ResourceType.Wood, identity.Data.sellRewardWood);
                PlayerNotification.Show(seller, $"<color=#20B2AA>{identity.Data.sellRewardWood}개의 추가목재 획득!</color>", 4f);   // j:13153/13165
            }

            // 흔함 9종 판매 누적(A09G) — 플레이어 전체 공유 카운터가 N번째에 도달할 때만
            // 위습을 준다(UnitUpgrades.RegisterCommonSell 참고). RewardDistributor.
            // GrantWisps는 count를 1로 고정 — 이 보상은 항상 위습 1기다.
            if (identity.Data.sellRewardEveryNSells > 0 && identity.Data.sellRewardEveryNWisp != null &&
                RewardDistributor.Instance != null && context.UnitUpgrades != null)
            {
                int everyN = identity.Data.sellRewardEveryNSells;
                if (context.UnitUpgrades.RegisterCommonSell(everyN))
                {
                    List<WispReward> reward = new List<WispReward>
                    {
                        new WispReward { wisp = identity.Data.sellRewardEveryNWisp, count = 1 }
                    };
                    RewardDistributor.Instance.GrantWisps(context, reward);
                    // 원작 j:13152(본인 4초) — 알림 묶음 13/13, GAP 108
                    PlayerNotification.Show(seller, $"누적 {everyN}포인트<color=#FF8200>를 획득하여</color> <color=#FFD700> 1기의 랜덤위습 획득!</color>", 4f);
                    // 원작 j:13153: 같은 순간 35% 확률로 목재 1 추가(「1개의 추가목재 획득!」, 위 일반 목재 보상과 같은 문구).
                    if (identity.Data.sellRewardEveryNWood > 0 && context.ResourceWallet != null && Random.value < identity.Data.sellRewardEveryNWoodChance)
                    {
                        context.ResourceWallet.Add(ResourceType.Wood, identity.Data.sellRewardEveryNWood);
                        PlayerNotification.Show(seller, $"<color=#20B2AA>{identity.Data.sellRewardEveryNWood}개의 추가목재 획득!</color>", 4f);
                    }
                }
                else
                    PlayerNotification.Show(seller, $"{context.UnitUpgrades.CommonSellCount}<color=#FF8200> 포인트 적립!</color>", 4f);   // j:13155
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
        SelectionManager selection = Selection;
        if (selection == null || selection.Selected.Count != 1) return;

        Selectable single = selection.Selected[0];
        if (single == null || !single.TryGetComponent(out UniqueRerollAbility reroll) ||
            !single.TryGetComponent(out OwnedByPlayer owner)) return;

        // MP: 멀티 클라는 호스트에 요청만(목재·확률·교체는 호스트 실물의 리롤 능력이). 결과 문구는 알림으로 돌아온다.
        if (!GameAuthority.IsServer) { NetCommands.RequestHudUnitAction(NetHudAction.Reroll, single, 0); return; }

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
        // 09-29 워크3 콘솔: 오른쪽 기둥(팀 패널 밑) 글 목록 → 콘솔 안 2열 × 4줄 칸(워크3 인벤토리 자리). 아이콘 아트가 없어
        //    칸엔 이름 × 개수를 적고, 설명은 전처럼 호버 툴팁으로. 제목 줄은 없앴다(워크3 인벤토리에도 없다).
        GridLayoutGroup grid = AddFitGrid(itemInventoryParent, "ItemInventoryGrid", 2, MaxItemInventorySlots / 2, 6f, 4f, false);
        itemInventoryTitleObject = grid.gameObject;

        for (int i = 0; i < MaxItemInventorySlots; i++)
        {
            RectTransform row = CreatePanel(grid.transform, $"ItemInventoryRow{i}", new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.72f));
            AddPanelBorder(row, BorderInnerColor, 1f);
            itemInventoryRowRoots[i] = row.gameObject;

            TMP_Text label = CreateLabel(row, $"ItemInventoryRowText{i}", "");
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 15f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            itemInventoryRowTexts[i] = label;

            int capturedIndex = i;
            EventTrigger trigger = row.gameObject.AddComponent<EventTrigger>();
            AddTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnItemInventoryRowHoverEnter(capturedIndex));
            AddTriggerEntry(trigger, EventTriggerType.PointerExit, _ => HideCombineTooltip());
            AddTriggerEntry(trigger, EventTriggerType.PointerClick, _ => OnItemInventoryRowClicked(capturedIndex));   // 좌클릭 = 사용(사용형 아이템만)

            row.gameObject.SetActive(false);
        }
    }

    // 사용형 아이템(ItemData.useKind) 칸 클릭 — 원작 Trig_item_up2(사용 이벤트). 서버/싱글은 바로, 클라는 RPC로 요청한다.
    void OnItemInventoryRowClicked(int index)
    {
        if (index < 0 || index >= MaxItemInventorySlots) return;
        ItemData item = itemInventoryRowItems[index];
        if (item == null || item.useKind == ItemUseKind.None) return;
        if (item.useKind == ItemUseKind.HeroTransform)
        {
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "영웅 변신 — 아직 사용할 수 없습니다.", 3f);
            return;
        }
        if (GameAuthority.IsServer) RewardDistributor.Instance?.UseItem(PlayerContext.Local, item.useKind);
        else NetCommands.RequestUseItem(item.useKind);
    }

    void OnItemInventoryRowHoverEnter(int index)
    {
        if (index < 0 || index >= MaxItemInventorySlots || itemInventoryRowRoots[index] == null) return;

        ItemData item = itemInventoryRowItems[index];
        if (item == null) return;

        string text = !string.IsNullOrEmpty(item.tooltipText) ? item.tooltipText : item.itemName;
        if (item.useKind == ItemUseKind.WispBundle || item.useKind == ItemUseKind.AncientShip) text += "\n(클릭하면 사용)";
        else if (item.useKind == ItemUseKind.HeroTransform) text += "\n(영웅 변신 — 아직 사용할 수 없음)";
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
            // 09-29 콘솔 격자: 빈 칸도 켜 둔다(끄면 격자가 당겨 붙어 칸 수가 안 보인다 — 워크3 인벤토리도 빈 칸이 보인다).
            if (!itemInventoryRowRoots[i].activeSelf) itemInventoryRowRoots[i].SetActive(true);
            itemInventoryRowRoots[i].GetComponent<Image>().color = used ? new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.82f) : new Color(SlotColor.r, SlotColor.g, SlotColor.b, 0.55f);   // 반투명 — 엠블럼이 비친다

            if (!used)
            {
                itemInventoryRowItems[i] = null;
                itemInventoryRowTexts[i].text = "";
                continue;
            }

            ItemData item = itemInventoryKeys[i];
            itemInventoryRowItems[i] = item;
            bool usable = item.useKind == ItemUseKind.WispBundle || item.useKind == ItemUseKind.AncientShip;
            itemInventoryRowTexts[i].text = $"{item.itemName} x{itemInventoryCounts[item]}" + (usable ? " [사용]" : "");
            if (usable) itemInventoryRowRoots[i].GetComponent<Image>().color = Color.Lerp(new Color(ButtonColor.r, ButtonColor.g, ButtonColor.b, 0.82f), new Color(0.9f, 0.75f, 0.3f, 0.82f), 0.35f);
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
        navigationButtonText = CreateTopBarButton(topBarButtons, "NavigationButton", "항법 선택", 190f);
        navigationButtonText.enableAutoSizing = true;
        navigationButtonText.fontSizeMin = 12f;
        navigationButtonText.fontSizeMax = 18f;
        navigationButtonText.textWrappingMode = TextWrappingModes.NoWrap;

        GameObject panel = navigationButtonText.transform.parent.gameObject;
        // 상단 바 왼쪽 줄에서는 원작 4버튼(퀘스트·메뉴·동맹·대화) 뒤 맨 끝에 둔다(사진 배치 — 항법은 우리만의 기능).
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
        SetAnchors(rightColumn, new Vector2(0.73f, BottomBarHeight + 0.01f), new Vector2(0.99f, TopBarBottom - 0.003f));

        VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 5.4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rightColumn;
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
        UiSkin.Apply(teamPanel.GetComponent<Image>(), "multiboard_frame_9s", new Color(0f, 0f, 0f, 0.6f));   // 원작 멀티보드: 금테 짙은 판
        VerticalLayoutGroup fit = teamPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        fit.padding = new RectOffset(8, 8, 4, 6);
        fit.childControlWidth = true;
        fit.childControlHeight = true;
        fit.childForceExpandWidth = true;
        fit.childForceExpandHeight = false;

        teamPanelText = CreateLabel(teamPanel, "TeamPanelText", "");
        teamPanelText.alignment = TextAlignmentOptions.TopLeft;
        teamPanelText.fontSize = 18;
        teamPanelText.lineSpacing = 1.1f;
        teamPanelText.textWrappingMode = TextWrappingModes.NoWrap;
        teamPanelText.overflowMode = TextOverflowModes.Overflow;
        teamPanelText.raycastTarget = false;

        // 접기 버튼(워크3 멀티보드 기본 기능, 사진의 ▼) — 접으면 제목 줄만 남는다. 기호는 폰트 누락을 피해 ASCII.
        GameObject collapse = new GameObject("TeamPanelCollapse", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        collapse.transform.SetParent(teamPanel, false);
        collapse.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform collapseRect = (RectTransform)collapse.transform;
        collapseRect.anchorMin = collapseRect.anchorMax = new Vector2(1f, 1f);
        collapseRect.pivot = new Vector2(1f, 1f);
        collapseRect.anchoredPosition = new Vector2(-4f, -3f);
        collapseRect.sizeDelta = new Vector2(26f, 22f);
        Image collapseImage = collapse.GetComponent<Image>();
        if (!UiSkin.Apply(collapseImage, "button_navy_9s", new Color(0.12f, 0.2f, 0.45f, 1f))) collapseImage.color = new Color(0.12f, 0.2f, 0.45f, 1f);
        TMP_Text collapseLabel = CreateLabel(collapse.transform, "Label", "-");
        collapseLabel.fontSize = 18;
        collapseLabel.raycastTarget = false;
        collapse.GetComponent<Button>().onClick.AddListener(() =>
        {
            teamPanelCollapsed = !teamPanelCollapsed;
            collapseLabel.text = teamPanelCollapsed ? "+" : "-";
            teamPanelInitialized = false;   // 다음 프레임에 다시 그린다
        });
    }

    // ───────────── 영웅 단추(사진: 왼쪽 아래 초상 아이콘 + 레벨) ─────────────
    // 대상 = 내 유닛 중 원작에서 영웅 클래스(H 아이디 + 영웅 베이스)인 유닛에 대응하는 것(UnitHeroTable — 로스터 2개뿐, 보고 항목).
    // 누르면 그 유닛을 고르고 카메라를 옮긴다. 그림은 직접 그린 틀(icon_hero_frame) 위에 이름 첫 글자 + 레벨.
    const int MaxHeroButtons = 6;
    RectTransform heroButtonColumn;
    readonly RectTransform[] heroButtonRoots = new RectTransform[MaxHeroButtons];
    readonly TMP_Text[] heroButtonTexts = new TMP_Text[MaxHeroButtons];
    readonly UnitIdentity[] heroButtonUnits = new UnitIdentity[MaxHeroButtons];
    float nextHeroRefresh;

    void BuildHeroButtons()
    {
        GameObject obj = new GameObject("HeroButtons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        obj.transform.SetParent(transform, false);
        heroButtonColumn = (RectTransform)obj.transform;
        heroButtonColumn.anchorMin = heroButtonColumn.anchorMax = new Vector2(0.002f, 0.40f);   // 사진: 콘솔 위 왼쪽(y≈65~71%) — 위습 칸 위로 쌓는다
        heroButtonColumn.pivot = new Vector2(0f, 0f);
        heroButtonColumn.sizeDelta = new Vector2(66f, 0f);
        VerticalLayoutGroup column = obj.GetComponent<VerticalLayoutGroup>();
        column.spacing = 4f;
        column.childAlignment = TextAnchor.LowerLeft;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        obj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < MaxHeroButtons; i++)
        {
            GameObject b = new GameObject($"HeroButton{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            b.transform.SetParent(heroButtonColumn, false);
            b.GetComponent<LayoutElement>().preferredHeight = 64f;
            Image frame = b.GetComponent<Image>();
            if (!UiSkin.Apply(frame, "icon_hero_frame", new Color(0.08f, 0.07f, 0.1f, 1f))) frame.color = new Color(0.08f, 0.07f, 0.1f, 1f);
            int captured = i;
            b.GetComponent<Button>().onClick.AddListener(() => OnHeroButtonClicked(captured));
            TMP_Text label = CreateLabel(b.transform, "Label", "");
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
            label.fontSize = 17;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            heroButtonRoots[i] = (RectTransform)b.transform;
            heroButtonTexts[i] = label;
            b.SetActive(false);
        }
    }

    void RefreshHeroButtons()
    {
        if (heroButtonColumn == null || Time.unscaledTime < nextHeroRefresh) return;
        nextHeroRefresh = Time.unscaledTime + 0.4f;
        int n = 0;
        foreach (UnitIdentity unit in UnitIdentity.Active)
        {
            if (n >= MaxHeroButtons) break;
            if (unit == null || unit.OwnerId != LocalPlayer.LocalPlayerId || !UnitHeroTable.IsHero(unit.Data)) continue;
            UnitAttacker attacker = unit.GetComponentInChildren<UnitAttacker>();
            int level = attacker != null ? attacker.CharacterLevel : 1;
            string initial = unit.Data.unitName.Length > 0 ? unit.Data.unitName.Substring(0, 1) : "?";
            heroButtonUnits[n] = unit;
            heroButtonTexts[n].text = $"<size=26>{initial}</size>\n<size=15><color=#F3D27A>Lv.{level}</color></size>";
            if (!heroButtonRoots[n].gameObject.activeSelf) heroButtonRoots[n].gameObject.SetActive(true);
            n++;
        }
        for (int i = n; i < MaxHeroButtons; i++)
        {
            heroButtonUnits[i] = null;
            if (heroButtonRoots[i].gameObject.activeSelf) heroButtonRoots[i].gameObject.SetActive(false);
        }
    }

    void OnHeroButtonClicked(int index)
    {
        UnitIdentity unit = index >= 0 && index < MaxHeroButtons ? heroButtonUnits[index] : null;
        if (unit == null) return;
        if (unit.TryGetComponent(out Selectable selectable) && Selection != null) Selection.SelectOnly(selectable);
        RtsCameraController camera = FindFirstObjectByType<RtsCameraController>();
        if (camera != null) camera.MoveTo(unit.transform.position);
    }

    // 초상 아래 바 한 줄(체력=초록, 마나=파랑): 어두운 바탕 + 채움 + 가운데 글자 「현재 / 최대」.
    static Image BuildPortraitBar(RectTransform parent, string name, string spriteName, Vector2 min, Vector2 max, out TMP_Text label)
    {
        RectTransform back = CreatePanel(parent, name, new Color(0f, 0f, 0f, 0.9f));
        SetAnchors(back, min, max);
        back.GetComponent<Image>().raycastTarget = false;
        RectTransform fill = CreatePanel(back, "Fill", Color.white);
        SetAnchors(fill, Vector2.zero, Vector2.one);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.raycastTarget = false;
        fillImage.color = spriteName == "bar_hp" ? new Color(0.2f, 0.75f, 0.28f) : new Color(0.18f, 0.38f, 0.9f);
        Sprite sprite = UiSkin.Get(spriteName);
        if (sprite != null) { fillImage.sprite = sprite; fillImage.color = Color.white; }
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        label = CreateLabel(back, "Text", "");
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
        label.fontSize = 17;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        back.gameObject.SetActive(false);
        return fillImage;
    }

    // 정보칸 스탯 한 줄: 왼쪽 아이콘(없으면 빈 자리) + 글자. 사진의 「데미지:」「아머:」「상태:」 줄.
    static TMP_Text BuildStatRow(RectTransform parent, string iconName)
    {
        RectTransform row = CreatePanel(parent, "StatRow", Color.clear);
        row.GetComponent<Image>().raycastTarget = false;
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        RectTransform icon = CreatePanel(row, "Icon", Color.clear);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.raycastTarget = false;
        LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 40f;
        iconLayout.flexibleWidth = 0f;
        if (iconName != null && UiSkin.Apply(iconImage, iconName)) iconImage.preserveAspect = true;
        TMP_Text text = CreateLabel(row, "Text", "");
        text.alignment = TextAlignmentOptions.Left;
        text.fontSize = 22;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        return text;
    }

    // 초상 아래 바 표시: hp=null이면 둘 다 숨김. mana가 null이면 마나 줄만 숨김(원작에서 마나 없는 유닛).
    void SetPortraitBars(int? hp, int? mana, int manaCap)
    {
        if (portraitHpBar == null) return;
        bool hpOn = hp.HasValue;
        bool mpOn = hpOn && mana.HasValue;
        if (portraitHpBar.transform.parent.gameObject.activeSelf != hpOn) portraitHpBar.transform.parent.gameObject.SetActive(hpOn);
        if (portraitMpBar.transform.parent.gameObject.activeSelf != mpOn) portraitMpBar.transform.parent.gameObject.SetActive(mpOn);
        if (hpOn) { portraitHpBar.fillAmount = 1f; portraitHpText.text = $"{hp.Value} / {hp.Value}"; }   // 플레이어 유닛은 피해를 안 받는다 — 항상 가득
        if (mpOn)
        {
            portraitMpBar.fillAmount = manaCap > 0 ? Mathf.Clamp01(mana.Value / (float)manaCap) : 1f;
            portraitMpText.text = $"{mana.Value} / {manaCap}";
        }
    }

    static MinimapCamera BuildMinimap(RectTransform parent)
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

        // 땅 비율 그대로 칸 안 가운데(09-29). 비율 값은 MinimapCamera가 땅을 잰 뒤(Start) 넣는다 — 그 전엔 칸을 꽉 채운다.
        AspectRatioFitter fitter = obj.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = parent.rect.height > 1f ? parent.rect.width / parent.rect.height : 2f;

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
        return obj.GetComponent<MinimapCamera>();
    }

    RectTransform consoleLeft;
    LayoutElement minimapLayout;
    LayoutElement portraitLayout;
    MinimapCamera minimapCamera;
    Vector2 appliedConsoleLayout;   // (칸 높이, 땅 비율) — 둘 중 하나라도 바뀌면 다시 잡는다

    // 미니맵 칸 = 그림(09-29). 땅 비율은 MinimapCamera가 Awake에서 재고 Start에서 한 번 더 잰다 — 둘 다 첫 그림보다 앞이라
    //   첫 프레임 Update에서 잡으면 화면에 옛 폭이 한 번도 안 나온다. 칸 높이는 해상도가 바뀌면 달라진다.
    void RefreshConsoleLayout()
    {
        if (consoleLeft == null || minimapCamera == null) return;
        float height = consoleLeft.rect.height;
        float aspect = minimapCamera.GroundAspect;
        if (height <= 1f || aspect <= 0f) return;
        Vector2 key = new Vector2(height, aspect);
        if (key == appliedConsoleLayout) return;
        appliedConsoleLayout = key;

        float minimapWidth = Mathf.Round((height - 2f * MinimapInset) * aspect + 2f * MinimapInset);
        minimapLayout.preferredWidth = minimapWidth;
        portraitLayout.preferredWidth = Mathf.Round(height * PortraitAspect);
        if (minimapCamera.TryGetComponent(out AspectRatioFitter fitter)) fitter.aspectRatio = aspect;
        LayoutRebuilder.MarkLayoutForRebuild(consoleLeft);
        LayoutWispSlots(minimapWidth);
    }

    // 카드 12개를 미리 만들어두고 선택이 바뀔 때만 켜고 끈다 — 매 프레임 새로 만들지 않는다.
    void BuildSelectionCards(RectTransform parent)
    {
        // 6열 × 2줄 정사각 — 칸 크기는 정보칸 크기에서 잰다(FitGrid).
        GridLayoutGroup grid = AddFitGrid(parent, "SelectionCardsPanel", SelectionCardColumns, SelectionCardRows, 8f, 6f, true);
        unitCardsPanel = grid.gameObject;

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
        nameText.enableAutoSizing = true;   // 카드가 커졌다(58 → 약 90). 두 줄 이름이 칸 크기를 따라가게
        nameText.fontSizeMin = 11f;
        nameText.fontSizeMax = 15f;
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
        else if (index == SellCommandSlot)
        {
            if (!sellSlotShown || string.IsNullOrEmpty(sellSlotTooltip)) { HideCombineTooltip(); return; }
            ShowTooltip(sellSlotTooltip, cardRect);
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

        string resultName = recipe.result != null ? recipe.result.DisplayName : "?";
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
                return ingredient.unit != null ? ingredient.unit.DisplayName : null;
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

    void BuildUnitCommandGrid(RectTransform frame)
    {
        // 격자는 금테 칸 안쪽 자식에 둔다(AddConsoleFrame 주석 — 테두리 띠가 격자 칸을 먹지 않게). 칸 크기는 칸에 맞춰 잰다.
        GridLayoutGroup grid = AddFitGrid(frame, "UnitCommandGrid", CommandColumns, CommandRows, 6f, 5f, false);

        for (int i = 0; i < CommandSlotCount; i++)
        {
            BuildUnitCommandSlot(i, grid.transform);

            if (i < UnitOnlyCommandLabels.Length)
            {
                unitCommandSlotNames[i].text = UnitOnlyCommandLabels[i];
                unitCommandSlotHotkeys[i].text = UnitOnlyCommandHotkeys[i];
                unitCommandSlotButtons[i].interactable = true;
            }
            else
            {
                // 판매·조합 결과가 들어올 칸. 채워지기 전까지는 보이지 않게 둔다.
                unitCommandSlotBackgrounds[i].color = Color.clear;
            }
        }
    }

    // 이동·정지·홀드·공격·모으기·정렬 여섯 칸. 유닛에게만 의미가 있어서 건물을 고르면 통째로 감춘다.
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
        // 09-29: 칸이 약 110×68로 커져(38 정사각에서) 긴 이름(최장 11자 「구일의악을퍼뿌린장본인」)은 접는다.
        //    옛 38칸에선 접으면 「공/격」처럼 세로로 쪼개져 NoWrap이었다 — 지금 폭이면 짧은 이름은 안 쪼개진다.
        //    글씨는 1920×1080 기준 최소 14pt(사장님 09-29 「글씨가 안 보인다」 · PM 지시).
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 14f;
        nameText.fontSizeMax = 20f;
        nameText.lineSpacing = -8f;
        // 아래 22%는 단축키 자리라 이름이 거기까지 내려오지 않게 비운다.
        nameText.rectTransform.anchorMin = new Vector2(0f, 0.22f);
        nameText.rectTransform.anchorMax = Vector2.one;
        nameText.rectTransform.offsetMin = new Vector2(3f, 0f);
        nameText.rectTransform.offsetMax = new Vector2(-3f, -2f);

        // 단축키는 오른쪽 아래 구석. 금색 굵게 — 워크3처럼 「무슨 키」가 한눈에.
        TMP_Text hotkeyText = CreateLabel(card.transform, "Hotkey", "");
        hotkeyText.raycastTarget = false;
        hotkeyText.fontSize = 15;
        hotkeyText.fontStyle = FontStyles.Bold;
        hotkeyText.alignment = TextAlignmentOptions.BottomRight;
        hotkeyText.color = new Color(1f, 0.85f, 0.45f, 1f);
        hotkeyText.textWrappingMode = TextWrappingModes.NoWrap;
        hotkeyText.rectTransform.anchorMin = Vector2.zero;
        hotkeyText.rectTransform.anchorMax = new Vector2(1f, 0.32f);
        hotkeyText.rectTransform.offsetMin = new Vector2(2f, 1f);
        hotkeyText.rectTransform.offsetMax = new Vector2(-4f, 0f);
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
    // 09-29 워크3 4×3: 1줄 이동·정지·홀드·공격(워크3 기본 명령 순서), 2줄 모으기·정렬 + 판매(6).
    static readonly string[] UnitOnlyCommandLabels = { "이동", "정지", "홀드", "공격", "모으기", "정렬" };
    static readonly string[] UnitOnlyCommandHotkeys = { "M", "S", "H", "A", "V", "C" };

    const int MoveCommandSlot = 0;
    const int StopCommandSlot = 1;
    const int HoldCommandSlot = 2;
    const int AttackCommandSlot = 3;
    const int GatherCommandSlot = 4;
    const int AlignCommandSlot = 5;
    // 판매(09-29 사장님 — 떠 있던 버튼을 옮김). 원작 판매 능력(A09G·A0B8·A0BA·A0B9·A0BB·A080·A0OE, war3map_new.w3a)은
    // 단축키(ahky)가 전부 빈 문자열이라 단축키를 안 붙인다. 원작 버튼 자리는 abpy 1(가운데 줄) · abpx 3(5종)/2(2종).
    const int SellCommandSlot = 7;   // 09-29 PM: 원작처럼 가운데 줄 오른쪽 끝(abpx 3) — 6번은 빈칸

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
        if (index >= MoveCommandSlot && index <= AlignCommandSlot && currentShop as Object == null)
        {
            SelectionManager selection = Selection;
            if (selection == null || selection.Selected.Count == 0) return;

            if (index == MoveCommandSlot) selection.BeginMoveTargeting();
            else if (index == AttackCommandSlot) selection.BeginAttackTargeting();
            else if (index == StopCommandSlot) UnitCommands.Stop(selection.Selected);
            else if (index == HoldCommandSlot) UnitCommands.Hold(selection.Selected);
            else if (index == GatherCommandSlot) UnitCommands.Gather(selection.Selected);
            else UnitCommands.SendToPen(selection.Selected);
            return;
        }

        if (index == SellCommandSlot && currentShop as Object == null)
        {
            if (sellSlotEnabled) OnSellButtonClicked();
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
        if (system == null) return;
        if (!system.CanCombineNow(recipe))
        {
            // 원작 [조합]은 흐려지지 않고 눌러 보면 모자란 것을 말해 준다(j:3444~3449, 그 플레이어에게만 5초) — 알림 묶음 5.
            //    흐린 버튼은 그대로 두되, 누르면 이유를 띄운다. 재료·돈이 다 있는데 안 되면(원딜·라운드 조건 등) 짧게.
            List<string> shortage = system.DescribeShortage(recipe);
            if (shortage.Count == 0) PlayerNotification.Show(LocalPlayer.LocalPlayerId, "지금은 조합할 수 없습니다.", 5f);
            foreach (string line in shortage) PlayerNotification.Show(LocalPlayer.LocalPlayerId, line, 5f);
            return;
        }

        // 원작 [조합]은 유닛 능력 — 결과가 누른 유닛 자리에 나온다. 선택 첫 유닛을 시전 유닛으로 본다.
        SelectionManager casterSelection = Selection;
        Vector3? casterPosition = casterSelection != null && casterSelection.Selected.Count > 0 && casterSelection.Selected[0] != null
            ? casterSelection.Selected[0].transform.position
            : (Vector3?)null;

        // MP: 멀티 클라는 조합을 호스트에 요청만 한다(재료 소모·결과 소환은 호스트, 결과는 거울로 돌아온다).
        if (!GameAuthority.IsServer)
        {
            NetCommands.RequestCombine(system, recipe, casterSelection != null && casterSelection.Selected.Count > 0 ? casterSelection.Selected[0] : null);
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
        pendingTargetRadius = view.targetRadius;
        targetingStartFrame = Time.frameCount;
    }

    // 상점을 고른 동안 칸 단축키를 받는다. 채팅 중엔 안 받는다(SelectionManager와 같은 이유).
    void RefreshShopHotkeys()
    {
        ShopSelected = currentShop as Object != null;
        if (!ShopSelected || ChatInputGate.IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        for (int visual = 0; visual < CommandSlotCount; visual++)
        {
            char key = shopSlotHotkeys[visual];
            if (key == '\0' || shopLogicalSlotIndex[visual] < 0) continue;
            if (!System.Enum.TryParse(key.ToString(), out Key inputKey)) continue;
            if (!keyboard[inputKey].wasPressedThisFrame) continue;

            OnShopSlotClicked(visual);
            return;
        }
    }

    // 지점 칸을 찍는 동안 커서 아래 땅에 범위 원. 대기가 풀리면(실행·우클릭·선택 해제) 바로 끈다.
    void RefreshTargetArea()
    {
        ShopTargetingPending = pendingSlotIndex >= 0;
        bool show = ShopTargetingPending && pendingTargetKind == LaneShopTargetKind.Ground
                    && pendingTargetRadius > 0f && Mouse.current != null && Camera.main != null;
        if (show && WorldPick.TryHit(Camera.main, Mouse.current.position.ReadValue(), out RaycastHit hit))
            TargetAreaIndicator.Show(hit.point, pendingTargetRadius);
        else
            TargetAreaIndicator.Hide();
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
        ShopClickConsumedFrame = Time.frameCount;   // 이 클릭은 대상 지정 — SelectionManager가 선택으로 안 친다

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
            unitCommandSlotHotkeys[slot].text = "";
            shopSlotHotkeys[slot] = '\0';
            unitCommandSlotBackgrounds[slot].color = Color.clear;
        }

        // 0~2·12번(공격/정지/모으기/정렬)은 상점 칸이 아니다 — 기본값 0이 "논리 슬롯 0"으로
        // 읽히지 않게 여기서도 -1로 씻어둔다. 안 씻으면 그 칸에 마우스를 올렸을 때
        // 상점의 0번 슬롯 툴팁이 엉뚱하게 뜬다.
        for (int i = 0; i < UnitOnlyCommandLabels.Length; i++) shopLogicalSlotIndex[i] = -1;
        if (shop != null) sellSlotShown = false;   // 판매 칸(6)은 상점에선 상점 칸이다 — RefreshSellButton이 덮어쓰지 않게
        unitCommandSlotNames[SellCommandSlot].color = Color.white;   // 판매 칸이 흐린 글씨로 남아 있었으면 상점 글씨로 되돌린다

        SetUnitOnlyCommandsVisible(shop == null);

        // 09-29 4×3: 상점 칸 3~6번은 유닛 명령·판매 칸과 겹친다. 위에서 유닛 명령을 감출 때(또는 판매 칸이 꺼질 때)
        //    interactable=false가 남으면 도박소 칸이 안 눌린다 — 상점 칸은 여기서 다시 켠다.
        for (int i = 0; i < UnitCommandResultSlotOrder.Length; i++)
            unitCommandSlotButtons[UnitCommandResultSlotOrder[i]].interactable = true;

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
                unitCommandSlotHotkeys[slot].text = "";
                shopSlotHotkeys[slot] = '\0';
                unitCommandSlotBackgrounds[slot].color = Color.clear;
                continue;
            }

            unitCommandSlotNames[slot].text = view.label;
            char key = view.hotkey != '\0' ? view.hotkey : ShopGridHotkeys[slot];
            if (shopSlotHotkeys[slot] != key)
            {
                shopSlotHotkeys[slot] = key;
                unitCommandSlotHotkeys[slot].text = key.ToString();
            }

            Color color = view.color;
            color.a = view.available ? color.a : 0.35f;
            unitCommandSlotBackgrounds[slot].color = color;
        }
    }

    void RebuildUnitCommandSlots(UnitData selectedData)
    {
        for (int i = 0; i < unitCommandSlotCount; i++)
        {
            int slot = UnitRecipeSlotOrder[i];
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
        // 3줄 4칸. 조합식 207개 중 한 유닛 결과 최대 4(09-29 실측) — 넘치면 알린다(칸을 조용히 버리지 않는다).
        int shown = Mathf.Min(startingWith.Count, UnitRecipeSlotOrder.Length);
        if (startingWith.Count > UnitRecipeSlotOrder.Length)
            Debug.LogWarning($"[HUD] {selectedData.name}: 조합 결과 {startingWith.Count}개 > 명령 카드 {UnitRecipeSlotOrder.Length}칸 — 뒤 {startingWith.Count - UnitRecipeSlotOrder.Length}개가 안 보입니다.");

        for (int i = 0; i < shown; i++)
        {
            CombineRecipe recipe = startingWith[i];
            if (recipe == null || recipe.result == null) continue;

            int slot = UnitRecipeSlotOrder[i];
            unitCommandRecipes[slot] = recipe;
            unitCommandSlotNames[slot].text = recipe.result.DisplayNameTwoLines;
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
            int slot = UnitRecipeSlotOrder[i];
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
        SetPortraitBars(null, null, 0);   // 아래 유닛 분기에서만 켠다
        if (unitStatRows != null && unitStatRows.activeSelf) unitStatRows.SetActive(false);
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
            SetPortraitModel(null); // 초상화
            return;
        }

        Selectable first = selection.Selected[0];
        if (first == null)
        {
            unitInfoText.text = "선택된 유닛 없음";
            SetUnitInfoPortrait(null);
            SetPortraitModel(null); // 초상화
            return;
        }

        // 상점 건물(09-29 PM): 「Lane1_도박소 - -」와 「-」 스탯 줄 대신 표시 이름 한 줄(상점 이름표와 같은 표).
        if (first.TryGetComponent(out ILaneShop _))
        {
            SetUnitInfoPortrait(null);
            SetPortraitModel(null);
            unitInfoText.text = $"<size=115%>{ShopNameplateLayer.DisplayNameOf(first.name)}</size>";
            return;
        }

        first.TryGetComponent(out UnitIdentity identity);
        first.TryGetComponent(out UnitAttacker attacker);
        UnitData data = identity != null ? identity.Data : null;

        SetUnitInfoPortrait(data);
        SetPortraitModel(first.gameObject); // 초상화: 선택 첫 유닛(클라는 거울 겉모습)

        // 위습은 UnitIdentity가 없어서 예전엔 오브젝트 이름이 그대로 떴다 — 화면에
        // **「WispPrefab(Clone)」**이라고 나왔다(2026-09-24 플레이 캡처). 사장님이 보는 이름은
        // 위습 에셋의 이름이어야 하고, 등급색도 그 위습이 뽑는 등급을 따라야 한다.
        first.TryGetComponent(out Wisp selectedWisp);
        WispData wispData = selectedWisp != null ? selectedWisp.Data : null;

        string unitName = data != null ? data.DisplayName
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
        // MP: 클라 겉모습엔 UnitAttacker가 없다 — 거울(NetEntity, 겉모습의 부모)이 싣고 온 호스트 실제 값을 쓴다.
        NetEntity mirror = attacker == null && data != null ? first.GetComponentInParent<NetEntity>() : null;
        // MP: 거울이 방금 사라진 프레임(판 끝·조합 재료)엔 [Networked] 값을 읽으면 예외다(09-29 두 창 실측).
        if (mirror != null && (mirror.Object == null || !mirror.Object.IsValid)) mirror = null;
        bool hasStats = attacker != null || mirror != null;
        float damage = attacker != null ? attacker.AttackDamage : mirror != null ? mirror.AttackDamage : 0f;
        float range = attacker != null ? attacker.AttackRange : mirror != null ? mirror.AttackRange : 0f;
        float interval = attacker != null ? attacker.AttackInterval : mirror != null ? mirror.AttackInterval : 0f;
        // 영웅 레벨(GAP 09-27 「영웅 레벨·XP HUD」) — 이름 줄 끝에 「Lv.N」(워크3 영웅 정보창의 레벨). 줄을 늘리지 않아 정보칸 높이는 그대로.
        int heroLevel = attacker != null ? attacker.CharacterLevel : mirror != null ? mirror.HeroLevel : 0;
        string levelLabel = hasStats && heroLevel > 0 ? $"  Lv.{heroLevel}" : "";
        string attackPower = hasStats ? damage.ToString("F0") : "-";
        string attackRange = hasStats ? range.ToString("F1") : "-";
        string attackSpeed = hasStats && interval > 0f
            ? (1f / interval).ToString("F2")
            : "-";

        // 마나·방어력은 PM 요청에 있었으나 UnitData/UnitAttacker에 그 필드 자체가 없다
        // (플레이어 유닛은 마나를 소모하지 않고, 방어력 감폭은 EnemyDummy 전용 축이다) —
        // 없는 값을 지어내지 않고 실제로 있는 축만 표시한다(2026-09-23, PM 보고 예정).
        if (data != null && unitStatRows != null)
        {
            // 사진 서식(Docs/reference/ui/원랜디_인게임_01.png): 「이름(흰) 별명(노랑) – 등급(등급색)」 + 공격 아이콘 줄 + 방어 아이콘 줄 + 상태 줄,
            //   체력·마나는 초상 아래 바. 플레이어 유닛은 피해를 안 받으니 방어는 원작 무적 표기(빨강)와 같다.
            string person = data.DisplayName.Length > data.unitName.Length ? data.DisplayName.Substring(0, data.DisplayName.Length - data.unitName.Length - 1) : "";
            string firstPart = person.Length > 0 ? person : data.unitName;
            string secondPart = person.Length > 0 ? $" <color=#FFD84A>{data.unitName}</color>" : "";
            unitInfoText.text = $"<size=115%>{firstPart}{secondPart} – <color=#{gradeColorHex}>{grade}{levelLabel}</color></size>";
            string bonus = hasStats && data.attackPower > 0f && damage - data.attackPower >= 0.5f ? $" <color=#46E06A>+{damage - data.attackPower:F0}</color>" : "";
            unitDamageText.text = $"<color=#FF9A3A>공격력:</color> {attackPower}{bonus}";   // 사장님 10-03: 사거리·공속은 정보칸에서 뺀다(F1 DebugHud엔 남음)
            unitArmorText.text = "<color=#FF9A3A>방어:</color> <color=#FF4A4A>무적</color>";
            unitStatusText.text = "<color=#FF9A3A>상태:</color>";
            if (!unitStatRows.activeSelf) unitStatRows.SetActive(true);
            int? manaNow = null;
            int manaCap = 0;
            if (UnitManaTable.HasMana(data))
            {
                PlayerContext localPlayer = PlayerContext.Local;
                if (localPlayer != null && localPlayer.ResourceWallet != null)
                {
                    manaNow = localPlayer.ResourceWallet.Get(ResourceType.Mana);
                    manaCap = localPlayer.ResourceWallet.GetCap(ResourceType.Mana);
                }
            }
            SetPortraitBars(Mathf.RoundToInt(data.hp), manaNow, manaCap);
            return;
        }

        unitInfoText.text =
            // 09-29 워크3 콘솔: 이름 한 줄 + 스탯 두 열(정보칸이 넓어져 한 줄에 하나씩 쓰면 오른쪽이 빈다).
            $"<size=115%><color=#{gradeColorHex}>{unitName} - {grade}{levelLabel}</color></size>\n" +
            $"공격력: {attackPower}<pos=50%>사거리: {attackRange}\n" +
            $"공격속도: {attackSpeed}/s<pos=50%>체력: {hp}";
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
            SetPortraitModel(target); // 초상화: 살펴보는 적
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
            SetPortraitModel(target); // 초상화: 조합표 인형
            string hex = ColorUtility.ToHtmlStringRGB(GetGradeColor(data.grade));
            unitInfoText.text =
                $"<color=#{hex}>{data.DisplayName} - {data.grade.KoreanName()}</color>  <size=80%>(조합표 · 조작 불가)</size>\n체력: {data.hp:F0}\n" +
                $"공격력: {data.attackPower:F0}\n사거리: {data.attackRange:F1}\n공격속도: {data.attackSpeed:F2}/s";
            return true;
        }
        return false;
    }

    // 초상화(09-26): 대상의 실제 모델을 PortraitStage에 세워 RawImage로 보인다. 모델이 없으면(자리표시) 글자 칸 그대로.
    // 매 프레임 불려도 PortraitStage.Show가 같은 대상이면 바로 돌아간다.
    void SetPortraitModel(GameObject target)
    {
        if (unitInfoPortraitModel == null) return;
        bool shown = PortraitStage.Show(target);
        if (shown && unitInfoPortraitModel.texture == null) unitInfoPortraitModel.texture = PortraitStage.Texture;
        if (unitInfoPortraitModel.gameObject.activeSelf != shown) unitInfoPortraitModel.gameObject.SetActive(shown);
        if (unitInfoPortraitInitial != null) unitInfoPortraitInitial.enabled = !shown;
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
        // 09-29 워크3 콘솔: 초상화 칸이 카드 격자와 따로 있어 여러 기를 골라도 첫 유닛 초상을 보인다(워크3와 같다).
        Selectable firstSelected = selection.Selected[0];
        UnitData firstData = firstSelected != null && firstSelected.TryGetComponent(out UnitIdentity firstIdentity) ? firstIdentity.Data : null;
        SetUnitInfoPortrait(firstData);
        SetPortraitModel(firstSelected != null ? firstSelected.gameObject : null);
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
            cardNames[i].text = data != null ? data.DisplayNameTwoLines : target.name;

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
        if (goldText == null || roundTimeText == null) return;

        PlayerContext local = PlayerContext.Local;
        int gold = local != null && local.GoldWallet != null ? local.GoldWallet.Gold : 0;
        int wood = local != null && local.ResourceWallet != null ? local.ResourceWallet.Get(ResourceType.Wood) : 0;

        // 도움소 스킬(마나포션·선택위습제조 등)이 마나를 쓰는데 현재 양을 볼 곳이 없었다(사장님 10-02).
        int mana = local != null && local.ResourceWallet != null ? local.ResourceWallet.Get(ResourceType.Mana) : 0;

        int food = local != null && local.UnitUpgrades != null ? local.UnitUpgrades.TraitPoints : 0;   // 원작 고기(인구) 칸 = FOOD_USED = 특성 포인트
        if (gold != lastGold || wood != lastWood || mana != lastMana || food != lastFood)
        {
            lastGold = gold;
            lastWood = wood;
            lastMana = mana;
            lastFood = food;
            goldText.text = gold.ToString();
            woodText.text = wood.ToString();
            foodText.text = food.ToString();
            manaText.text = $"마나 {mana}";
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
            // 원작 타이머 창: 제목(빨강) + 시간. 「현재레벨->N」(j 29588) · 라운드 사이 준비엔 「N라운드 준비」.
            roundTimerTitle.text = rm == null ? "현재레벨->" : preparing ? $"{round}라운드 준비" : $"현재레벨->{round}";
            roundTimeText.text = rm == null ? "-" : Clock(timeTenths / 10f);
        }

        RefreshExtraTimer(rm);
        RefreshWispCount();
    }

    TMP_Text extraTimerText;
    GameObject extraTimerObject;
    int lastExtraTimerKey = int.MinValue;

    void RefreshExtraTimer(RoundManager rm)
    {
        if (extraTimerObject == null) return;
        bool has;
        bool newWorldWait = false;
        float seconds = 0f;
        float storyLeft;
        string storyName;
        if (!GameAuthority.IsServer && NetGameState.Instance != null)   // MP: 클라는 호스트 값
        {
            has = NetGameState.Instance.ExtraTimerKind != 0;
            newWorldWait = NetGameState.Instance.ExtraTimerKind == 2;
            seconds = NetGameState.Instance.ExtraTimerLeft;
            storyLeft = NetGameState.Instance.StoryLimitLeft;
            storyName = NetGameState.Instance.StoryLimitName.ToString();
        }
        else
        {
            has = rm != null && rm.TryGetExtraTimer(out newWorldWait, out seconds);
            StoryManager story = StoryManager.Instance;
            storyLeft = story != null ? story.SecondsLeftInLimit : -1f;
            storyName = story != null && story.Running != null ? story.Running.storyName : "";
        }
        if (!has) { newWorldWait = false; seconds = 0f; }
        bool hasStory = storyLeft >= 0f && !string.IsNullOrEmpty(storyName);   // 원작 와노쿠니 제한 창(j:13754)

        int key = (has ? (newWorldWait ? 100000 : 0) + Mathf.CeilToInt(seconds) : -1) * 10000 + (hasStory ? Mathf.CeilToInt(storyLeft) : -1);
        if (key == lastExtraTimerKey) return;
        lastExtraTimerKey = key;
        bool any = has || hasStory;
        if (extraTimerObject.activeSelf != any) extraTimerObject.SetActive(any);
        if (!any) return;
        string text = "";
        if (has) text = newWorldWait ? $"60라운드-신세계 대기중  {Clock(seconds)}" : $"<color=#FF0000>보스 제한시간-></color>  {Clock(seconds)}";
        if (hasStory) text += (text.Length > 0 ? "     " : "") + $"{storyName} 남은 시간:  {Clock(storyLeft)}";
        extraTimerText.text = text;
    }

    static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
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
    // 위습 칸을 미니맵 칸 폭 안에 줄 세운다 — 넘치면 위로 한 줄 더(아래 줄부터). PlayerNotification은 켜진 칸 전부의 윗선을 본다.
    void LayoutWispSlots(float rowWidth)
    {
        int perRow = Mathf.Max(1, Mathf.FloorToInt((rowWidth + WispSlotGap) / (WispSlotSize + WispSlotGap)));
        for (int i = 0; i < wispSlots.Count; i++)
        {
            RectTransform slot = (RectTransform)wispSlots[i].root.transform;
            slot.anchoredPosition = new Vector2((i % perRow) * (WispSlotSize + WispSlotGap), (i / perRow) * (WispSlotSize + WispSlotGap));
        }
    }

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

            // 위습 모델 아이콘(WispIconBaker) — 배경은 등급색 그대로. 이름 글자는 칸에서 빼고 마우스를 올리면 툴팁으로 보인다(사장님 10-03).
            RectTransform iconRect = CreatePanel(slot, "Icon", Color.white);
            SetAnchors(iconRect, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
            Image iconImage = iconRect.GetComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;   // 아이콘이 구워지면 켠다

            TMP_Text nameText = null;   // 이름 칸 없음(툴팁)

            TMP_Text countText = CreateLabel(slot, "Count", "");
            SetAnchors((RectTransform)countText.transform, new Vector2(0.45f, 0.0f), new Vector2(0.98f, 0.5f));
            countText.fontSize = 22f;
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.BottomRight;
            countText.color = Color.white;
            countText.outlineWidth = 0.3f;
            countText.outlineColor = new Color32(0, 0, 0, 255);
            countText.raycastTarget = false;

            int index = i;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(() => OnWispSlotClicked(index));
            EventTrigger wispTrigger = slot.gameObject.AddComponent<EventTrigger>();
            AddTriggerEntry(wispTrigger, EventTriggerType.PointerEnter, _ => OnWispSlotHover(index));
            AddTriggerEntry(wispTrigger, EventTriggerType.PointerExit, _ => HideCombineTooltip());
            slot.gameObject.SetActive(false);   // 그 종류가 하나도 없으면 칸을 아예 안 보인다

            wispSlots.Add(new WispSlot
            {
                root = slot.gameObject,
                background = slot.GetComponent<Image>(),
                icon = iconImage,
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
            if (slot.nameText != null) slot.nameText.text = ShortWispName(data.wispName);
            if (!slot.icon.enabled || slot.icon.sprite == null)
            {
                Sprite sprite = WispIconBaker.Get(wispsByType[data].Count > 0 ? wispsByType[data][0] : null);
                if (sprite != null) { slot.icon.sprite = sprite; slot.icon.enabled = true; }
            }
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

    void OnWispSlotHover(int index)
    {
        if (index < 0 || index >= wispSlots.Count || wispSlots[index].data == null) return;
        ShowTooltip(wispSlots[index].data.wispName, (RectTransform)wispSlots[index].root.transform);
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

    // 점수판 열 위치(px, 글자 20 기준) — 이름 칸 폭 ≈ 칩 + 칭호(최대 5자) + 닉네임(최대 12자).
    const int ScoreboardCountColumn = 250;
    const int ScoreboardFullColumn = 430;
    // 원작 플레이어 색(j 14714~14717) — 1 빨강 · 2 파랑 · 3 보라 · 4 노랑.
    static readonly string[] ScoreboardColorHex = { "FF0202", "0041FF", "530080", "FFFC00" };

    /// <summary>점수판 이름 칸: 칭호(자체 색) + 닉네임(플레이어 색). 닉네임이 없으면 「플레이어 N」.</summary>
    static string ScoreboardName(int slot)
    {
        if (PlayerContext.GetOccupied(slot) == null) return null;
        string nick = PlayerDisplayName.RawNickname(slot);
        if (string.IsNullOrWhiteSpace(nick)) nick = $"플레이어 {slot + 1}";
        PlayerContext context = PlayerContext.GetOccupied(slot);
        int clears = context != null && context.PersistentSave != null ? context.PersistentSave.Data.cumulativeClearCount : 0;
        string title = PlayerDisplayName.ClearTitleOf(clears);
        int index = Mathf.Clamp(slot, 0, 3);
        return title + "<color=#" + ScoreboardColorHex[index] + ">" + nick + "</color>";
    }

    // 팀 현황판 값은 자주 안 바뀌므로(적 수·이름·풀카운트), 이전 프레임과 비교해 실제로 바뀐 경우에만
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
        // 난이도(2026-09-26 사장님 「난이도 선택하고 쉬움인지 어려움인지 유닛카운트 있는 쪽에 표시」) — 고른 뒤에만.
        //    같은 줄에 붙인다: 판 높이가 줄 수에서 유도되므로(BuildTeamPanel 주석) 줄을 늘리지 않는다.
        DifficultyManager difficulty = DifficultyManager.Instance;
        string difficultyLabel = difficulty != null && difficulty.IsModeSelected ? difficulty.Current.KoreanName() : null;

        // 원작 멀티보드 제목 「유닛 카운트 = 70 <- 패배」(j:3362, udg_ModeEnemyInt = 레인당 한계). 한계는 41R에 난이도별로 내려간다.
        int deathLimit = !GameAuthority.IsServer && NetGameState.Instance != null   // MP: 클라는 호스트 값
            ? NetGameState.Instance.DeathLimit
            : (roundManager != null ? roundManager.EnemyCountLimit : 0);
        bool fullVisible = FullCountScore.Visible;
        bool namesChanged = false;
        if (Time.unscaledTime >= nextSlotNameRefresh)
        {
            nextSlotNameRefresh = Time.unscaledTime + 1f;
            for (int i = 0; i < TeamSlotCount; i++)
            {
                string nameNow = ScoreboardName(i);
                if (nameNow != slotNameCache[i]) { slotNameCache[i] = nameNow; namesChanged = true; }
            }
        }
        bool changed = namesChanged || !teamPanelInitialized || fullVisible != lastFullVisible || totalEnemies != lastTotalEnemyCount || difficultyLabel != lastDifficultyLabel || deathLimit != lastDeathLimit;

        for (int i = 0; i < TeamSlotCount; i++)
        {
            PlayerContext context = PlayerContext.GetOccupied(i);
            slotHas[i] = context != null;
            slotDead[i] = context != null && context.IsDead;
            slotEnemy[i] = context != null ? EnemyDummy.CountInLane(context.PlayerId) : 0;
            slotGold[i] = context != null && context.GoldWallet != null ? context.GoldWallet.Gold : 0;
            slotWood[i] = context != null && context.ResourceWallet != null ? context.ResourceWallet.Get(ResourceType.Wood) : 0;
            slotGrace[i] = MatchConfig.Active ? NetPlayer.GraceSecondsFor(i) : 0;   // MP: 재접속 유예 「연결 끊김 57초」
            slotFull[i] = FullCountScore.Get(i);
            if (slotFull[i] != lastSlotFull[i]) changed = true;

            if (slotGrace[i] != lastSlotGrace[i]) changed = true;
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
        lastDifficultyLabel = difficultyLabel;
        lastDeathLimit = deathLimit;
        lastFullVisible = fullVisible;

        teamPanelBuilder.Clear();
        // 신세계에서 3열이 되면 원작이 글자를 0.11→0.09(≈82%)로 줄인다(j 29703) — 열 위치도 같은 비율로.
        float boardScale = fullVisible ? 0.82f : 1f;
        int countCol = Mathf.RoundToInt(ScoreboardCountColumn * boardScale);
        int fullCol = Mathf.RoundToInt(ScoreboardFullColumn * boardScale);
        if (fullVisible) teamPanelBuilder.Append("<size=82%>");
        // 원작 멀티보드(j 14700~14728) 모양: 제목 한 줄 + 머리줄 + 플레이어 4줄, 열은 [이름 | 남은 라운드 유닛 수 | (신세계) 풀카운트].
        //   제목 「|c00ffb0ff유닛 카운트 = |cFF00FF00N|c00ffb0ff<- 패배」 · 머리줄 「|cff00ffff난이도 :|r{모드}|cff00ffff모드|r」 / 「|c0000ff00남은 라운드 유닛 수」.
        //   이름은 플레이어 색(1 ff0202 · 2 0041FF · 3 530080 · 4 FFFC00)이고, 골드·목재는 점수판에 없다(상단 바 자원 — 사장님 확정 10-03).
        if (deathLimit > 0)
            teamPanelBuilder.Append("<color=#FFB0FF>유닛 카운트 = </color><color=#00FF00>").Append(deathLimit).Append("</color><color=#FFB0FF> <- 패배</color>");
        else
            teamPanelBuilder.Append("<color=#FFB0FF>유닛 카운트 </color>").Append(totalEnemies);

        if (teamPanelCollapsed)
        {
            for (int i = 0; i < TeamSlotCount; i++)   // 접힌 동안에도 「바뀌었나」 기준을 맞춰 둔다(안 맞추면 매 프레임 다시 그림)
            {
                lastSlotHasContext[i] = slotHas[i]; lastSlotDead[i] = slotDead[i]; lastSlotEnemyCount[i] = slotEnemy[i];
                lastSlotGold[i] = slotGold[i]; lastSlotFull[i] = slotFull[i]; lastSlotWood[i] = slotWood[i]; lastSlotGrace[i] = slotGrace[i];
            }
            teamPanelText.text = teamPanelBuilder.ToString();
            return;   // 접힘: 제목 줄만(원작 멀티보드 접기)
        }

        teamPanelBuilder.Append("\n<color=#00FFFF>");
        if (difficultyLabel != null) teamPanelBuilder.Append("난이도 : ").Append(difficultyLabel).Append("모드"); else teamPanelBuilder.Append("난이도 : -");
        teamPanelBuilder.Append("</color><pos=").Append(countCol).Append("><color=#00FF00>남은 라운드 유닛 수</color>");

        for (int i = 0; i < TeamSlotCount; i++)
        {
            lastSlotHasContext[i] = slotHas[i];
            lastSlotDead[i] = slotDead[i];
            lastSlotEnemyCount[i] = slotEnemy[i];
            lastSlotGold[i] = slotGold[i];
            lastSlotFull[i] = slotFull[i];
            lastSlotWood[i] = slotWood[i];
            lastSlotGrace[i] = slotGrace[i];

            teamPanelBuilder.Append('\n');

            string hex = ScoreboardColorHex[i];
            bool isLocal = slotHas[i] && i == LocalPlayer.LocalPlayerId;
            if (isLocal) teamPanelBuilder.Append("<b>");
            // 이름 칸 = 플레이어 색 칩(원작 줄 앞 아이콘 자리) + 칭호(자체 색) + 닉네임(플레이어 색)
            teamPanelBuilder.Append("<mark=#").Append(hex).Append("FF>  </mark> ");   // 칩은 글자(■)가 폰트에 없을 수 있어 TMP 형광펜 배경으로 그린다
            if (slotHas[i])
            {
                string title = slotNameCache[i] ?? ScoreboardName(i);
                teamPanelBuilder.Append(title);   // 칭호·닉네임 서식은 ScoreboardName이 색 태그까지 만든다
            }
            // 빈 자리는 색 칩만(사장님 10-03: 「열림」 글자 제거)

            if (slotHas[i])
            {
                teamPanelBuilder.Append("<pos=").Append(countCol).Append('>');
                if (slotGrace[i] > 0 && !slotDead[i])
                    teamPanelBuilder.Append("<color=#FF8A65>연결 끊김 ").Append(slotGrace[i]).Append("초</color>");   // MP
                else if (slotDead[i])
                    teamPanelBuilder.Append("<color=#FF5050>사망</color>");
                else
                    teamPanelBuilder.Append(slotEnemy[i]);
                // 원작 3열 「|cff00ffff풀카운트  :|r N  점」 — 신세계 진입 뒤부터(j 29702~29708).
                if (fullVisible) teamPanelBuilder.Append("<pos=").Append(fullCol).Append("><color=#00FFFF>풀카운트  :</color> ").Append(slotFull[i]).Append("  점");
            }
            if (isLocal) teamPanelBuilder.Append("</b>");
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
                .Append(unit.DisplayName).Append(" x").Append(inventoryCounts[unit]);
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
        Image image = obj.GetComponent<Image>();
        image.color = color;
        // 둥근 모서리(사장님 10-03). 투명 컨테이너와 화면 전체를 덮는 막·바는 그대로 사각형.
        if (color.a > 0.01f && !SquarePanelNames.Contains(name))
        {
            image.sprite = UiSkin.RoundFill();
            image.type = Image.Type.Sliced;
        }
        return obj.GetComponent<RectTransform>();
    }

    static readonly HashSet<string> SquarePanelNames = new HashSet<string> { "BottomBar", "TopBar", "GameMenu", "Fill", "Border" };

    // 9-slice 스프라이트가 없어 모서리 4개를 얇은 Image 띠로 겹쳐 테두리처럼 보이게 한다.
    // 미니맵·초상화 칸처럼 "이 영역이 하나의 칸"임을 배경 알파만으로는 못 알아볼 때 쓴다.
    static void AddPanelBorder(RectTransform parent, Color color, float thickness)
    {
        AddRoundRing(parent, color, thickness, UiSkin.DefaultRadius, 0f);
    }

    // 둥근 테두리 고리 한 겹 — inset만큼 안쪽으로 들어가 앉는다(반지름도 그만큼 줄인다).
    static void AddRoundRing(RectTransform parent, Color color, float thickness, float radius, float inset)
    {
        GameObject obj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = UiSkin.RoundRing(Mathf.Max(1f, radius - inset), thickness);
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // 워크3 콘솔 칸 테두리 — 바깥 금테 2px + 안쪽 짙은 금 1px(09-29). ⚠️ GridLayoutGroup이 붙은 오브젝트엔 쓰지 않는다 —
    //    테두리 띠가 격자 자식으로 끼어 칸 하나를 차지한다. 격자는 이 칸 안의 자식에 둔다(BuildUnitCommandGrid).
    static void AddConsoleFrame(RectTransform parent)
    {
        AddRoundRing(parent, BorderColor, 2f, 10f, 0f);
        AddRoundRing(parent, BorderInnerColor, 1f, 10f, 2f);
    }

    // 하단 바 안, 오른쪽 끝에서 rightInset만큼 떨어진 곳에 고정 폭 칸(세로는 바의 5~95%).
    static void SetFixedRight(RectTransform rect, float rightInset, float width)
    {
        rect.anchorMin = new Vector2(1f, 0.05f);
        rect.anchorMax = new Vector2(1f, 0.95f);
        rect.offsetMin = new Vector2(-rightInset - width, 0f);
        rect.offsetMax = new Vector2(-rightInset, 0f);
    }

    // 왼쪽은 바 폭 비율, 오른쪽은 오른쪽 끝에서 rightInset(px) — 오른쪽 고정 칸들이 가져가고 남는 폭을 받는다.
    static void SetRightAnchored(RectTransform rect, float leftFraction, float rightInset)
    {
        rect.anchorMin = new Vector2(leftFraction, 0.05f);
        rect.anchorMax = new Vector2(1f, 0.95f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-rightInset, 0f);
    }

    // 격자 칸 크기를 부모 크기에서 계산한다(09-29 — 박아 둔 38px 칸이 패널 가운데 작게 몰렸다). 해상도·비율이 바뀌면 다시 잰다.
    //   square면 정사각(선택 카드), 아니면 가로·세로 따로 꽉 채운다(명령·아이템 칸).
    struct FitGrid { public GridLayoutGroup grid; public int columns, rows; public bool square; public Vector2 lastSize; }
    readonly List<FitGrid> fitGrids = new List<FitGrid>();

    GridLayoutGroup AddFitGrid(RectTransform frame, string name, int columns, int rows, float inset, float spacing, bool square)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(frame, false);
        RectTransform rect = (RectTransform)obj.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);

        GridLayoutGroup grid = obj.AddComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.MiddleCenter;
        fitGrids.Add(new FitGrid { grid = grid, columns = columns, rows = rows, square = square, lastSize = -Vector2.one });
        return grid;
    }

    void RefreshFitGrids()
    {
        for (int i = 0; i < fitGrids.Count; i++)
        {
            FitGrid fit = fitGrids[i];
            if (fit.grid == null) continue;
            Vector2 size = ((RectTransform)fit.grid.transform).rect.size;
            if (size == fit.lastSize) continue;
            fit.lastSize = size;
            float w = Mathf.Floor((size.x - fit.grid.spacing.x * (fit.columns - 1)) / fit.columns);
            float h = Mathf.Floor((size.y - fit.grid.spacing.y * (fit.rows - 1)) / fit.rows);
            if (fit.square) w = h = Mathf.Min(w, h);
            fit.grid.cellSize = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            fitGrids[i] = fit;
        }
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
