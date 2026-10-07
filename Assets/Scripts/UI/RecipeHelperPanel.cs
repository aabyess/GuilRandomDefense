using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 조합 도우미(tmo build-helper 식, 사장님 10-07) — F5 조합 검색 서랍의 [크게 보기]로 여는 큰 창. 등급별 열에 유닛을 늘어놓고 유닛마다 진행률 %(RecipeProgress)·능력 꼬리표(SkillTags) 필터·정렬·내 능력치 합계·툴팁을 보여 준다.
/// 설계표 Docs/design/RECIPE_HELPER_DESIGN_2026-10-06.md. 실제 보유만 본다(가상 +/− 계획 모드 없음). 전부 로컬 UI — 서버·네트워크 안 감.
/// 진입은 서랍의 [크게 보기] 하나뿐(단독 단축키 없음). 서랍의 검색어를 이어받고, [작게 보기]·Esc는 서랍으로 돌아가며 검색어를 돌려준다. ✕는 전부 닫는다.
/// 모양: 하단 바 C 금속(청동 액자)과 같은 톤 — 어두운 금속 판 + 청동 테두리. 등급은 탭(기본 등급 / 전설·히든 / 제한됨 이상)으로 묶어 한 화면 열을 6개 이하로, 유닛 칸은 크게.
/// </summary>
public class RecipeHelperPanel : MonoBehaviour
{
    /// <summary>호환용 스위치(탐침·옛 코드). 진입은 서랍 단추라 기본 true.</summary>
    public static bool Enabled { get; set; } = true;

    public static RecipeHelperPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    System.Action<string> onBack;

    /// <summary>서랍의 [크게 보기]에서 부른다 — query는 서랍의 검색어, onBack은 [작게 보기] 때 서랍으로 검색어를 돌려주는 콜백.</summary>
    public static void Show(string query, System.Action<string> back)
    {
        if (Instance == null) new GameObject("[조합 도우미]").AddComponent<RecipeHelperPanel>();
        Instance.onBack = back;
        Instance.SetQuery(query ?? "");
        Instance.SetOpen(true);
    }

    // ── 모양(1920×1080 기준 px, 캔버스가 비례 확대)
    const float PanelW = 1760f, PanelH = 960f, Pad = 40f, RowH = 76f, RowGap = 5f, HeaderH = 40f;
    static readonly Color PanelFill = new Color(0.06f, 0.065f, 0.08f, 0.985f);
    static readonly Color Bronze = new Color(0.79f, 0.62f, 0.30f, 1f);
    static readonly Color BronzeDark = new Color(0.36f, 0.27f, 0.12f, 1f);
    static readonly Color RowFill = new Color(0.11f, 0.115f, 0.14f, 0.97f);
    static readonly Color Muted = new Color(0.68f, 0.66f, 0.60f, 1f);
    static readonly Color BtnOff = new Color(0.16f, 0.16f, 0.19f, 1f);
    static readonly Color BtnOn = new Color(0.52f, 0.38f, 0.12f, 1f);

    enum SortMode { Default, High, Low }

    // 열 순서(등급 → 열). 전설·히든은 딜 종류(물딜/마딜)로 갈라 두 열이 된다. tab: 0 기본 등급 · 1 전설·히든 · 2 제한됨 이상
    struct ColumnDef { public string title; public UnitGrade grade; public int damage; public int tab; }   // damage: 0 = 상관없음, 1 = 물딜(AD 포함), 2 = 마딜(AP만)

    class Cell
    {
        public UnitData unit;
        public GameObject root;
        public Image bg, portrait, barFill, outline;
        public TMP_Text name, tags, percent;
        public CombineRecipe recipe;
        public float pct;
        public SkillTag skillTags;
        public bool matches = true;
        public int defaultOrder;
    }

    class Column
    {
        public ColumnDef def;
        public RectTransform root;
        public readonly List<Cell> cells = new List<Cell>();
        public TMP_Text header;
    }

    static readonly string[] TabNames = { "기본 등급", "전설 · 히든", "제한됨 이상" };

    // ── 상태
    bool open;
    CombineSystem system;
    RecipeProgress progress;
    readonly List<UnitData> units = new List<UnitData>();
    readonly List<Column> columns = new List<Column>();
    SortMode sort = SortMode.Default;
    SkillTag filter = SkillTag.None;
    string query = "";
    string normalizedQuery = "";
    bool hideNonMatching;
    int tab;
    bool dirty = true;
    float nextRefresh;
    RectTransform panel, gridContent, filterPopup;
    TMP_InputField searchInput;
    TMP_Text totalsText, tooltipText, filterButtonLabel;
    GameObject tooltip;
    readonly List<TMP_Text> texts = new List<TMP_Text>();
    readonly Dictionary<SkillTag, Image> chipImages = new Dictionary<SkillTag, Image>();
    Image[] sortButtons, tabButtons;
    Image hideToggleImage;
    readonly Dictionary<UnitData, int> ownedUnits = new Dictionary<UnitData, int>();
    readonly Dictionary<ItemData, int> ownedItems = new Dictionary<ItemData, int>();
    bool fontApplied, searchFocused;

