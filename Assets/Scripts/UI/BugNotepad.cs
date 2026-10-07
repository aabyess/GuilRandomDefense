using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 버그 기록지(사장님 10-07 — 친구 테스터가 적는다). 게임 화면 왼쪽 위 헤더 바로 밑 단추 「버그 기록」 → 메모장 창.
/// · 저장: 입력할 때마다(0.5초 디바운스) + 창 닫을 때 + 게임 종료·일시정지·씬 전환 때 — 임시 파일에 쓰고 바꿔치기(원자적)라 강제 종료해도 마지막 0.5초만 잃는다.
/// · 위치: 구랜디 폴더 안 「버그기록.txt」(윈도우 = exe 옆, 맥 = .app 옆 — 앱 번들 안 금지). 못 쓰면(맥 격리 앱 등) 로그 폴더(GameLog.Folder)로 대체하고 창에 실제 위치를 한 줄 보인다.
///       에디터에선 프로젝트 폴더를 더럽히지 않게 늘 로그 폴더.
/// · 다음 실행 때 같은 파일을 불러와 이어 쓴다. 멀티에서도 각자 PC에(네트워크 안 탄다).
/// · 입력 중엔 게임 단축키가 안 먹는다(ChatInputGate — 채팅·조합 검색과 같은 처리).
/// 스스로 붙는다(GameVersion과 같은 방식) — GameHud는 건드리지 않는다. 게임 화면(GameHud가 있는 씬)에서만 단추가 보인다.
/// </summary>
public class BugNotepad : MonoBehaviour
{
    public const string FileName = "버그기록.txt";
    const float SaveDelay = 0.5f;

