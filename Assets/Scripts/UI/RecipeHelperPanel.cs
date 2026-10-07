using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 조합 도우미(tmo build-helper 식, 사장님 10-07) — 등급별 열에 유닛 전체를 늘어놓고 유닛마다 진행률 %(RecipeProgress)·능력 꼬리표(SkillTags) 필터·정렬·내 능력치 합계·툴팁을 보여 주는 큰 창.
/// 설계표 Docs/design/RECIPE_HELPER_DESIGN_2026-10-06.md. 실제 보유만 본다(가상 +/− 계획 모드 없음). 전부 로컬 UI — 서버·네트워크 안 감.
/// ⚠️ 켜는 스위치 <see cref="Enabled"/>가 false인 동안 서랍(F5)에 「열 보기」 단추조차 안 생긴다 — 반쯤 된 화면이 배포판에 안 들어가게(PM 10-07). 다 되면 true.
/// 진입: RecipeSearchDrawer의 「열 보기」 단추 → Toggle(). 닫기 ✕·Esc. 유닛 클릭 → 조합판의 그 식으로(RecipeLocator, 식이 있는 유닛만).
/// </summary>
public class RecipeHelperPanel : MonoBehaviour
{
    /// <summary>🔴 기능 스위치. false(기본)면 이 창은 어디서도 안 열리고 서랍에 단추도 없다. 켜는 곳: 에디터 탐침(RecipeHelperProbe)·다 되면 이 기본값을 true로.</summary>
    public static bool Enabled { get; set; } = false;

    public static RecipeHelperPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    public static void Toggle()
    {
        if (!Enabled) return;
        if (Instance == null) new GameObject("[조합 도우미]").AddComponent<RecipeHelperPanel>();
        Instance.SetOpen(!Instance.open);
    }

    // ── 모양(1920×1080 기준 px, 캔버스가 비례 확대)
    const float PanelW = 1760f, PanelH = 960f, Pad = 44f, RowH = 50f, HeaderH = 34f;
    static readonly Color PanelFill = new Color(0.05f, 0.06f, 0.10f, 0.97f);
    static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    static readonly Color RowFill = new Color(0.10f, 0.12f, 0.20f, 0.95f);
    static readonly Color Muted = new Color(0.65f, 0.64f, 0.59f, 1f);
    static readonly Color ChipOff = new Color(0.12f, 0.16f, 0.28f, 1f);
    static readonly Color ChipOn = new Color(0.55f, 0.42f, 0.12f, 1f);

    enum SortMode { Default, High, Low }

    // 열 순서(등급 → 열). 전설·히든은 딜 종류(물딜/마딜)로 갈라 두 열이 된다.
    struct ColumnDef { public string title; public UnitGrade grade; public int damage; }   // damage: 0 = 상관없음, 1 = 물딜(AD 포함), 2 = 마딜(AP만)

    class Cell
    {
        public UnitData unit;
        public GameObject root;
        public Image bg, portrait, barFill;
        public TMP_Text name, tags, percent;
        public CombineRecipe recipe;
        public float pct;
        public SkillTag skillTags;
        public int defaultOrder;
    }

    class Column
    {
        public ColumnDef def;
        public RectTransform root;
        public readonly List<Cell> cells = new List<Cell>();
        public TMP_Text header;
    }

    // ── 상태
    bool open;
    CombineSystem system;
    RecipeProgress progress;
    readonly List<UnitData> units = new List<UnitData>();
    readonly List<Column> columns = new List<Column>();
    SortMode sort = SortMode.Default;
    SkillTag filter = SkillTag.None;
    bool hideNonMatching;
    bool dirty = true;
    float nextRefresh;
    RectTransform panel, gridContent;
    TMP_Text totalsText, tooltipText, infoText;
    GameObject tooltip;
    readonly List<TMP_Text> texts = new List<TMP_Text>();
    readonly Dictionary<SkillTag, Image> chipImages = new Dictionary<SkillTag, Image>();
    Image[] sortButtons;
    Image hideToggleImage;
    readonly Dictionary<UnitData, int> ownedUnits = new Dictionary<UnitData, int>();
    readonly Dictionary<ItemData, int> ownedItems = new Dictionary<ItemData, int>();
    bool fontApplied;

