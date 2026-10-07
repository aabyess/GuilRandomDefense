using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 조합 검색 서랍(사장님 확정 10-04, 시안 A + 결과 초상). 친구 이름이 스킨이라 조합표에서 이름을 하나씩 봐야 하던 불편을 푼다.
/// 화면 오른쪽 끝 세로 탭(「조합 검색」, F5)을 누르면 오른쪽에서 밀려 나오는 패널 — 열린 채 게임은 계속된다.
///  · 검색: 친구 이름·별명·에셋 이름·스킨 별명(UnitData.skinAlias)·조합 채팅 코드(commandId)·초성(ㄱㅈㄱ), 한글 부분 일치, 공백 무시.
///  · 등급 칩(전체·특별·희귀·전설·히든·초월·불멸) + 「지금 가능」(CombineSystem.CanCombineNow 재사용).
///  · 결과 줄: 결과 초상 + 이름(등급색) + 비용/코드, 아래 재료 초상들(보유 ✔ 초록 / 없음 ✘ 회색 반투명). 검색 대상이 재료로 쓰이는 식은 「▼ 재료로 쓰임」 구역.
///  · 초상은 UnitThumbBaker가 처음 필요할 때 프레임당 몇 개씩 구워 캐시. 「아무 유닛」 재료는 위습 아이콘(아직 안 구웠으면 회색 상자).
///  · 결과 줄 클릭 → RecipeLocator가 카메라를 조합판의 그 식으로 옮기고 인형 발밑 고리를 깜빡인다.
/// 전부 로컬 UI다 — 서버·네트워크 안 감(싱글·클라 모두 동작). 자체 캔버스라 GameHud와 얽히지 않는다.
/// 채팅 입력 중(ChatInputGate)엔 F5를 무시하고, 검색창에 글을 쓰는 동안은 게임 단축키가 안 먹게 ChatInputGate를 같이 켠다.
/// </summary>
public class RecipeSearchDrawer : MonoBehaviour
{
    // ── 설치: 씬이 불릴 때마다(배포판 첫 씬은 NetBoot — IslandShores와 같은 이유) 조합기가 있는 씬에만 하나 세운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Install();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<RecipeSearchDrawer>() != null) return;
        if (FindFirstObjectByType<CombineSystem>() == null) return;
        new GameObject("[조합 검색]").AddComponent<RecipeSearchDrawer>();
    }

    // ── 모양
    const float PanelWidth = 560f, PanelHeight = 800f, TabWidth = 46f, TabHeight = 210f, TabOffsetY = 150f;
    const float OpenX = -(TabWidth + 6f), ClosedX = 40f;
    const float RowHeight = 148f, CellWidth = 62f;
    const int MaxRows = 40;
    const int MaxCells = 8;
    static readonly Color PanelFill = new Color(0.04f, 0.07f, 0.18f, 1f);
    static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    static readonly Color RowFill = new Color(0.04f, 0.07f, 0.16f, 0.96f);
    static readonly Color Muted = new Color(0.65f, 0.64f, 0.59f, 1f);
    static readonly Color Have = new Color(0.44f, 0.83f, 0.44f, 1f);
    static readonly Color Lack = new Color(0.55f, 0.55f, 0.58f, 1f);
    static readonly Color ChipOff = new Color(0.10f, 0.15f, 0.28f, 1f);

    static readonly string[] GradeChipLabels = { "전체", "특별", "희귀", "전설", "히든", "초월", "불멸" };
    static readonly UnitGrade?[] GradeChipGrades = { null, UnitGrade.Special, UnitGrade.Rare, UnitGrade.Legendary, UnitGrade.Hidden, UnitGrade.Transcendent, UnitGrade.Immortal };

    class Cell
    {
        public GameObject root;
        public Image portrait;
        public TMP_Text label, mark;
    }

    class Row
    {
        public GameObject root;
        public Image portrait;
        public TMP_Text title, sub;
        public GameObject stone, gold, gradeBorder;
        public Cell[] cells;
        public CombineRecipe recipe;
        public List<Ingredient> ingredients = new List<Ingredient>();
    }

    struct Ingredient
    {
        public IngredientKind kind;
        public UnitData unit;
        public ItemData item;
        public UnitGrade grade;
        public int count;
        public UnitData alt;   // 「A 또는 B」 재료(RecipeIngredient.alternativeUnit)
    }

    CombineSystem system;
    RectTransform panel, tab, content;
    TMP_InputField input;
    TMP_Text infoText;
    TMP_Text materialHeader;
    readonly List<TMP_Text> texts = new List<TMP_Text>();
    readonly List<Row> rows = new List<Row>();
    Image[] chipImages;
    bool fontApplied;

    bool open;
    float slide;
    bool searchFocused;
    string query = "";
    int gradeChip;
    bool nowOnly;
    bool dirty = true;
    bool thumbsDirty;
    float nextOwnershipRefresh, nextNowRebuild;
    int shownCount;

    // ownership 계산용 임시 버퍼
    readonly Dictionary<UnitData, int> ownedUnits = new Dictionary<UnitData, int>();
    readonly Dictionary<UnitGrade, int> ownedGrades = new Dictionary<UnitGrade, int>();
    readonly Dictionary<ItemData, int> ownedItems = new Dictionary<ItemData, int>();

    void Awake()
    {
        system = FindFirstObjectByType<CombineSystem>();
        Build();
        UnitThumbBaker.Baked += OnThumbBaked;
    }

    void OnDestroy()
    {
        UnitThumbBaker.Baked -= OnThumbBaked;
        if (searchFocused) ChatInputGate.IsOpen = false;
    }

    void OnThumbBaked() => thumbsDirty = true;

    // ───────────────────────── 만들기 ─────────────────────────

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildTab();
        BuildPanel();
        panel.gameObject.SetActive(false);
    }

    // 워크3(원랜디) 결: 돌/금 테두리 9-슬라이스(dialog_panel_9s) + 남색 단추(button_navy_9s) + 어두운 홈(console_cell_frame_9s). 각진 테두리가 우선이다.
    // 그림이 없으면(Resources/UI/Skin) 색 칠한 칸으로 물러난다(UiSkin.Apply). multiplier = Image.pixelsPerUnitMultiplier — 클수록 9-슬라이스 테두리가 얇아진다.
    static void Skin(Image image, string name, Color fallback, float multiplier = 1f)
    {
        if (UiSkin.Apply(image, name, fallback))
        {
            image.pixelsPerUnitMultiplier = multiplier;
            image.raycastTarget = true;
        }
    }

    // 둥글지 않은 얇은 사각 테두리(위·아래·왼·오른 4장). 켜고 끌 수 있게 묶음 오브젝트를 돌려준다.
    static GameObject AddSquareBorder(RectTransform parent, string name, Color color, float thickness)
    {
        RectTransform holder = NewRect(name, parent);
        Stretch(holder, 0f, 0f, 0f, 0f);
        void Edge(string edge, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            RectTransform r = NewRect(edge, holder);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
            Image image = r.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
        Edge("T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        Edge("B", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        Edge("L", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        Edge("R", new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        return holder.gameObject;
    }

    static void SetBorderColor(GameObject border, Color color)
    {
        foreach (Image image in border.GetComponentsInChildren<Image>(true)) image.color = color;
    }

    const float Inset = 26f;   // 돌 테두리(16px) 안쪽 여백

    void BuildTab()
    {
        tab = NewRect("Tab", transform);
        tab.anchorMin = tab.anchorMax = new Vector2(1f, 0.5f);
        tab.pivot = new Vector2(1f, 0.5f);
        tab.sizeDelta = new Vector2(TabWidth, TabHeight);
        tab.anchoredPosition = new Vector2(0f, TabOffsetY);
        Image bg = tab.gameObject.AddComponent<Image>();
        Skin(bg, "dialog_panel_9s", new Color(0.12f, 0.19f, 0.36f, 0.97f), 1.7f);
        Button button = tab.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.35f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
        button.colors = colors;
        button.onClick.AddListener(Toggle);

        TMP_Text label = MakeText(tab, "Label", "조\n합\n검\n색", 19f, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(label.rectTransform, 4f, 14f, 4f, 36f);
        label.lineSpacing = -6f;
        TMP_Text key = MakeText(tab, "Key", "F5", 15f, new Color(0.81f, 0.85f, 0.93f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(key.rectTransform, 0f, TabHeight - 36f, TabWidth, 24f);
    }

    void BuildPanel()
    {
        panel = NewRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = new Vector2(ClosedX, 0f);
        Image bg = panel.gameObject.AddComponent<Image>();
        Skin(bg, "dialog_panel_9s", PanelFill);   // 난이도 대화상자와 같은 돌/금 테두리 + 남색 안쪽

        TMP_Text title = MakeText(panel, "Title", "조합 검색", 25f, new Color(1f, 0.84f, 0.25f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, Inset, 18f, 300f, 34f);
        TMP_Text close = MakeText(panel, "Close", "✕", 22f, Muted, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(close.rectTransform, PanelWidth - Inset - 36f, 18f, 36f, 34f);
        close.raycastTarget = true;
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => SetOpen(false));

        // [크게 보기] — 조합 도우미(큰 창)로 펼친다(사장님 10-07: 서랍이 기본, 단추를 누르면 크게). 서랍의 검색어를 넘기고, 도우미의 [작게 보기]·Esc가 검색어를 들고 돌아온다.
        {
            RectTransform helperBtn = NewRect("HelperButton", panel);
            Place(helperBtn, PanelWidth - Inset - 36f - 124f, 16f, 116f, 34f);
            Image helperImage = helperBtn.gameObject.AddComponent<Image>();
            Skin(helperImage, "button_navy_9s", ChipOff);
            helperBtn.gameObject.AddComponent<Button>().onClick.AddListener(OpenHelper);
            TMP_Text helperLabel = MakeText(helperBtn, "Label", "크게 보기", 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(helperLabel.rectTransform, 0f, 0f, 0f, 0f);
        }

        BuildInput();
        BuildChips();

        infoText = MakeText(panel, "Info", "", 15f, Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        Place(infoText.rectTransform, Inset, 150f, PanelWidth - Inset * 2f, 24f);

        BuildScroll();
    }

    void BuildInput()
    {
        RectTransform inputRect = NewRect("Input", panel);
        Place(inputRect, Inset, 58f, PanelWidth - Inset * 2f, 44f);
        Image bg = inputRect.gameObject.AddComponent<Image>();
        Skin(bg, "console_cell_frame_9s", new Color(0.02f, 0.035f, 0.09f, 1f), 3f);   // 어두운 홈

        RectTransform viewport = NewRect("Text Area", inputRect);
        Stretch(viewport, 14f, 5f, 14f, 5f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MakeText(viewport, "Text", "", 21f, new Color(0.91f, 0.90f, 0.86f, 1f), TextAlignmentOptions.Left, FontStyles.Normal);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.raycastTarget = false;
        TMP_Text placeholder = MakeText(viewport, "Placeholder", "이름 · 별명 · 코드 · 초성(ㄱㅈㄱ)", 18f, new Color(0.45f, 0.47f, 0.52f, 1f), TextAlignmentOptions.Left, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

        // 연결 전에 OnEnable이 돌지 않게 꺼 둔 채 붙인다.
        inputRect.gameObject.SetActive(false);
        input = inputRect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = bg;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.onValueChanged.AddListener(value => { query = value ?? ""; dirty = true; });
        input.onSelect.AddListener(_ => { searchFocused = true; ChatInputGate.IsOpen = true; });
        input.onDeselect.AddListener(_ => { searchFocused = false; ChatInputGate.IsOpen = false; });
        inputRect.gameObject.SetActive(true);
    }

    GameObject[] chipBorders;

    void BuildChips()
    {
        int count = GradeChipLabels.Length + 1;
        chipImages = new Image[count];
        chipBorders = new GameObject[count];
        float x = Inset;
        for (int i = 0; i < count; i++)
        {
            bool isNow = i == GradeChipLabels.Length;
            string label = isNow ? "지금 가능" : GradeChipLabels[i];
            float width = isNow ? 88f : 55f;
            RectTransform chip = NewRect("Chip" + i, panel);
            Place(chip, x, 110f, width, 30f);
            Image bg = chip.gameObject.AddComponent<Image>();
            Skin(bg, "button_navy_9s", ChipOff);
            chipImages[i] = bg;
            Button button = chip.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.45f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            button.colors = colors;
            int captured = i;
            button.onClick.AddListener(() => OnChip(captured));
            TMP_Text text = MakeText(chip, "Label", label, 16f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            chipBorders[i] = AddSquareBorder(chip, "GoldBorder", Gold, 2f);   // 누른 칩 = 금빛 테두리
            x += width + 4f;
        }
        RefreshChips();
    }

    void BuildScroll()
    {
        RectTransform scrollRect = NewRect("Scroll", panel);
        Place(scrollRect, Inset - 4f, 180f, PanelWidth - Inset * 2f + 8f, PanelHeight - 180f - Inset);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        Image hit = scrollRect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0.001f);

        const float barWidth = 14f;
        RectTransform viewport = NewRect("Viewport", scrollRect);
        Stretch(viewport, 0f, 0f, barWidth + 4f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.padding = new RectOffset(2, 2, 2, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 스크롤바: 어두운 홈 + 금색 손잡이.
        RectTransform barRect = NewRect("Scrollbar", scrollRect);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-barWidth, 0f);
        barRect.offsetMax = Vector2.zero;
        Image track = barRect.gameObject.AddComponent<Image>();
        track.color = new Color(0.02f, 0.025f, 0.05f, 1f);
        AddSquareBorder(barRect, "Edge", new Color(0.30f, 0.31f, 0.34f, 1f), 1f);
        RectTransform sliding = NewRect("Sliding Area", barRect);
        Stretch(sliding, 2f, 2f, 2f, 2f);
        RectTransform handleRect = NewRect("Handle", sliding);
        handleRect.sizeDelta = Vector2.zero;
        Image handle = handleRect.gameObject.AddComponent<Image>();
        handle.color = new Color(0.78f, 0.62f, 0.30f, 1f);
        Scrollbar bar = barRect.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handleRect;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 2f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        materialHeader = MakeText(content, "MaterialHeader", "", 17f, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
        materialHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        materialHeader.gameObject.SetActive(false);
    }

    Row NewRow()
    {
        var row = new Row();
        RectTransform root = NewRect("Row" + rows.Count, content);
        row.root = root.gameObject;
        root.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = RowFill;   // 각진 어두운 남색 판
        row.stone = AddSquareBorder(root, "StoneBorder", new Color(0.30f, 0.31f, 0.35f, 1f), 2f);
        row.gold = AddSquareBorder(root, "GoldBorder", Gold, 2f);   // 고른 줄 = 금빛 외곽
        row.gold.SetActive(false);
        root.gameObject.AddComponent<Button>().onClick.AddListener(() => OnRowClicked(row));

        // 결과 초상: 워크3 명령 칸처럼 테두리 있는 정사각(console_cell_frame) + 등급색 얇은 안쪽 테두리.
        RectTransform frame = NewRect("PortraitFrame", root);
        Place(frame, 8f, 8f, 60f, 60f);
        Image frameImage = frame.gameObject.AddComponent<Image>();
        Skin(frameImage, "console_cell_frame_9s", new Color(0.02f, 0.02f, 0.03f, 1f), 3f);
        frameImage.raycastTarget = false;
        RectTransform portrait = NewRect("Portrait", frame);
        Stretch(portrait, 8f, 8f, 8f, 8f);
        row.portrait = portrait.gameObject.AddComponent<Image>();
        row.portrait.raycastTarget = false;
        row.gradeBorder = AddSquareBorder(portrait, "GradeBorder", Color.white, 2f);

        row.title = MakeText(root, "Title", "", 20f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(row.title.rectTransform, 76f, 8f, 440f, 28f);
        row.title.overflowMode = TextOverflowModes.Ellipsis;
        row.title.richText = true;
        row.sub = MakeText(root, "Sub", "", 14f, Muted, TextAlignmentOptions.Left, FontStyles.Normal);
        Place(row.sub.rectTransform, 76f, 38f, 440f, 22f);
        row.sub.overflowMode = TextOverflowModes.Ellipsis;

        row.cells = new Cell[MaxCells];
        for (int i = 0; i < MaxCells; i++)
        {
            var cell = new Cell();
            RectTransform cellRoot = NewRect("Cell" + i, root);
            Place(cellRoot, 6f + i * CellWidth, 72f, CellWidth - 2f, 72f);
            cell.root = cellRoot.gameObject;
            RectTransform cf = NewRect("Frame", cellRoot);
            Place(cf, 6f, 0f, 48f, 48f);
            Image cfImage = cf.gameObject.AddComponent<Image>();
            Skin(cfImage, "console_cell_frame_9s", new Color(0.02f, 0.02f, 0.03f, 1f), 3.4f);
            cfImage.raycastTarget = false;
            RectTransform cp = NewRect("Portrait", cf);
            Stretch(cp, 6f, 6f, 6f, 6f);
            cell.portrait = cp.gameObject.AddComponent<Image>();
            cell.portrait.raycastTarget = false;
            cell.label = MakeText(cellRoot, "Label", "", 11f, new Color(0.85f, 0.86f, 0.9f, 1f), TextAlignmentOptions.Top, FontStyles.Normal);
            Place(cell.label.rectTransform, -2f, 49f, CellWidth + 2f, 28f);
            cell.label.lineSpacing = -10f;
            cell.mark = MakeText(cellRoot, "Mark", "", 15f, Have, TextAlignmentOptions.TopRight, FontStyles.Bold);
            Place(cell.mark.rectTransform, 22f, -3f, 40f, 20f);
            row.cells[i] = cell;
        }
        rows.Add(row);
        if (fontApplied) ApplyFont();
        return row;
    }

    // ── 작은 도우미
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform r, float left, float top, float right, float bottom)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    static void AddRing(RectTransform parent, float radius, float thickness, Color? color = null)
    {
        RectTransform ring = NewRect("Border", parent);
        Stretch(ring, 0f, 0f, 0f, 0f);
        Image image = ring.gameObject.AddComponent<Image>();
        image.sprite = UiSkin.RoundRing(radius, thickness);
        image.type = Image.Type.Sliced;
        image.color = color ?? Gold;
        image.raycastTarget = false;
    }

    TMP_Text MakeText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        texts.Add(t);
        return t;
    }

    // GameHud의 한글 SDF가 늦게 준비될 수 있다 — 준비되면 한 번 전부 바꿔 끼운다(없는 채로 한글을 넣으면 네모·경고).
    void ApplyFont()
    {
        TMP_FontAsset font = GameHud.UiFontAsset;
        if (font == null) return;
        foreach (TMP_Text t in texts) if (t != null) t.font = font;
        fontApplied = true;
    }

    // ───────────────────────── 동작 ─────────────────────────

    void Toggle() => SetOpen(!open);

    void SetOpen(bool value)
    {
        if (open == value) return;
        open = value;
        if (open)
        {
            panel.gameObject.SetActive(true);
            dirty = true;
        }
        else if (searchFocused)
        {
            input.DeactivateInputField();
            ChatInputGate.IsOpen = false;
            searchFocused = false;
        }
    }

    // 서랍 → 조합 도우미. 서랍은 접고(검색어는 그대로 둠), 도우미가 [작게 보기]로 돌아오면 다시 펼치고 검색어를 이어받는다.
    void OpenHelper()
    {
        string current = query;
        SetOpen(false);
        RecipeHelperPanel.Show(current, back =>
        {
            query = back ?? "";
            if (input != null && input.text != query) input.SetTextWithoutNotify(query);
            dirty = true;
            SetOpen(true);
        });
    }

    void OnChip(int index)
    {
        if (index == GradeChipLabels.Length) nowOnly = !nowOnly;
        else gradeChip = index;
        RefreshChips();
        dirty = true;
    }

    void RefreshChips()
    {
        for (int i = 0; i < chipImages.Length; i++)
        {
            bool on = i == GradeChipLabels.Length ? nowOnly : i == gradeChip;
            if (chipBorders != null && chipBorders[i] != null) chipBorders[i].SetActive(on);
            TMP_Text label = chipImages[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = on ? new Color(1f, 0.84f, 0.25f, 1f) : Color.white;
        }
    }

    CombineRecipe selectedRecipe;

    void OnRowClicked(Row row)
    {
        if (row.recipe == null) return;
        selectedRecipe = row.recipe;
        foreach (Row other in rows) if (other.gold != null) other.gold.SetActive(other.root.activeSelf && other.recipe == selectedRecipe);
        if (!RecipeLocator.Locate(row.recipe))
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, "조합판에서 그 식의 인형을 찾지 못했습니다.", 4f);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.f5Key.wasPressedThisFrame && (!ChatInputGate.IsOpen || searchFocused)) Toggle();
            else if (open && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        if (!fontApplied && GameHud.UiFontAsset != null) ApplyFont();

        float target = open ? 1f : 0f;
        if (!Mathf.Approximately(slide, target))
        {
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime * 6f);
            float eased = 1f - (1f - slide) * (1f - slide);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(ClosedX + PanelWidth, OpenX, eased), 0f);
            if (slide <= 0f) panel.gameObject.SetActive(false);
        }
        if (!open) return;

        if (dirty || (nowOnly && Time.unscaledTime >= nextNowRebuild))
        {
            dirty = false;
            nextNowRebuild = Time.unscaledTime + 1f;
            Rebuild();
            thumbsDirty = true;
        }
        if (thumbsDirty || Time.unscaledTime >= nextOwnershipRefresh)
        {
            thumbsDirty = false;
            nextOwnershipRefresh = Time.unscaledTime + 0.5f;
            RefreshVisuals();
        }
    }

    // ───────────────────────── 검색 ─────────────────────────

    bool PassesGrade(UnitGrade grade)
    {
        UnitGrade? wanted = GradeChipGrades[gradeChip];
        return !wanted.HasValue || grade == wanted.Value || (wanted.Value == UnitGrade.Rare && grade == UnitGrade.Superior);
    }

    readonly List<CombineRecipe> direct = new List<CombineRecipe>();
    readonly List<CombineRecipe> asMaterial = new List<CombineRecipe>();

    void Rebuild()
    {
        direct.Clear();
        asMaterial.Clear();
        string nq = HangulSearch.Normalize(query);
        IReadOnlyList<CombineRecipe> recipes = system != null ? system.Recipes : null;
        if (recipes != null)
        {
            foreach (CombineRecipe recipe in recipes)
            {
                if (recipe == null || recipe.result == null) continue;
                if (!PassesGrade(recipe.result.grade)) continue;
                if (nowOnly && !system.CanCombineNow(recipe)) continue;
                if (nq.Length == 0) { direct.Add(recipe); continue; }
                if (HangulSearch.UnitMatches(nq, recipe.result) || HangulSearch.Matches(nq, HangulSearch.Normalize(recipe.commandId)))
                {
                    direct.Add(recipe);
                    continue;
                }
                if (recipe.ingredients == null) continue;
                foreach (RecipeIngredient ing in recipe.ingredients)
                    if (ing != null && ing.kind == IngredientKind.SpecificUnit && ing.unit != null && (HangulSearch.UnitMatches(nq, ing.unit) || (ing.alternativeUnit != null && HangulSearch.UnitMatches(nq, ing.alternativeUnit))))
                    {
                        asMaterial.Add(recipe);
                        break;
                    }
            }
        }

        int total = direct.Count + asMaterial.Count;
        int directShown = Mathf.Min(direct.Count, MaxRows);
        int materialShown = Mathf.Min(asMaterial.Count, MaxRows - directShown);
        shownCount = directShown + materialShown;
        while (rows.Count < shownCount) NewRow();

        int sibling = 0;
        for (int i = 0; i < rows.Count; i++) rows[i].root.SetActive(false);
        for (int i = 0; i < directShown; i++) Fill(rows[i], direct[i], sibling++);
        bool header = materialShown > 0;
        materialHeader.gameObject.SetActive(header);
        if (header)
        {
            materialHeader.text = nq.Length > 0 ? $"▼ 「{query.Trim()}」이(가) 재료로 쓰이는 식" : "▼ 재료로 쓰이는 식";
            materialHeader.transform.SetSiblingIndex(sibling++);
        }
        for (int i = 0; i < materialShown; i++) Fill(rows[directShown + i], asMaterial[i], sibling++);

        string info = nq.Length == 0 ? $"조합식 {direct.Count}개" : $"만드는 법 {direct.Count} · 재료로 쓰임 {asMaterial.Count}";
        if (total > shownCount) info += $"  (앞 {shownCount}개만 표시 — 검색어를 더 쓰세요)";
        else if (total == 0) info = "찾은 식이 없습니다";
        infoText.text = info;
        content.anchoredPosition = Vector2.zero;
    }

    void Fill(Row row, CombineRecipe recipe, int sibling)
    {
        row.recipe = recipe;
        row.root.SetActive(true);
        row.root.transform.SetSiblingIndex(sibling);
        UnitData result = recipe.result;
        string gradeHex = ColorUtility.ToHtmlStringRGB(result.grade.Color());
        string alias = FirstAlias(result);
        row.title.text = $"<color=#{gradeHex}>{result.grade.KoreanName()} {result.DisplayName}</color>" + (alias != null ? $" <color=#9a9aa2><size=85%>· {alias}</size></color>" : "");
        row.title.color = Color.white;
        SetBorderColor(row.gradeBorder, result.grade.Color());
        row.gold.SetActive(recipe == selectedRecipe);
        row.sub.text = CostLine(recipe);

        row.ingredients.Clear();
        if (recipe.ingredients != null)
            foreach (RecipeIngredient ing in recipe.ingredients)
            {
                if (ing == null) continue;
                int at = row.ingredients.FindIndex(e => e.kind == ing.kind &&
                    (ing.kind == IngredientKind.SpecificUnit ? e.unit == ing.unit : ing.kind == IngredientKind.SpecificItem ? e.item == ing.item : e.grade == ing.wildcardGrade));
                int count = Mathf.Max(1, ing.count);
                if (at >= 0)
                {
                    Ingredient merged = row.ingredients[at];
                    merged.count += count;
                    row.ingredients[at] = merged;
                }
                else
                    row.ingredients.Add(new Ingredient { kind = ing.kind, unit = ing.unit, item = ing.item, grade = ing.wildcardGrade, count = count, alt = ing.alternativeUnit });
            }

        for (int i = 0; i < MaxCells; i++)
        {
            Cell cell = row.cells[i];
            if (i >= row.ingredients.Count) { cell.root.SetActive(false); continue; }
            cell.root.SetActive(true);
            Ingredient ing = row.ingredients[i];
            cell.label.text = ing.kind == IngredientKind.SpecificUnit && ing.unit != null ? (ing.alt != null ? $"{ing.unit.DisplayNameTwoLines}\n또는 {ing.alt.DisplayName}" : ing.unit.DisplayNameTwoLines)
                : ing.kind == IngredientKind.SpecificItem && ing.item != null ? ing.item.itemName
                : $"아무 {ing.grade.KoreanName()}";
        }
    }

    // 스킨 캐릭터 이름(UnitData.skinAlias, 쉼표 구분)의 첫 번째 — 결과 이름 옆 회색으로.
    static string FirstAlias(UnitData unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.skinAlias)) return null;
        foreach (string part in unit.skinAlias.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0) return trimmed;
        }
        return null;
    }

    static string CostLine(CombineRecipe recipe)
    {
        var sb = new StringBuilder();
        if (recipe.goldCost > 0) sb.Append("금화 ").Append(recipe.goldCost).Append("  ");
        if (recipe.resourceCosts != null)
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
                if (cost != null && cost.amount > 0)
                    sb.Append(cost.type == ResourceType.Wood ? "목재" : cost.type.ToString()).Append(' ').Append(cost.amount).Append("  ");
        if (recipe.minRound > 0 || recipe.maxRound > 0) sb.Append("라운드 ").Append(recipe.minRound > 0 ? recipe.minRound.ToString() : "").Append('~').Append(recipe.maxRound > 0 ? recipe.maxRound.ToString() : "").Append("  ");
        if (!string.IsNullOrEmpty(recipe.commandId)) sb.Append("코드 ").Append(recipe.commandId);
        return sb.ToString().TrimEnd();
    }

    // 초상 그림·보유 ✔/✘를 다시 맞춘다(0.5초마다 + 새 썸네일이 구워졌을 때).
    void RefreshVisuals()
    {
        CountOwned();
        for (int r = 0; r < rows.Count; r++)
        {
            Row row = rows[r];
            if (!row.root.activeSelf || row.recipe == null) continue;
            SetPortrait(row.portrait, row.recipe.result, row.recipe.result.grade.Color());
            for (int i = 0; i < row.ingredients.Count && i < MaxCells; i++)
            {
                Ingredient ing = row.ingredients[i];
                Cell cell = row.cells[i];
                int owned = Owned(ing);
                bool have = owned >= ing.count;
                string countText = ing.count > 1 ? $"×{ing.count}" : "";
                cell.mark.text = (have ? "✔" : "✘") + countText;
                cell.mark.color = have ? Have : Lack;
                Color tint = have ? Color.white : new Color(1f, 1f, 1f, 0.38f);
                if (ing.kind == IngredientKind.SpecificUnit) SetPortrait(cell.portrait, ing.unit, ing.unit != null ? ing.unit.grade.Color() : Lack);
                else if (ing.kind == IngredientKind.UnitGradeWildcard)
                {
                    Sprite wisp = WispIconBaker.Cached;
                    cell.portrait.sprite = wisp;
                    cell.portrait.color = wisp != null ? Color.white : new Color(0.3f, 0.5f, 0.8f, 1f);
                }
                else { cell.portrait.sprite = null; cell.portrait.color = new Color(0.55f, 0.45f, 0.2f, 1f); }
                if (cell.portrait.sprite != null) cell.portrait.color = tint;
                else cell.portrait.color = new Color(cell.portrait.color.r, cell.portrait.color.g, cell.portrait.color.b, have ? 1f : 0.38f);
                cell.label.color = have ? new Color(0.85f, 0.86f, 0.9f, 1f) : Lack;
            }
        }
    }

    static void SetPortrait(Image image, UnitData unit, Color gradeColor)
    {
        Sprite sprite = UnitThumbBaker.Get(unit);
        image.sprite = sprite;
        // 아직 안 구워졌거나 모델이 없으면 등급색 상자(구워지면 Baked로 다시 그린다).
        image.color = sprite != null ? Color.white : new Color(gradeColor.r * 0.45f, gradeColor.g * 0.45f, gradeColor.b * 0.45f, 1f);
    }

    void CountOwned()
    {
        ownedUnits.Clear();
        ownedGrades.Clear();
        ownedItems.Clear();
        PlayerContext me = PlayerContext.Local;
        if (me == null) return;
        if (me.UnitInventory != null)
            foreach (UnitData unit in me.UnitInventory.Units)
            {
                if (unit == null) continue;
                ownedUnits[unit] = (ownedUnits.TryGetValue(unit, out int n) ? n : 0) + 1;
                ownedGrades[unit.grade] = (ownedGrades.TryGetValue(unit.grade, out int g) ? g : 0) + 1;
            }
        if (me.ItemInventory != null)
            foreach (ItemData item in me.ItemInventory.Items)
                if (item != null) ownedItems[item] = (ownedItems.TryGetValue(item, out int n) ? n : 0) + 1;
    }

    int Owned(Ingredient ing)
    {
        int n = 0;
        switch (ing.kind)
        {
            case IngredientKind.SpecificUnit: if (ing.unit != null) ownedUnits.TryGetValue(ing.unit, out n); if (ing.alt != null && ownedUnits.TryGetValue(ing.alt, out int altOwned)) n += altOwned; break;
            case IngredientKind.SpecificItem: if (ing.item != null) ownedItems.TryGetValue(ing.item, out n); break;
            default: ownedGrades.TryGetValue(ing.grade, out n); break;
        }
        return n;
    }
}