    void Awake()
    {
        Instance = this;
        system = FindFirstObjectByType<CombineSystem>();
        BuildData();
        Build();
        panel.gameObject.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; UnitThumbBaker.Baked -= OnThumbBaked; if (searchFocused) ChatInputGate.IsOpen = false; }

    void OnThumbBaked() => dirty = true;

    // ───────────────────────── 데이터 ─────────────────────────

    void BuildData()
    {
        var recipes = system != null ? system.Recipes : null;
        var set = new HashSet<UnitData>();
        if (recipes != null)
            foreach (CombineRecipe r in recipes)
            {
                if (r == null || r.result == null) continue;
                set.Add(r.result);
                if (r.ingredients != null)
                    foreach (RecipeIngredient ing in r.ingredients)
                    {
                        if (ing == null) continue;
                        if (ing.unit != null) set.Add(ing.unit);
                        if (ing.alternativeUnit != null) set.Add(ing.alternativeUnit);
                    }
            }
        foreach (UnitData loaded in Resources.FindObjectsOfTypeAll<UnitData>()) if (loaded != null && !string.IsNullOrEmpty(loaded.unitName) && loaded.prefab != null) set.Add(loaded);   // 조합식에 안 나오는 유닛(특수함 등)도 — 이미 로드된 로스터
        foreach (UnitData u in set) if (!u.isSystemUnit && u.grade != UnitGrade.TranscendentWisp && u.grade != UnitGrade.Transformed) units.Add(u);
        units.Sort((a, b) => { int t = a.grade.Tier().CompareTo(b.grade.Tier()); return t != 0 ? t : string.CompareOrdinal(a.name, b.name); });
        progress = new RecipeProgress(recipes != null ? recipes : new List<CombineRecipe>(), units);
        UnitThumbBaker.Baked += OnThumbBaked;
    }

    static IEnumerable<ColumnDef> ColumnDefs()
    {
        yield return new ColumnDef { title = "흔함", grade = UnitGrade.Common, tab = 0 };
        yield return new ColumnDef { title = "안흔함", grade = UnitGrade.Uncommon, tab = 0 };
        yield return new ColumnDef { title = "특별함", grade = UnitGrade.Special, tab = 0 };
        yield return new ColumnDef { title = "희귀함", grade = UnitGrade.Rare, tab = 0 };
        yield return new ColumnDef { title = "특수함", grade = UnitGrade.Superior, tab = 0 };
        yield return new ColumnDef { title = "전설[물딜]", grade = UnitGrade.Legendary, damage = 1, tab = 1 };
        yield return new ColumnDef { title = "전설[마딜]", grade = UnitGrade.Legendary, damage = 2, tab = 1 };
        yield return new ColumnDef { title = "히든[물딜]", grade = UnitGrade.Hidden, damage = 1, tab = 1 };
        yield return new ColumnDef { title = "히든[마딜]", grade = UnitGrade.Hidden, damage = 2, tab = 1 };
        yield return new ColumnDef { title = "제한됨", grade = UnitGrade.Limited, tab = 2 };
        yield return new ColumnDef { title = "초월", grade = UnitGrade.Transcendent, tab = 2 };
        yield return new ColumnDef { title = "불멸", grade = UnitGrade.Immortal, tab = 2 };
        yield return new ColumnDef { title = "영원", grade = UnitGrade.Eternal, tab = 2 };
        yield return new ColumnDef { title = "신비함", grade = UnitGrade.OtherWorld, tab = 2 };   // tmo 「신비함」 = 우리 다른세계 식 결과(PM 확인 §11)
        yield return new ColumnDef { title = "랜덤유닛", grade = UnitGrade.RandomUnit, tab = 2 };
    }

    static bool InColumn(UnitData u, ColumnDef def)
    {
        if (u.grade != def.grade) return false;
        if (def.damage == 0) return true;
        bool ad = (u.damageType & DamageType.AD) != 0, ap = (u.damageType & DamageType.AP) != 0;
        return def.damage == 1 ? (ad || !ap) : (ap && !ad);   // AD가 조금이라도 있으면 물딜 열, AP만이면 마딜 열
    }

    // ───────────────────────── 만들기 ─────────────────────────