    void Awake()
    {
        Instance = this;
        system = FindFirstObjectByType<CombineSystem>();
        BuildData();
        Build();
        panel.gameObject.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; UnitThumbBaker.Baked -= OnThumbBaked; }

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
        yield return new ColumnDef { title = "흔함", grade = UnitGrade.Common };
        yield return new ColumnDef { title = "안흔함", grade = UnitGrade.Uncommon };
        yield return new ColumnDef { title = "특별함", grade = UnitGrade.Special };
        yield return new ColumnDef { title = "희귀함", grade = UnitGrade.Rare };
        yield return new ColumnDef { title = "특수함", grade = UnitGrade.Superior };
        yield return new ColumnDef { title = "전설[물딜]", grade = UnitGrade.Legendary, damage = 1 };
        yield return new ColumnDef { title = "전설[마딜]", grade = UnitGrade.Legendary, damage = 2 };
        yield return new ColumnDef { title = "히든[물딜]", grade = UnitGrade.Hidden, damage = 1 };
        yield return new ColumnDef { title = "히든[마딜]", grade = UnitGrade.Hidden, damage = 2 };
        yield return new ColumnDef { title = "제한됨", grade = UnitGrade.Limited };
        yield return new ColumnDef { title = "초월", grade = UnitGrade.Transcendent };
        yield return new ColumnDef { title = "불멸", grade = UnitGrade.Immortal };
        yield return new ColumnDef { title = "영원", grade = UnitGrade.Eternal };
        yield return new ColumnDef { title = "신비함", grade = UnitGrade.OtherWorld };   // tmo 「신비함」 = 우리 다른세계 식 결과(PM 확인 §11)
        yield return new ColumnDef { title = "랜덤유닛", grade = UnitGrade.RandomUnit };
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

    Image AddButton(Transform parent, string label, float x, float y, float w, float h, System.Action onClick, out TMP_Text text, float size = 16f)
    {
        RectTransform r = NewRect("Btn", parent);
        Place(r, x, y, w, h);
        Image bg = r.gameObject.AddComponent<Image>();
        bg.color = ChipOff;
        r.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
        text = MakeText(r, label, size, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform, 0, 0, 0, 0);
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

        panel = NewRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panel.anchoredPosition = Vector2.zero;
        Image bg = panel.gameObject.AddComponent<Image>();
        if (!UiSkin.Apply(bg, "dialog_panel_9s", PanelFill)) bg.color = PanelFill;

        MakeText(panel, "조합 도우미", 26f, new Color(1f, 0.84f, 0.25f, 1f), TextAlignmentOptions.Left, FontStyles.Bold).rectTransform.SetAsLastSibling();
        Place(texts[texts.Count - 1].rectTransform, Pad, 30f, 300f, 34f);
        infoText = MakeText(panel, "", 14f, Muted, TextAlignmentOptions.Left);
        Place(infoText.rectTransform, Pad + 190f, 36f, 520f, 24f);

        // 정렬 · 숨김 · 닫기
        sortButtons = new Image[3];
        string[] sortNames = { "기본순", "퍼센트 높은순", "퍼센트 낮은순" };
        float sx = PanelW - Pad - 36f - 12f - (3 * 150f + 2 * 6f) - 170f;
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            sortButtons[i] = AddButton(panel, sortNames[i], sx + i * 156f, 28f, 150f, 34f, () => { sort = (SortMode)captured; dirty = true; RefreshSortButtons(); }, out _);
        }
        hideToggleImage = AddButton(panel, "안 맞는 유닛 숨김", sx + 3 * 156f + 6f, 28f, 164f, 34f, () => { hideNonMatching = !hideNonMatching; dirty = true; RefreshToggles(); }, out _, 15f);
        AddButton(panel, "✕", PanelW - Pad - 36f, 28f, 36f, 34f, () => SetOpen(false), out _, 20f);
        RefreshSortButtons();