    static BugNotepad instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("BugNotepad");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<BugNotepad>();
    }

    // ───────── 상태 ─────────
    string savePath, saveFolder;
    bool savePrimary;
    bool dirty;
    float saveAt;
    string lastSavedStamp = "";

    GameObject buttonRoot, windowRoot;
    TMP_InputField input;
    TMP_Text pathText, statusText;
    bool inputFocused;
    float nextHudCheck;
    bool fontApplied;
    readonly System.Collections.Generic.List<TMP_Text> texts = new System.Collections.Generic.List<TMP_Text>();

    // ───────── 저장 위치 ─────────
    static string ResolveFolder(out bool primary)
    {
        primary = false;
#if !UNITY_EDITOR
        try
        {
            string data = Application.dataPath;
#if UNITY_STANDALONE_OSX
            // dataPath = X.app/Contents → .app 바로 옆 폴더(구랜디 폴더).
            string folder = Directory.GetParent(Directory.GetParent(data).FullName).FullName;
#else
            string folder = Directory.GetParent(data).FullName;   // GuRandi_Data → exe가 있는 폴더
#endif
            string probe = Path.Combine(folder, ".bugnote_probe");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            primary = true;
            return folder;
        }
        catch (Exception e) { Debug.Log($"[버그기록] 게임 폴더에 못 써서 로그 폴더로 대체: {e.Message}"); }
#endif
        try { Directory.CreateDirectory(GameLog.Folder); } catch { }
        return GameLog.Folder;
    }

    void ResolvePath()
    {
        saveFolder = ResolveFolder(out savePrimary);
        savePath = Path.Combine(saveFolder, FileName);
    }

    // 임시 파일에 쓰고 바꿔치기 — 쓰다 죽어도 옛 파일이 온전하다.
    bool SaveNow()
    {
        if (input == null || string.IsNullOrEmpty(savePath)) return false;
        string text = input.text ?? "";
        try
        {
            string tmp = savePath + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(true));
            if (File.Exists(savePath)) File.Replace(tmp, savePath, null);
            else File.Move(tmp, savePath);
            dirty = false;
            lastSavedStamp = DateTime.Now.ToString("HH:mm:ss");
            return true;
        }
        catch (Exception e)
        {
            if (savePrimary)
            {
                // 게임 폴더에 못 써지면 로그 폴더로 옮겨 다시 시도한다.
                Debug.Log($"[버그기록] 저장 실패({e.Message}) — 로그 폴더로 옮김");
                saveFolder = GameLog.Folder; savePrimary = false;
                try { Directory.CreateDirectory(saveFolder); } catch { }
                savePath = Path.Combine(saveFolder, FileName);
                RefreshPathText();
                return SaveNow();
            }
            Debug.LogWarning($"[버그기록] 저장 실패: {e.Message}");
            return false;
        }
    }

    void LoadExisting()
    {
        try
        {
            if (File.Exists(savePath)) { input.SetTextWithoutNotify(File.ReadAllText(savePath, Encoding.UTF8)); Debug.Log($"[버그기록] 이전 기록 {input.text.Length}자를 불러왔습니다: {savePath}"); }
            else if (savePrimary)
            {
                // 예전에 로그 폴더로 대체 저장한 파일이 있으면 이어 받는다.
                string old = Path.Combine(GameLog.Folder, FileName);
                if (File.Exists(old)) input.SetTextWithoutNotify(File.ReadAllText(old, Encoding.UTF8));
            }
        }
        catch (Exception e) { Debug.Log($"[버그기록] 불러오기 실패: {e.Message}"); }
    }

    // ───────── 생명주기 ─────────
    void Awake()
    {
        ResolvePath();
        Build();
        LoadExisting();
        RefreshPathText();
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        windowRoot.SetActive(false);
        buttonRoot.SetActive(false);
        string[] args = Environment.GetCommandLineArgs();
        int test = Array.IndexOf(args, "-bugnoteTest");
        if (test >= 0 && test + 1 < args.Length) StartCoroutine(TestRoutine(args[test + 1]));
    }

    // 시험 전용(-bugnoteTest 글): 게임 화면이 뜨면 창을 열고 글을 넣는다 — 바로 kill -9 해서 파일이 남는지 확인하는 용도.
    System.Collections.IEnumerator TestRoutine(string text)
    {
        while (FindFirstObjectByType<GameHud>() == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);
        OpenWindow();
        input.text = text.Replace("\\n", "\n");
        Debug.Log($"[버그기록] 시험 글 입력: {input.text.Length}자 · 저장 위치 {savePath}");
    }

    void OnDestroy() { SceneManager.sceneUnloaded -= OnSceneUnloaded; if (inputFocused) ChatInputGate.IsOpen = false; if (dirty) SaveNow(); }
    void OnApplicationQuit() { if (dirty) SaveNow(); }
    void OnApplicationPause(bool paused) { if (paused && dirty) SaveNow(); }
    void OnSceneUnloaded(Scene scene) { if (dirty) SaveNow(); }

    void Update()
    {
        if (!fontApplied && GameHud.UiFontAsset != null)
        {
            foreach (TMP_Text t in texts) if (t != null) t.font = GameHud.UiFontAsset;
            fontApplied = true;
        }

        if (Time.unscaledTime >= nextHudCheck)
        {
            nextHudCheck = Time.unscaledTime + 0.5f;
            bool inGame = FindFirstObjectByType<GameHud>() != null;
            if (buttonRoot.activeSelf != inGame) buttonRoot.SetActive(inGame);
            if (!inGame && windowRoot.activeSelf) CloseWindow();
        }

        if (dirty && Time.unscaledTime >= saveAt) { SaveNow(); RefreshStatus(); }
    }

    // ───────── 창 ─────────
    void OpenWindow()
    {
        windowRoot.SetActive(true);
        input.ActivateInputField();
        input.MoveTextEnd(false);
        RefreshPathText();
        RefreshStatus();
    }

    void CloseWindow()
    {
        if (dirty) SaveNow();
        if (inputFocused) { input.DeactivateInputField(); }
        inputFocused = false;
        ChatInputGate.IsOpen = false;
        windowRoot.SetActive(false);
    }

    void ToggleWindow() { if (windowRoot.activeSelf) CloseWindow(); else OpenWindow(); }

    void OnValueChanged(string _) { dirty = true; saveAt = Time.unscaledTime + SaveDelay; }

    void InsertHeader()
    {
        string nick = "";
        try { nick = PlayerDisplayName.RawNickname(LocalPlayer.LocalPlayerId); } catch { }
        if (string.IsNullOrEmpty(nick)) nick = "닉네임 없음";
        var difficulty = FindFirstObjectByType<DifficultyManager>();
        string diff = difficulty != null ? difficulty.Current.KoreanName() : "-";
        var round = FindFirstObjectByType<RoundManager>();
        string header = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] 버전 {GameVersion.Label} · {nick} · 난이도 {diff} · 라운드 {(round != null ? round.CurrentRound : 0)}\n";
        string text = input.text ?? "";
        int caret = Mathf.Clamp(input.caretPosition, 0, text.Length);
        int lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        string insert = (lineStart > 0 && lineStart == text.Length ? "\n" : "") + header;
        input.text = text.Insert(lineStart, insert);   // onValueChanged가 저장 예약
        input.caretPosition = lineStart + insert.Length;
        input.ActivateInputField();
    }

    void CopyAll()
    {
        GUIUtility.systemCopyBuffer = input.text ?? "";
        statusText.text = "복사했습니다 — 카톡에 붙여넣기";
    }

    void OpenFolder()
    {
        try
        {
            if (dirty) SaveNow();
            Application.OpenURL(new Uri(saveFolder + Path.DirectorySeparatorChar).AbsoluteUri);
        }
        catch (Exception e) { statusText.text = "폴더를 못 열었습니다: " + e.Message; }
    }

    void RefreshPathText()
    {
        if (pathText == null) return;
        pathText.text = (savePrimary ? "저장 위치: " : Application.isEditor ? "저장 위치(에디터는 로그 폴더): " : "저장 위치(게임 폴더에 못 써서 대체): ") + savePath;
    }

    void RefreshStatus()
    {
        if (statusText == null) return;
        statusText.text = string.IsNullOrEmpty(lastSavedStamp) ? "" : $"저장됨 {lastSavedStamp}";
    }

    // ───────── UI 만들기 ─────────

    // 워크3풍 창 그림(Resources/UI/SkinWc3) — UiSkin과 같은 경로·같은 규칙이지만 UiSkin에 기대지 않는다(이 창은 혼자 선다).
    static readonly System.Collections.Generic.Dictionary<string, Sprite> wc3Cache = new System.Collections.Generic.Dictionary<string, Sprite>();
    static Sprite Wc3(string name)
    {
        if (wc3Cache.TryGetValue(name, out Sprite cached)) return cached;
        Sprite sprite = Resources.Load<Sprite>("UI/SkinWc3/" + name);
        wc3Cache[name] = sprite;
        return sprite;
    }
    static bool Wc3Has(string name) => Wc3(name) != null;
    static bool ApplyWc3(Image image, string name, float ppum)
    {
        Sprite sprite = Wc3(name);
        if (sprite == null) return false;
        image.sprite = sprite;
        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = ppum;
        image.color = Color.white;
        return true;
    }
    static readonly Color Gold = new Color(0.95f, 0.85f, 0.55f, 1f);

    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 왼쪽 위 기준 좌표(y는 아래로 +).
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform r, float l, float t, float rr, float b)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
    }

    TMP_Text MakeText(Transform parent, string s, float size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        RectTransform r = NewRect("Text", parent);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        if (GameHud.UiFontAsset != null) t.font = GameHud.UiFontAsset;
        texts.Add(t);
        return t;
    }

    Button MakeButton(Transform parent, string label, float x, float y, float w, float h, Action onClick, float size = 18f)
    {
        RectTransform r = NewRect("Btn_" + label, parent);
        Place(r, x, y, w, h);
        Image bg = r.gameObject.AddComponent<Image>();
        bool wc3 = Wc3Has("win_btn");
        bg.color = new Color(0.16f, 0.16f, 0.19f, 1f);
        if (wc3) ApplyWc3(bg, "win_btn", 2f);
        var btn = r.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        if (wc3)
        {
            btn.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState { highlightedSprite = Wc3("win_btn_hover"), pressedSprite = Wc3("win_btn_pressed"), disabledSprite = Wc3("win_btn_disabled") };
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        btn.onClick.AddListener(() => onClick());
        TMP_Text t = MakeText(r, label, size, new Color(0.95f, 0.92f, 0.82f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform, 2f, 0f, 2f, 0f);
        return btn;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // 단추 — 왼쪽 위 헤더(퀘스트·메뉴·동맹·대화 줄) 바로 밑
        RectTransform root = (RectTransform)transform;
        Button open = MakeButton(root, "버그 기록", 6f, 40f, 124f, 32f, ToggleWindow, 18f);
        buttonRoot = open.gameObject;

        // 창
        const float W = 900f, H = 700f;
        windowRoot = NewRect("Window", root).gameObject;
        var panel = (RectTransform)windowRoot.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(W, H);
        panel.anchoredPosition = Vector2.zero;
        Image bg = windowRoot.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.08f, 0.97f);
        bg.raycastTarget = true;   // 창 뒤 게임 클릭 막기

        MakeText(panel, "버그 기록지", 28f, Gold, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.SetPlace(0f, 34f, W, 40f);   // 창 틀(약 28px)이 가장자리를 덮는다 — 안쪽으로 들인다
        TMP_Text guide = MakeText(panel, "게임 중 이상한 점을 적어 주세요. 자동 저장됩니다 — 다 적으면 [폴더 열기]로 버그기록.txt를 보내 주세요.", 19f, new Color(0.88f, 0.9f, 0.95f, 1f), TextAlignmentOptions.Left);
        guide.rectTransform.SetPlace(46f, 80f, W - 92f, 56f);

        MakeButton(panel, "+ 새 기록", 46f, 140f, 130f, 38f, InsertHeader, 18f);
        MakeButton(panel, "전체 복사", 184f, 140f, 130f, 38f, CopyAll, 18f);
        MakeButton(panel, "폴더 열기", 322f, 140f, 130f, 38f, OpenFolder, 18f);
        statusText = MakeText(panel, "", 17f, new Color(0.7f, 0.9f, 0.7f, 1f), TextAlignmentOptions.Right);
        statusText.rectTransform.SetPlace(468f, 144f, W - 468f - 46f, 30f);

        // 입력칸(여러 줄) — 오른쪽에 스크롤바
        RectTransform box = NewRect("InputBox", panel);
        Place(box, 46f, 188f, W - 92f, H - 188f - 120f);
        Image boxBg = box.gameObject.AddComponent<Image>();
        boxBg.color = new Color(0.03f, 0.03f, 0.045f, 1f);
        if (Wc3Has("win_search_box")) ApplyWc3(boxBg, "win_search_box", 2f);

        RectTransform viewport = NewRect("Viewport", box);
        Stretch(viewport, 12f, 8f, 28f, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = MakeText(viewport, "", 22f, new Color(0.95f, 0.93f, 0.86f, 1f), TextAlignmentOptions.TopLeft);   // 채팅(24)과 비슷한 크기
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.raycastTarget = true;
        TMP_Text placeholder = MakeText(viewport, "여기에 적으세요. 어떤 상황이었는지(어느 유닛·몇 라운드·무엇을 눌렀는지)도 같이 적어 주면 좋아요.", 20f, new Color(0.5f, 0.5f, 0.55f, 1f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform sbRect = NewRect("Scrollbar", box);
        sbRect.anchorMin = new Vector2(1f, 0f); sbRect.anchorMax = new Vector2(1f, 1f); sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.sizeDelta = new Vector2(20f, -16f); sbRect.anchoredPosition = new Vector2(-4f, 0f);
        Image track = sbRect.gameObject.AddComponent<Image>();
        track.color = new Color(0.1f, 0.1f, 0.13f, 1f);
        if (Wc3Has("win_scroll_track")) ApplyWc3(track, "win_scroll_track", 2f);
        RectTransform slide = NewRect("Sliding Area", sbRect);
        Stretch(slide, 0f, 2f, 0f, 2f);
        RectTransform handle = NewRect("Handle", slide);
        Stretch(handle, 0f, 0f, 0f, 0f);
        Image handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.55f, 0.45f, 0.25f, 1f);
        if (Wc3Has("win_scroll_handle")) ApplyWc3(handleImg, "win_scroll_handle", 2f);
        var scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        box.gameObject.SetActive(false);
        input = box.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = boxBg;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.richText = false;
        input.verticalScrollbar = scrollbar;
        input.scrollSensitivity = 30f;
        input.characterLimit = 200000;
        input.onValueChanged.AddListener(OnValueChanged);
        input.onSelect.AddListener(_ => { inputFocused = true; ChatInputGate.IsOpen = true; });
        input.onDeselect.AddListener(_ => { inputFocused = false; ChatInputGate.IsOpen = false; });
        box.gameObject.SetActive(true);

        pathText = MakeText(panel, "", 15f, new Color(0.62f, 0.65f, 0.7f, 1f), TextAlignmentOptions.Left);
        pathText.rectTransform.SetPlace(46f, H - 112f, W - 92f - 120f, 44f);
        pathText.overflowMode = TextOverflowModes.Ellipsis;
        MakeButton(panel, "닫기", W - 46f - 100f, H - 108f, 100f, 38f, CloseWindow, 18f);

        // 워크3 창 틀
        if (Wc3Has("win_frame"))
        {
            RectTransform frame = NewRect("Wc3Frame", panel);
            Stretch(frame, 0f, 0f, 0f, 0f);
            Image fi = frame.gameObject.AddComponent<Image>();
            ApplyWc3(fi, "win_frame", 2f);
            fi.raycastTarget = false;
        }
    }
}

static class BugNotepadRectExt
{
    public static void SetPlace(this RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }
}