    TMP_Text MakeText(Transform parent, string value, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = value; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; t.richText = true;
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        texts.Add(t);
        return t;
    }

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

    static void Stretch(RectTransform r, float l, float t, float rt, float b)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
    }

    // 청동 테두리 한 줄(4변) — 켜고 끌 수 있게 묶음 오브젝트를 돌려준다.
    static Image AddFrame(RectTransform parent, Color color, float thickness)
    {
        RectTransform holder = NewRect("Frame", parent);
        Stretch(holder, 0f, 0f, 0f, 0f);
        Image first = null;
        void Edge(Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            RectTransform r = NewRect("E", holder);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
            Image im = r.gameObject.AddComponent<Image>(); im.color = color; im.raycastTarget = false;
            if (first == null) first = im;
        }
        Edge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        Edge(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        Edge(Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        Edge(new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero);
        return first;
    }

    Image AddButton(Transform parent, string label, float x, float y, float w, float h, System.Action onClick, out TMP_Text text, float size = 17f)
    {
        RectTransform r = NewRect("Btn", parent);
        Place(r, x, y, w, h);
        Image bg = r.gameObject.AddComponent<Image>();
        bg.color = BtnOff;
        var btn = r.gameObject.AddComponent<Button>();
        ColorBlock cb = btn.colors; cb.highlightedColor = new Color(1.2f, 1.2f, 1.25f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f); btn.colors = cb;
        btn.onClick.AddListener(() => onClick());
        AddFrame(r, BronzeDark, 2f);
        text = MakeText(r, label, size, new Color(0.95f, 0.92f, 0.82f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform, 2f, 0f, 2f, 0f);
        return bg;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // 판: 하단 바 C 금속과 같은 청동 액자(UiSkin.BarCell) + 어두운 금속 안쪽. 그림이 없으면 색 판 + 청동 테두리.
        panel = NewRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panel.anchoredPosition = Vector2.zero;
        Image bg = panel.gameObject.AddComponent<Image>();
        Sprite cell = UiSkin.BarCell();
        if (cell != null) { bg.sprite = cell; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.pixelsPerUnitMultiplier = 1f; }
        else { bg.color = PanelFill; AddFrame(panel, Bronze, 3f); }
        // 안쪽 금속 판(청동 액자 안을 한 겹 더 어둡게 — 글자 대비)
        RectTransform inner = NewRect("Inner", panel);
        Stretch(inner, 12f, 12f, 12f, 12f);
        Image innerImage = inner.gameObject.AddComponent<Image>(); innerImage.color = PanelFill; innerImage.raycastTarget = false;

        // 1줄: 제목 · 검색 · 필터 · 정렬 · 작게 보기 · 닫기
        TMP_Text title = MakeText(panel, "조합 도우미", 30f, new Color(1f, 0.84f, 0.25f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, Pad, 26f, 230f, 44f);
        BuildSearch(Pad + 236f, 28f, 340f, 42f);
        AddButton(panel, "능력 필터", Pad + 592f, 28f, 190f, 42f, ToggleFilterPopup, out filterButtonLabel, 17f);
        sortButtons = new Image[3];
        string[] sortNames = { "기본순", "높은순", "낮은순" };
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            sortButtons[i] = AddButton(panel, sortNames[i], Pad + 800f + i * 100f, 28f, 94f, 42f, () => { sort = (SortMode)captured; dirty = true; }, out _);
        }
        hideToggleImage = AddButton(panel, "안 맞는 칸 숨김", Pad + 1112f, 28f, 170f, 42f, () => { hideNonMatching = !hideNonMatching; dirty = true; }, out _, 16f);
        AddButton(panel, "작게 보기", PanelW - Pad - 84f - 150f, 28f, 142f, 42f, GoBack, out _, 17f);
        AddButton(panel, "닫기", PanelW - Pad - 76f, 28f, 76f, 42f, () => SetOpen(false), out _, 17f);

        // 2줄: 등급 탭 + 합계
        tabButtons = new Image[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            int captured = i;
            tabButtons[i] = AddButton(panel, TabNames[i], Pad + i * 176f, 86f, 170f, 40f, () => { tab = captured; dirty = true; LayoutColumns(); }, out _, 17f);
        }
        totalsText = MakeText(panel, "", 16f, new Color(0.88f, 0.9f, 0.95f, 1f), TextAlignmentOptions.Left);
        Place(totalsText.rectTransform, Pad + 540f, 88f, PanelW - Pad * 2f - 540f, 36f);
        totalsText.overflowMode = TextOverflowModes.Ellipsis;

        BuildGrid();
        BuildFilterPopup();
        BuildTooltip();
    }

    void BuildSearch(float x, float y, float w, float h)
    {
        RectTransform inputRect = NewRect("Search", panel);
        Place(inputRect, x, y, w, h);
        Image bg = inputRect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.045f, 1f);
        AddFrame(inputRect, BronzeDark, 2f);
        RectTransform viewport = NewRect("Text Area", inputRect);
        Stretch(viewport, 12f, 4f, 12f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MakeText(viewport, "", 20f, new Color(0.95f, 0.93f, 0.86f, 1f), TextAlignmentOptions.Left);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        TMP_Text placeholder = MakeText(viewport, "이름 · 별명 · 초성(ㄱㅈㄱ)", 18f, new Color(0.5f, 0.5f, 0.55f, 1f), TextAlignmentOptions.Left, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        inputRect.gameObject.SetActive(false);
        searchInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        searchInput.targetGraphic = bg;
        searchInput.textViewport = viewport;
        searchInput.textComponent = text;
        searchInput.placeholder = placeholder;
        searchInput.lineType = TMP_InputField.LineType.SingleLine;
        searchInput.richText = false;
        searchInput.onValueChanged.AddListener(value => { query = value ?? ""; normalizedQuery = HangulSearch.Normalize(query); dirty = true; });
        searchInput.onSelect.AddListener(_ => { searchFocused = true; ChatInputGate.IsOpen = true; });
        searchInput.onDeselect.AddListener(_ => { searchFocused = false; ChatInputGate.IsOpen = false; });
        inputRect.gameObject.SetActive(true);
    }

    // 능력 필터 칩은 접이식 팝업 — 「그 꼬리표를 가진 유닛이 1기 이상」인 것만(데이터 근거 없는 tmo 항목은 자동으로 안 뜬다).
    void BuildFilterPopup()
    {
        filterPopup = NewRect("FilterPopup", panel);
        var present = SkillTags.All.Where(e => units.Any(u => (SkillTags.Of(u) & e.tag) != 0)).ToList();
        const float w = 180f, h = 40f, gap = 8f; const int perRow = 6;
        int rows = Mathf.CeilToInt(present.Count / (float)perRow);
        float popupW = perRow * (w + gap) + gap + 120f, popupH = rows * (h + gap) + gap + 56f;
        Place(filterPopup, Pad + 560f, 76f, popupW, popupH);
        Image bg = filterPopup.gameObject.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.075f, 0.09f, 0.99f);
        AddFrame(filterPopup, Bronze, 2f);
        MakeText(filterPopup, "능력 필터 — 고른 능력을 전부 가진 유닛만 밝게", 15f, Muted, TextAlignmentOptions.Left).rectTransform.SetAsLastSibling();
        Place(texts[texts.Count - 1].rectTransform, gap + 4f, 6f, popupW - 160f, 26f);
        AddButton(filterPopup, "모두 해제", popupW - 128f - gap, 6f, 120f, 30f, () => { filter = SkillTag.None; dirty = true; }, out _, 15f);
        for (int i = 0; i < present.Count; i++)
        {
            SkillTag tag = present[i].tag;
            float x = gap + (i % perRow) * (w + gap), y = 46f + (i / perRow) * (h + gap);
            Image chip = AddButton(filterPopup, present[i].label, x, y, w, h, () => { filter ^= tag; dirty = true; }, out TMP_Text label, 16f);
            label.enableAutoSizing = true; label.fontSizeMin = 11f; label.fontSizeMax = 16f;
            chipImages[tag] = chip;
        }
        filterPopup.gameObject.SetActive(false);
    }

    void ToggleFilterPopup() { filterPopup.gameObject.SetActive(!filterPopup.gameObject.activeSelf); filterPopup.SetAsLastSibling(); if (tooltip != null) tooltip.transform.SetAsLastSibling(); }

    void BuildGrid()
    {
        float top = 138f;
        RectTransform scrollRect = NewRect("Scroll", panel);
        Place(scrollRect, Pad - 6f, top, PanelW - Pad * 2f + 12f, PanelH - top - Pad + 8f);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scrollRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        RectTransform viewport = NewRect("Viewport", scrollRect);
        Stretch(viewport, 0f, 0f, 14f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        gridContent = NewRect("Content", viewport);
        gridContent.anchorMin = new Vector2(0f, 1f); gridContent.anchorMax = new Vector2(1f, 1f); gridContent.pivot = new Vector2(0.5f, 1f);
        scroll.viewport = viewport; scroll.content = gridContent;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 50f;

        // 스크롤바(금속 홈 + 청동 손잡이)
        RectTransform barRect = NewRect("Scrollbar", scrollRect);
        barRect.anchorMin = new Vector2(1f, 0f); barRect.anchorMax = Vector2.one; barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-12f, 0f); barRect.offsetMax = Vector2.zero;
        barRect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 1f);
        RectTransform sliding = NewRect("Sliding", barRect);
        Stretch(sliding, 0f, 0f, 0f, 0f);
        RectTransform handle = NewRect("Handle", sliding);
        Stretch(handle, 0f, 0f, 0f, 0f);
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Bronze;
        Scrollbar bar = barRect.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = handleImage; bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        foreach (ColumnDef def in ColumnDefs())
        {
            var members = units.Where(u => InColumn(u, def)).ToList();
            if (members.Count == 0) continue;
            var column = new Column { def = def };
            column.root = NewRect("Col", gridContent);
            column.header = MakeText(column.root, $"{def.title} <size=70%><color=#9a9aa2>{members.Count}</color></size>", 22f, def.grade.Color(), TextAlignmentOptions.Center, FontStyles.Bold);
            for (int ri = 0; ri < members.Count; ri++) column.cells.Add(BuildCell(column, members[ri], ri));
            columns.Add(column);
        }
        LayoutColumns();
    }

    // 지금 탭의 열만 보이고, 폭은 탭 안 열 수로 나눈다(최대 330). 열 높이는 가장 긴 열 기준.
    void LayoutColumns()
    {
        var visible = columns.Where(c => c.def.tab == tab).ToList();
        float usable = PanelW - Pad * 2f - 14f;
        float colW = Mathf.Min(330f, usable / Mathf.Max(1, visible.Count));
        int maxRows = visible.Count > 0 ? visible.Max(c => c.cells.Count) : 0;
        gridContent.sizeDelta = new Vector2(0f, HeaderH + maxRows * (RowH + RowGap) + 16f);
        float x = 0f;
        foreach (Column column in columns)
        {
            bool show = column.def.tab == tab;
            column.root.gameObject.SetActive(show);
            if (!show) continue;
            Place(column.root, x, 0f, colW - 8f, HeaderH + column.cells.Count * (RowH + RowGap));
            Place(column.header.rectTransform, 0f, 0f, colW - 8f, HeaderH - 4f);
            foreach (Cell cell in column.cells)
            {
                RectTransform rt = (RectTransform)cell.root.transform;
                rt.sizeDelta = new Vector2(colW - 8f, RowH);
                LayoutCellText(cell, colW - 8f);
            }
            x += colW;
        }
        dirty = true;
        for (int i = 0; i < tabButtons.Length; i++) tabButtons[i].color = tab == i ? BtnOn : BtnOff;
    }

    static void LayoutCellText(Cell cell, float width)
    {
        Place(cell.name.rectTransform, 74f, 6f, width - 80f, 26f);
        Place(cell.tags.rectTransform, 74f, 32f, width - 140f, 20f);
        Place(cell.percent.rectTransform, width - 84f, 30f, 78f, 24f);
    }

    Cell BuildCell(Column column, UnitData unit, int order)
    {
        var cell = new Cell { unit = unit, defaultOrder = order, skillTags = SkillTags.Of(unit) };
        if (system != null && system.Recipes != null)
            cell.recipe = system.Recipes.Where(r => r != null && r.result == unit).OrderByDescending(r => r.ingredients != null ? r.ingredients.Count : 0).FirstOrDefault();
        RectTransform root = NewRect("Cell", column.root);
        cell.root = root.gameObject;
        cell.bg = root.gameObject.AddComponent<Image>();
        cell.bg.color = RowFill;
        Place(root, 0f, HeaderH + order * (RowH + RowGap), 300f, RowH);
        root.gameObject.AddComponent<Button>().onClick.AddListener(() => OnCellClicked(cell));
        var trigger = root.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowTooltip(cell));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => HideTooltip());
        cell.outline = AddFrame(root, Bronze, 3f);   // 보유 유닛은 청동 테두리 강조
        cell.outline.transform.parent.gameObject.SetActive(false);

        // 초상: 큰 정사각 + 등급색 테두리 한 줄
        RectTransform pf = NewRect("Portrait", root);
        Place(pf, 6f, 6f, 62f, 62f);
        Color g = unit.grade.Color();
        pf.gameObject.AddComponent<Image>().color = new Color(g.r * 0.5f, g.g * 0.5f, g.b * 0.5f, 1f);
        RectTransform pimg = NewRect("Img", pf);
        Stretch(pimg, 2f, 2f, 2f, 2f);
        cell.portrait = pimg.gameObject.AddComponent<Image>();
        cell.portrait.raycastTarget = false;
        cell.portrait.color = new Color(g.r * 0.45f, g.g * 0.45f, g.b * 0.45f, 1f);

        cell.name = MakeText(root, unit.DisplayName, 18f, g, TextAlignmentOptions.Left, FontStyles.Bold);
        cell.name.overflowMode = TextOverflowModes.Ellipsis;
        cell.tags = MakeText(root, "", 14f, new Color(0.72f, 0.77f, 0.88f, 1f), TextAlignmentOptions.Left);
        cell.tags.overflowMode = TextOverflowModes.Ellipsis;
        string shortTags = SkillTags.ShortList(cell.skillTags, 3);
        cell.tags.text = shortTags.Length > 0 ? shortTags : "";
        cell.percent = MakeText(root, "", 17f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
        LayoutCellText(cell, 300f);

        // 진행률 막대: 칸 아래 전체가 검정 바탕, 채움은 초록(보유·100%는 금색) — 굵게(14px)
        RectTransform barBg = NewRect("BarBg", root);
        barBg.anchorMin = new Vector2(0f, 0f); barBg.anchorMax = new Vector2(1f, 0f); barBg.pivot = new Vector2(0.5f, 0f);
        barBg.offsetMin = new Vector2(74f, 8f); barBg.offsetMax = new Vector2(-8f, 22f);
        barBg.gameObject.AddComponent<Image>().color = Color.black;
        RectTransform fill = NewRect("Fill", barBg);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        cell.barFill = fill.gameObject.AddComponent<Image>();
        cell.barFill.color = new Color(0.25f, 0.8f, 0.3f, 1f);
        return cell;
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    void BuildTooltip()
    {
        RectTransform t = NewRect("Tooltip", transform);
        t.anchorMin = t.anchorMax = t.pivot = new Vector2(0f, 1f);
        t.sizeDelta = new Vector2(480f, 170f);
        Image bg = t.gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.045f, 0.06f, 0.98f); bg.raycastTarget = false;
        AddFrame(t, Bronze, 2f);
        tooltipText = MakeText(t, "", 16f, Color.white, TextAlignmentOptions.TopLeft);
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(tooltipText.rectTransform, 12f, 10f, 12f, 10f);
        tooltip = t.gameObject;
        tooltip.SetActive(false);
        t.SetAsLastSibling();
    }

    // ───────────────────────── 동작 ─────────────────────────

    void SetQuery(string q)
    {
        query = q; normalizedQuery = HangulSearch.Normalize(q);
        if (searchInput != null && searchInput.text != q) searchInput.SetTextWithoutNotify(q);
        dirty = true;
    }

    void SetOpen(bool value)
    {
        open = value;
        panel.gameObject.SetActive(value);
        if (!value)
        {
            HideTooltip();
            filterPopup.gameObject.SetActive(false);
            if (searchFocused) { searchInput.DeactivateInputField(); ChatInputGate.IsOpen = false; searchFocused = false; }
        }
        else dirty = true;
    }

    /// <summary>[작게 보기]·Esc — 서랍으로 돌아가며 검색어를 돌려준다.</summary>
    void GoBack()
    {
        string q = query;
        SetOpen(false);
        onBack?.Invoke(q);
    }

    void OnCellClicked(Cell cell)
    {
        if (cell.recipe == null) { PlayerNotification.Show(LocalPlayer.LocalPlayerId, $"{cell.unit.DisplayName}: 조합식이 없는 유닛입니다(위습·뽑기로 얻는다).", 4f); return; }
        if (!RecipeLocator.Locate(cell.recipe)) PlayerNotification.Show(LocalPlayer.LocalPlayerId, "조합판에서 그 식의 인형을 찾지 못했습니다.", 4f);
    }

    void Update()
    {
        if (!fontApplied && GameHud.UiFontAsset != null)
        {
            foreach (TMP_Text t in texts) if (t != null) t.font = GameHud.UiFontAsset;
            fontApplied = true;
        }
        if (!open) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (filterPopup.gameObject.activeSelf) filterPopup.gameObject.SetActive(false);
            else GoBack();
            return;
        }
        if (tooltip != null && tooltip.activeSelf && Mouse.current != null)
        {
            // 툴팁은 포인터 오른쪽 아래, 화면 밖으로 안 나가게(캔버스 기준 좌표로 변환)
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransform rt = (RectTransform)tooltip.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, mouse, null, out Vector2 local);
            Vector2 size = ((RectTransform)transform).rect.size;
            float x = Mathf.Clamp(local.x + size.x * 0.5f + 20f, 0f, size.x - rt.sizeDelta.x);
            float y = Mathf.Clamp(local.y - size.y * 0.5f - 20f, -size.y + rt.sizeDelta.y, 0f);
            rt.anchoredPosition = new Vector2(x, y);
        }
        if (dirty || Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            dirty = false;
            Refresh();
        }
    }

    void CountOwned()
    {
        ownedUnits.Clear(); ownedItems.Clear();
        PlayerContext me = PlayerContext.Local;
        if (me == null) return;
        if (me.UnitInventory != null)
            foreach (UnitData u in me.UnitInventory.Units) if (u != null) ownedUnits[u] = (ownedUnits.TryGetValue(u, out int n) ? n : 0) + 1;
        if (me.ItemInventory != null)
            foreach (ItemData it in me.ItemInventory.Items) if (it != null) ownedItems[it] = (ownedItems.TryGetValue(it, out int n) ? n : 0) + 1;
    }

    void Refresh()
    {
        CountOwned();
        foreach (var pair in chipImages) pair.Value.color = (filter & pair.Key) != 0 ? BtnOn : BtnOff;
        for (int i = 0; i < sortButtons.Length; i++) sortButtons[i].color = (int)sort == i ? BtnOn : BtnOff;
        hideToggleImage.color = hideNonMatching ? BtnOn : BtnOff;
        int filterCount = SkillTags.All.Count(e => (filter & e.tag) != 0);
        filterButtonLabel.text = filterCount > 0 ? $"능력 필터 ({filterCount})" : "능력 필터";
        bool anyFilter = filter != SkillTag.None || normalizedQuery.Length > 0;

        foreach (Column column in columns)
        {
            if (column.def.tab != tab) continue;
            foreach (Cell cell in column.cells)
            {
                cell.pct = progress.Compute(cell.unit, ownedUnits, ownedItems);
                cell.matches = (cell.skillTags & filter) == filter && (normalizedQuery.Length == 0 || HangulSearch.UnitMatches(normalizedQuery, cell.unit));
                bool have = ownedUnits.TryGetValue(cell.unit, out int count) && count > 0;
                cell.root.SetActive(cell.matches || !hideNonMatching);
                // 위계: 맞는 칸은 또렷하게, 0%는 흐리게, 필터에 안 맞는 칸은 더 흐리게
                float a = !cell.matches ? 0.25f : (cell.pct <= 0.001f && !have ? 0.62f : 1f);
                cell.bg.color = new Color(RowFill.r, RowFill.g, RowFill.b, cell.matches ? RowFill.a : 0.4f);
                cell.name.alpha = a; cell.tags.alpha = a; cell.percent.alpha = a;
                cell.portrait.sprite = UnitThumbBaker.Get(cell.unit);
                Color g = cell.unit.grade.Color();
                cell.portrait.color = cell.portrait.sprite != null ? new Color(1f, 1f, 1f, a) : new Color(g.r * 0.45f, g.g * 0.45f, g.b * 0.45f, a);
                RectTransform fill = (RectTransform)cell.barFill.transform;
                fill.anchorMax = new Vector2(Mathf.Clamp01(cell.pct), 1f);
                bool full = have || cell.pct >= 0.999f;
                cell.barFill.color = full ? new Color(0.95f, 0.78f, 0.25f, a) : new Color(0.25f, 0.8f, 0.3f, a);
                cell.percent.text = have ? "<color=#F2C744>● 보유</color>" : $"{Mathf.RoundToInt(cell.pct * 100f)}%";
                cell.outline.transform.parent.gameObject.SetActive(have && cell.matches);
            }
            // 정렬은 열 안에서만
            IEnumerable<Cell> ordered = column.cells;
            if (sort == SortMode.High) ordered = column.cells.OrderByDescending(c => c.pct).ThenBy(c => c.defaultOrder);
            else if (sort == SortMode.Low) ordered = column.cells.OrderBy(c => c.pct).ThenBy(c => c.defaultOrder);
            int row = 0;
            foreach (Cell cell in ordered)
            {
                if (!cell.root.activeSelf) continue;
                ((RectTransform)cell.root.transform).anchoredPosition = new Vector2(0f, -(HeaderH + row * (RowH + RowGap)));
                row++;
            }
        }
        totalsText.text = BuildTotals();
    }

    // 내 능력치 합계(참고용 — 설계표 §3). 개수 중심, 방깎은 같은 스킬 이름은 최댓값 하나만(합 상한 −75), 이감은 가장 센 하나.
    string BuildTotals()
    {
        int owned = ownedUnits.Values.Sum();
        var counts = new Dictionary<SkillTag, int>();
        var armorByName = new Dictionary<string, float>();
        float slow = 0f;
        foreach (var pair in ownedUnits)
        {
            UnitData u = pair.Key;
            SkillTag tags = SkillTags.Of(u);
            foreach (var entry in SkillTags.All) if ((tags & entry.tag) != 0) counts[entry.tag] = (counts.TryGetValue(entry.tag, out int n) ? n : 0) + pair.Value;
            for (int i = 0; i < u.SkillCount; i++)
            {
                SkillData s = u.SkillAt(i);
                if (s == null || s.levels == null || s.levels.Count == 0 || s.levels[0].effects == null) continue;
                foreach (SkillEffect e in s.levels[0].effects)
                {
                    if (e == null) continue;
                    if (e.kind == SkillEffectKind.ArmorBreak || (e.kind == SkillEffectKind.ArmorBonus && e.multiplier < 0f))
                    {
                        float amount = Mathf.Abs(e.multiplier);
                        string key = s.skillName ?? s.name;
                        if (!armorByName.TryGetValue(key, out float cur) || amount > cur) armorByName[key] = amount;
                    }
                    else if (e.kind == SkillEffectKind.Slow && e.multiplier > 0f && e.multiplier < 1f) slow = Mathf.Max(slow, 1f - e.multiplier);
                }
            }
        }
        float armorSum = armorByName.Values.Sum();
        var sb = new StringBuilder($"<color=#F2C744>내 유닛 {owned}기 · 방깎 −{Mathf.Min(75f, armorSum):F0}{(armorSum > 75f ? "(상한)" : "")} · 이감 {slow * 100f:F0}%</color>");
        var top = SkillTags.All.Where(e => counts.ContainsKey(e.tag)).Select(e => $"{e.label} {counts[e.tag]}").Take(7);
        if (counts.Count > 0) sb.Append("   ").Append(string.Join(" · ", top));
        return sb.ToString();
    }

    void ShowTooltip(Cell cell)
    {
        var sb = new StringBuilder();
        string gradeHex = ColorUtility.ToHtmlStringRGB(cell.unit.grade.Color());
        sb.AppendLine($"<color=#{gradeHex}><b>{cell.unit.DisplayName}</b></color> ({cell.unit.grade.KoreanName()}) — {Mathf.RoundToInt(cell.pct * 100f)}%");
        if (cell.recipe != null && cell.recipe.ingredients != null)
        {
            var parts = new List<string>();
            foreach (RecipeIngredient ing in cell.recipe.ingredients)
            {
                if (ing == null) continue;
                string n;
                bool have;
                if (ing.kind == IngredientKind.SpecificUnit && ing.unit != null)
                {
                    have = (ownedUnits.TryGetValue(ing.unit, out int a) && a >= Mathf.Max(1, ing.count)) || (ing.alternativeUnit != null && ownedUnits.TryGetValue(ing.alternativeUnit, out int b) && b >= Mathf.Max(1, ing.count));
                    n = ing.alternativeUnit != null ? $"({ing.unit.DisplayName} 또는 {ing.alternativeUnit.DisplayName})" : ing.unit.DisplayName;
                }
                else if (ing.kind == IngredientKind.SpecificItem && ing.item != null) { have = ownedItems.TryGetValue(ing.item, out int c) && c >= 1; n = ing.item.itemName; }
                else { have = ownedUnits.Any(p => p.Key.grade == ing.wildcardGrade && p.Value > 0); n = ing.wildcardGrade.KoreanName() + " 아무거나"; }
                parts.Add($"<color={(have ? "#6FD36F" : "#8A8A90")}>{(have ? "✔" : "✘")} {n}{(ing.count > 1 ? " ×" + ing.count : "")}</color>");
            }
            sb.AppendLine("직접 재료: " + string.Join("  ", parts));
            var costs = new List<string>();
            if (cell.recipe.goldCost > 0) costs.Add($"엔 {cell.recipe.goldCost:N0}");
            if (cell.recipe.resourceCosts != null) foreach (RecipeResourceCost c in cell.recipe.resourceCosts) if (c != null && c.amount > 0) costs.Add($"{c.type} {c.amount}");
            if (costs.Count > 0) sb.AppendLine("비용: " + string.Join(" · ", costs));
            if (cell.recipe.maxRound > 0) sb.AppendLine($"<color=#FFB060>{cell.recipe.maxRound}라운드 이전만 가능</color>");
            if (cell.recipe.requiredSaveCount > 0) sb.AppendLine($"<color=#FFB060>클리어 {cell.recipe.requiredSaveCount}회 필요</color>");
        }
        else sb.AppendLine("조합식 없음(위습·뽑기로 얻는다)");
        string tags = string.Join(" · ", SkillTags.Labels(cell.skillTags));
        sb.Append(tags.Length > 0 ? "능력: " + tags : "<color=#8A8A90>능력 꼬리표 없음</color>");
        tooltipText.text = sb.ToString();
        tooltip.SetActive(true);
        tooltip.transform.SetAsLastSibling();
    }

    void HideTooltip() { if (tooltip != null) tooltip.SetActive(false); }
}