        BuildChips();

        totalsText = MakeText(panel, "", 15f, new Color(0.85f, 0.9f, 1f, 1f), TextAlignmentOptions.TopLeft);
        totalsText.textWrappingMode = TextWrappingModes.Normal;
        Place(totalsText.rectTransform, Pad, 150f, PanelW - Pad * 2f, 44f);

        BuildGrid();
        BuildTooltip();
    }

    void BuildChips()
    {
        // 「그 꼬리표를 가진 유닛이 1기 이상」일 때만 칩을 만든다(데이터 근거 없는 tmo 항목은 자동으로 안 뜬다).
        var present = SkillTags.All.Where(e => units.Any(u => (SkillTags.Of(u) & e.tag) != 0)).ToList();
        float x = Pad, y = 76f, w = 124f, h = 28f, gap = 6f, maxX = PanelW - Pad;
        foreach (var entry in present)
        {
            if (x + w > maxX) { x = Pad; y += h + gap; }
            SkillTag tag = entry.tag;
            Image chip = AddButton(panel, entry.label, x, y, w, h, () => { filter ^= tag; dirty = true; RefreshChips(); }, out TMP_Text label, 14f);
            label.enableAutoSizing = true; label.fontSizeMin = 10f; label.fontSizeMax = 14f;
            chipImages[tag] = chip;
            x += w + gap;
        }
        RefreshChips();
    }

    void BuildGrid()
    {
        float top = 204f;
        RectTransform scrollRect = NewRect("Scroll", panel);
        Place(scrollRect, Pad - 4f, top, PanelW - Pad * 2f + 8f, PanelH - top - Pad);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scrollRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        RectTransform viewport = NewRect("Viewport", scrollRect);
        Stretch(viewport, 0f, 0f, 0f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        gridContent = NewRect("Content", viewport);
        gridContent.anchorMin = new Vector2(0f, 1f); gridContent.anchorMax = new Vector2(1f, 1f); gridContent.pivot = new Vector2(0.5f, 1f);
        scroll.viewport = viewport; scroll.content = gridContent;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40f;

        var defs = ColumnDefs().Select(d => (def: d, members: units.Where(u => InColumn(u, d)).ToList())).Where(x => x.members.Count > 0).ToList();
        float usable = PanelW - Pad * 2f;
        float colW = Mathf.Min(150f, usable / Mathf.Max(1, defs.Count));
        int maxRows = defs.Max(x => x.members.Count);
        gridContent.sizeDelta = new Vector2(0f, HeaderH + maxRows * (RowH + 3f) + 12f);
        for (int ci = 0; ci < defs.Count; ci++)
        {
            var column = new Column { def = defs[ci].def };
            column.root = NewRect("Col" + ci, gridContent);
            Place(column.root, ci * colW, 0f, colW - 4f, HeaderH + defs[ci].members.Count * (RowH + 3f));
            column.header = MakeText(column.root, $"{defs[ci].def.title} <size=75%><color=#9a9aa2>{defs[ci].members.Count}</color></size>", 16f, defs[ci].def.grade.Color(), TextAlignmentOptions.Center, FontStyles.Bold);
            Place(column.header.rectTransform, 0f, 0f, colW - 4f, HeaderH - 4f);
            for (int ri = 0; ri < defs[ci].members.Count; ri++)
                column.cells.Add(BuildCell(column, defs[ci].members[ri], ri, colW - 4f));
            columns.Add(column);
        }
    }

    Cell BuildCell(Column column, UnitData unit, int order, float width)
    {
        var cell = new Cell { unit = unit, defaultOrder = order, skillTags = SkillTags.Of(unit) };
        if (system != null && system.Recipes != null)
            cell.recipe = system.Recipes.Where(r => r != null && r.result == unit).OrderByDescending(r => r.ingredients != null ? r.ingredients.Count : 0).FirstOrDefault();
        RectTransform root = NewRect("Cell", column.root);
        cell.root = root.gameObject;
        cell.bg = root.gameObject.AddComponent<Image>();
        cell.bg.color = RowFill;
        Place(root, 0f, HeaderH + order * (RowH + 3f), width, RowH);
        UnitData captured = unit;
        root.gameObject.AddComponent<Button>().onClick.AddListener(() => OnCellClicked(cell));
        var trigger = root.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowTooltip(cell));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => HideTooltip());

        RectTransform pf = NewRect("Portrait", root);
        Place(pf, 3f, 3f, 34f, 34f);
        cell.portrait = pf.gameObject.AddComponent<Image>();
        cell.portrait.raycastTarget = false;
        Color g = unit.grade.Color();
        cell.portrait.color = new Color(g.r * 0.45f, g.g * 0.45f, g.b * 0.45f, 1f);

        cell.name = MakeText(root, unit.DisplayName, 13f, g, TextAlignmentOptions.Left);
        Place(cell.name.rectTransform, 40f, 1f, width - 42f, 20f);
        cell.name.overflowMode = TextOverflowModes.Ellipsis;
        cell.tags = MakeText(root, "", 10f, new Color(0.7f, 0.75f, 0.85f, 1f), TextAlignmentOptions.Left);
        Place(cell.tags.rectTransform, 40f, 19f, width - 42f, 14f);
        cell.tags.overflowMode = TextOverflowModes.Ellipsis;
        string shortTags = SkillTags.ShortList(cell.skillTags, 2);
        cell.tags.text = shortTags.Length > 0 ? $"({shortTags})" : "";

        // 진행률 막대: 칸 전체가 검정 바탕, 채움은 초록(100%는 금색)
        RectTransform barBg = NewRect("BarBg", root);
        Place(barBg, 3f, 39f, width - 6f, 9f);
        barBg.gameObject.AddComponent<Image>().color = Color.black;
        RectTransform fill = NewRect("Fill", barBg);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        cell.barFill = fill.gameObject.AddComponent<Image>();
        cell.barFill.color = new Color(0.25f, 0.8f, 0.3f, 1f);
        cell.percent = MakeText(root, "", 11f, Color.white, TextAlignmentOptions.Right);
        Place(cell.percent.rectTransform, width - 52f, 20f, 48f, 16f);
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
        t.sizeDelta = new Vector2(420f, 150f);
        Image bg = t.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.08f, 0.97f); bg.raycastTarget = false;
        tooltipText = MakeText(t, "", 15f, Color.white, TextAlignmentOptions.TopLeft);
        tooltipText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(tooltipText.rectTransform, 10f, 8f, 10f, 8f);
        tooltip = t.gameObject;
        tooltip.SetActive(false);
        tooltip.GetComponent<RectTransform>().SetAsLastSibling();
    }

    // ───────────────────────── 동작 ─────────────────────────

    void SetOpen(bool value)
    {
        open = value;
        panel.gameObject.SetActive(value);
        if (!value) HideTooltip();
        else dirty = true;
        ChatInputGate.IsOpen = false;
    }

    void RefreshSortButtons()
    {
        for (int i = 0; i < sortButtons.Length; i++) sortButtons[i].color = (int)sort == i ? ChipOn : ChipOff;
    }

    void RefreshToggles() { if (hideToggleImage != null) hideToggleImage.color = hideNonMatching ? ChipOn : ChipOff; }

    void RefreshChips()
    {
        foreach (var pair in chipImages) pair.Value.color = (filter & pair.Key) != 0 ? ChipOn : ChipOff;
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
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
        if (tooltip != null && tooltip.activeSelf && Mouse.current != null)
        {
            // 툴팁은 포인터 오른쪽 아래, 화면 밖으로 안 나가게(캔버스 기준 좌표로 변환)
            Vector2 mouse = Mouse.current.position.ReadValue();
            RectTransform rt = (RectTransform)tooltip.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, mouse, null, out Vector2 local);
            Vector2 size = ((RectTransform)transform).rect.size;
            float x = Mathf.Clamp(local.x + size.x * 0.5f + 18f, 0f, size.x - rt.sizeDelta.x);
            float y = Mathf.Clamp(local.y - size.y * 0.5f - 18f, -size.y + rt.sizeDelta.y, 0f);
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
        int owned = ownedUnits.Values.Sum();
        foreach (Column column in columns)
        {
            foreach (Cell cell in column.cells)
            {
                cell.pct = progress.Compute(cell.unit, ownedUnits, ownedItems);
                bool matches = (cell.skillTags & filter) == filter;
                cell.root.SetActive(matches || !hideNonMatching);
                Color tint = matches ? Color.white : new Color(1f, 1f, 1f, 0.28f);
                cell.bg.color = new Color(RowFill.r, RowFill.g, RowFill.b, matches ? RowFill.a : 0.35f);
                cell.name.alpha = tint.a; cell.tags.alpha = tint.a; cell.percent.alpha = tint.a;
                cell.portrait.sprite = UnitThumbBaker.Get(cell.unit);
                Color g = cell.unit.grade.Color();
                cell.portrait.color = cell.portrait.sprite != null ? tint : new Color(g.r * 0.45f, g.g * 0.45f, g.b * 0.45f, tint.a);
                RectTransform fill = (RectTransform)cell.barFill.transform;
                fill.anchorMax = new Vector2(Mathf.Clamp01(cell.pct), 1f);
                bool have = ownedUnits.TryGetValue(cell.unit, out int count) && count > 0;
                cell.barFill.color = have || cell.pct >= 0.999f ? new Color(0.95f, 0.78f, 0.25f, tint.a) : new Color(0.25f, 0.8f, 0.3f, tint.a);
                cell.percent.text = have ? "● 보유" : $"{Mathf.RoundToInt(cell.pct * 100f)}%";
            }
            // 정렬은 열 안에서만
            IEnumerable<Cell> ordered = column.cells;
            if (sort == SortMode.High) ordered = column.cells.OrderByDescending(c => c.pct).ThenBy(c => c.defaultOrder);
            else if (sort == SortMode.Low) ordered = column.cells.OrderBy(c => c.pct).ThenBy(c => c.defaultOrder);
            int row = 0;
            foreach (Cell cell in ordered)
            {
                if (!cell.root.activeSelf) continue;
                RectTransform rt = (RectTransform)cell.root.transform;
                rt.anchoredPosition = new Vector2(0f, -(HeaderH + row * (RowH + 3f)));
                row++;
            }
        }
        infoText.text = $"유닛 {units.Count}종 · 내 유닛 {owned}기 · 진행률은 유닛마다 따로 잰다(내 보유를 나눠 쓰지 않음)";
        totalsText.text = BuildTotals();
    }

    // 내 능력치 합계(참고용 — 설계표 §3). 개수 중심, 방깎은 같은 스킬 이름은 최댓값 하나만(합 상한 −75), 이감은 가장 센 하나.
    string BuildTotals()
    {
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
        var sb = new StringBuilder("<color=#FFD54F>내 능력치(참고용 합)</color>  ");
        float armor = Mathf.Min(75f, armorByName.Values.Sum());
        sb.Append($"방깎 −{armor:F0}{(armorByName.Values.Sum() > 75f ? "(상한)" : "")} · 이감 {slow * 100f:F0}% · ");
        sb.Append(string.Join(" · ", SkillTags.All.Where(e => counts.ContainsKey(e.tag)).Select(e => $"{e.label} {counts[e.tag]}")));
        if (counts.Count == 0) sb.Append("보유 유닛 없음");
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
    }

    void HideTooltip() { if (tooltip != null) tooltip.SetActive(false); }
}
