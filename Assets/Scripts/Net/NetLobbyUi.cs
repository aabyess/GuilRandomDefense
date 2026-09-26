using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 멀티 첫 화면과 대기실(uGUI, 코드로 생성 — GameHud와 같은 방식). 친구들이 처음 보는 화면이라 OnGUI를 안 쓴다.
/// 로직은 전부 NetLauncher에 있고 여기는 그리기와 버튼 연결만 한다. 매 프레임 NetPlayer·NetGameState를 읽어 갱신한다.
/// 게임 씬에서는 숨는다(게임 HUD와 겹친다).
/// </summary>
public class NetLobbyUi : MonoBehaviour
{
    static readonly DifficultyMode[] DifficultyOrder =
    {
        DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard,
        DifficultyMode.Hell, DifficultyMode.God, DifficultyMode.Nightmare,
    };

    static readonly Color Backdrop = new Color(0.07f, 0.09f, 0.14f, 1f);
    static readonly Color Card = new Color(0.13f, 0.16f, 0.23f, 0.97f);
    static readonly Color Row = new Color(0.18f, 0.22f, 0.31f, 1f);
    static readonly Color ButtonNormal = new Color(0.26f, 0.32f, 0.44f, 1f);
    static readonly Color ButtonAccent = new Color(0.20f, 0.52f, 0.86f, 1f);
    static readonly Color ButtonDanger = new Color(0.55f, 0.22f, 0.24f, 1f);
    static readonly Color Selected = new Color(0.95f, 0.72f, 0.20f, 1f);
    static readonly Color TextMain = new Color(0.94f, 0.95f, 0.97f, 1f);
    static readonly Color TextDim = new Color(0.62f, 0.67f, 0.76f, 1f);
    static readonly Color ReadyGreen = new Color(0.42f, 0.85f, 0.50f, 1f);

    NetLauncher launcher;
    Canvas canvas;
    TMP_FontAsset font;
    TMP_FontAsset boldFont;

    GameObject modePanel;   // 첫 화면: 혼자 하기 / 같이 하기
    GameObject mainPanel;   // 같이 하기: 닉네임 · 방 만들기 · 코드로 참가
    GameObject roomPanel;   // 대기실
    TMP_Text subtitle;
    bool multiplayerChosen;
    string lastSeenStatus = "";

    TMP_InputField nicknameInput;
    TMP_InputField codeInput;
    Button createButton;
    Button joinButton;
    TMP_Text mainStatus;
    Button rejoinButton;


    TMP_Text roomCodeText;
    readonly TMP_Text[] slotNumbers = new TMP_Text[NetSession.MaxSlots];
    readonly TMP_Text[] slotNames = new TMP_Text[NetSession.MaxSlots];
    readonly TMP_Text[] slotTags = new TMP_Text[NetSession.MaxSlots];
    readonly Image[] slotRows = new Image[NetSession.MaxSlots];
    readonly Button[] difficultyButtons = new Button[6];
    readonly Image[] difficultyImages = new Image[6];
    TMP_Text difficultyHint;
    Button readyButton;
    TMP_Text readyLabel;
    Button startButton;
    TMP_Text startHint;
    TMP_Text roomStatus;
    TMP_InputField chatInput;

    void Awake()
    {
        launcher = GetComponent<NetLauncher>();
        font = GameHud.UiFontAsset;
        boldFont = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Bold SDF") ?? font;
        Build();
    }

    void Update()
    {
        bool inGame = launcher != null && SceneManager.GetActiveScene().buildIndex == launcher.GameSceneBuildIndex;
        if (canvas.enabled == inGame) canvas.enabled = !inGame;   // 게임 중 [나가기]는 GameHud 「메뉴」가 맡는다
        if (inGame) return;

        EnsureEventSystem();

        bool inRoom = launcher.InRoom;
        // 연결이 끊겨 돌아왔거나 방에 있으면 「같이 하기」 쪽 화면이다(모드 화면으로 되돌리지 않는다 — 끊긴 이유를 봐야 한다).
        // 상태 문구가 새로 바뀐 순간에만 따진다 — 계속 따지면 「뒤로」를 눌러도 남아 있는 문구 때문에 되돌아온다.
        if (launcher.Status != lastSeenStatus)
        {
            lastSeenStatus = launcher.Status;
            if (!string.IsNullOrEmpty(lastSeenStatus)) multiplayerChosen = true;
        }
        if (inRoom) multiplayerChosen = true;
        bool showMode = !multiplayerChosen;
        bool showMain = multiplayerChosen && !inRoom;
        if (modePanel.activeSelf != showMode) modePanel.SetActive(showMode);
        if (mainPanel.activeSelf != showMain) mainPanel.SetActive(showMain);
        if (roomPanel.activeSelf != inRoom) roomPanel.SetActive(inRoom);
        string wantedSubtitle = multiplayerChosen ? "같이 하기" : "";
        if (subtitle.text != wantedSubtitle) subtitle.text = wantedSubtitle;
        if (showMode) return;

        if (inRoom) RefreshRoom();
        else RefreshMain();
    }

    // 게임 씬에서 돌아오면 NetBoot에 EventSystem이 없을 수 있다(이 오브젝트는 씬을 넘어 살고, EventSystem은 씬에 산다).
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ───────────── 갱신 ─────────────

    void RefreshMain()
    {
        bool idle = !launcher.IsBusy;
        createButton.interactable = idle;
        joinButton.interactable = idle;
        nicknameInput.interactable = idle;
        codeInput.interactable = idle;
        mainStatus.text = launcher.Status;
        bool canRejoin = !string.IsNullOrEmpty(launcher.RejoinCode);
        if (rejoinButton.gameObject.activeSelf != canRejoin) rejoinButton.gameObject.SetActive(canRejoin);
        rejoinButton.interactable = idle;
    }

    void RefreshRoom()
    {
        roomCodeText.text = launcher.RoomCode;

        for (int slot = 0; slot < NetSession.MaxSlots; slot++)
        {
            NetPlayer player = NetPlayer.All.FirstOrDefault(p => p != null && p.Slot == slot);
            slotNumbers[slot].text = (slot + 1).ToString();

            if (player == null)
            {
                slotNames[slot].text = "비어 있음";
                slotNames[slot].color = TextDim;
                slotTags[slot].text = "";
                slotRows[slot].color = new Color(Row.r, Row.g, Row.b, 0.45f);
                continue;
            }

            bool me = player == NetPlayer.Local;
            slotNames[slot].text = player.DisplayName + (me ? "  (나)" : "");
            slotNames[slot].color = TextMain;
            slotRows[slot].color = me ? new Color(0.22f, 0.30f, 0.45f, 1f) : Row;

            if (player.IsHost) { slotTags[slot].text = "방장"; slotTags[slot].color = Selected; }
            else if (player.Ready) { slotTags[slot].text = "준비 완료"; slotTags[slot].color = ReadyGreen; }
            else { slotTags[slot].text = "준비 중…"; slotTags[slot].color = TextDim; }
        }

        NetGameState state = NetGameState.Instance;
        DifficultyMode? selected = state != null ? state.SelectedDifficulty : null;
        bool isHost = launcher.IsHost;
        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            bool on = selected.HasValue && selected.Value == DifficultyOrder[i];
            difficultyImages[i].color = on ? Selected : ButtonNormal;
            difficultyButtons[i].interactable = isHost;
            difficultyButtons[i].GetComponentInChildren<TMP_Text>().color = on ? Backdrop : TextMain;
        }
        difficultyHint.text = isHost ? "방장이 고릅니다" : (selected.HasValue ? "방장이 골랐습니다" : "방장이 고르는 중…");

        NetPlayer local = NetPlayer.Local;
        readyButton.gameObject.SetActive(!isHost);
        startButton.gameObject.SetActive(isHost);
        if (!isHost && local != null)
        {
            readyLabel.text = local.Ready ? "준비 취소" : "준비";
            readyButton.GetComponent<Image>().color = local.Ready ? ButtonNormal : ButtonAccent;
        }
        startButton.interactable = launcher.CanStartMatch;
        startHint.text = isHost ? launcher.StartBlockedReason : "";
        roomStatus.text = launcher.Status;
    }

    // ───────────── 만들기 ─────────────

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = (RectTransform)transform;
        Image backdrop = CreateImage(root, "Backdrop", Backdrop);
        Stretch(backdrop.rectTransform);

        TMP_Text title = CreateText(root, "Title", "구 랜 디", 72, boldFont, TextMain, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 110f));
        subtitle = CreateText(root, "Subtitle", "", 28, font, TextDim, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(900f, 40f));

        BuildModePanel(root);
        BuildMainPanel(root);
        BuildRoomPanel(root);
        mainPanel.SetActive(false);
        roomPanel.SetActive(false);
    }

    void BuildModePanel(RectTransform root)
    {
        Image card = CreateImage(root, "ModePanel", Card);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(620f, 380f));
        modePanel = card.gameObject;
        RectTransform c = card.rectTransform;

        Button solo = CreateButton(c, "SoloButton", "혼자 하기", ButtonNormal, 36);
        Place((RectTransform)solo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 75f), new Vector2(480f, 100f));
        solo.onClick.AddListener(() => launcher.PlaySolo());

        Button together = CreateButton(c, "TogetherButton", "같이 하기", ButtonAccent, 36);
        Place((RectTransform)together.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -65f), new Vector2(480f, 100f));
        together.onClick.AddListener(() => multiplayerChosen = true);
    }

    void BuildMainPanel(RectTransform root)
    {
        Image card = CreateImage(root, "MainPanel", Card);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(720f, 560f));
        mainPanel = card.gameObject;
        RectTransform c = card.rectTransform;

        Label(c, "닉네임", new Vector2(0f, 215f));
        nicknameInput = CreateInput(c, "NicknameInput", "이름을 입력하세요", NetPlayer.MaxNicknameLength);
        Place((RectTransform)nicknameInput.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(600f, 64f));
        nicknameInput.text = launcher != null ? launcher.InitialNickname : NetPlayer.LoadNickname();
        nicknameInput.onEndEdit.AddListener(value => NetPlayer.SaveNickname(value));

        createButton = CreateButton(c, "CreateButton", "방 만들기", ButtonAccent, 32);
        Place((RectTransform)createButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 84f));
        createButton.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(nicknameInput.text);
            launcher.CreateRoom();
        });

        TMP_Text or = CreateText(c, "Or", "또는 방 코드로 참가", 24, font, TextDim, TextAlignmentOptions.Center);
        Place(or.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(600f, 40f));

        codeInput = CreateInput(c, "CodeInput", "방 코드 (예: K7QXM)", 12);
        codeInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        codeInput.onValidateInput = (text, index, ch) => char.ToUpperInvariant(ch);
        Place((RectTransform)codeInput.transform, new Vector2(0.5f, 0.5f), new Vector2(-95f, -100f), new Vector2(410f, 72f));

        joinButton = CreateButton(c, "JoinButton", "참가", ButtonNormal, 30);
        Place((RectTransform)joinButton.transform, new Vector2(0.5f, 0.5f), new Vector2(210f, -100f), new Vector2(180f, 72f));
        joinButton.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(nicknameInput.text);
            launcher.JoinRoom(codeInput.text);
        });

        Button back = CreateButton(c, "BackButton", "뒤로", ButtonNormal, 22);
        Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(70f, -34f), new Vector2(100f, 44f));
        back.onClick.AddListener(() => { if (!launcher.IsBusy) multiplayerChosen = false; });

        // 판 도중 끊겨 돌아왔을 때만 보인다 — 기억해 둔 방으로 다시 붙는다(방장이 60초 동안 자리를 붙잡고 있다).
        rejoinButton = CreateButton(c, "RejoinButton", "다시 참가", ButtonAccent, 28);
        Place((RectTransform)rejoinButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -175f), new Vector2(420f, 58f));
        rejoinButton.onClick.AddListener(() => launcher.Rejoin());
        rejoinButton.gameObject.SetActive(false);

        mainStatus = CreateText(c, "Status", "", 22, font, TextDim, TextAlignmentOptions.Center);
        Place(mainStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -238f), new Vector2(660f, 60f));
    }

    void BuildRoomPanel(RectTransform root)
    {
        Image card = CreateImage(root, "RoomPanel", Card);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1120f, 760f));
        roomPanel = card.gameObject;
        RectTransform c = card.rectTransform;

        // 방 코드 + 복사
        TMP_Text codeLabel = CreateText(c, "CodeLabel", "방 코드", 24, font, TextDim, TextAlignmentOptions.Left);
        Place(codeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-400f, 330f), new Vector2(200f, 40f));
        roomCodeText = CreateText(c, "RoomCode", "", 60, boldFont, Selected, TextAlignmentOptions.Left);
        roomCodeText.characterSpacing = 12f;
        Place(roomCodeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-270f, 275f), new Vector2(460f, 76f));
        Button copy = CreateButton(c, "CopyButton", "코드 복사", ButtonNormal, 22);
        Place((RectTransform)copy.transform, new Vector2(0.5f, 0.5f), new Vector2(60f, 275f), new Vector2(160f, 56f));
        copy.onClick.AddListener(() => GUIUtility.systemCopyBuffer = launcher.RoomCode);

        // 슬롯 4줄
        for (int slot = 0; slot < NetSession.MaxSlots; slot++)
        {
            float y = 175f - slot * 78f;
            Image row = CreateImage(c, $"Slot{slot}", Row);
            Place(row.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1040f, 66f));
            slotRows[slot] = row;

            slotNumbers[slot] = CreateText(row.rectTransform, "Number", "", 30, boldFont, TextDim, TextAlignmentOptions.Center);
            Place(slotNumbers[slot].rectTransform, new Vector2(0f, 0.5f), new Vector2(45f, 0f), new Vector2(60f, 60f));
            slotNames[slot] = CreateText(row.rectTransform, "Name", "", 30, font, TextMain, TextAlignmentOptions.Left);
            Place(slotNames[slot].rectTransform, new Vector2(0f, 0.5f), new Vector2(390f, 0f), new Vector2(640f, 60f));
            slotTags[slot] = CreateText(row.rectTransform, "Tag", "", 26, boldFont, TextDim, TextAlignmentOptions.Right);
            Place(slotTags[slot].rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 60f));
        }

        // 난이도
        TMP_Text difficultyLabel = CreateText(c, "DifficultyLabel", "난이도", 26, boldFont, TextMain, TextAlignmentOptions.Left);
        Place(difficultyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-420f, -135f), new Vector2(200f, 40f));
        difficultyHint = CreateText(c, "DifficultyHint", "", 22, font, TextDim, TextAlignmentOptions.Right);
        Place(difficultyHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(360f, -135f), new Vector2(320f, 40f));
        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            DifficultyMode mode = DifficultyOrder[i];
            Button b = CreateButton(c, $"Difficulty{mode}", mode.KoreanName(), ButtonNormal, 26);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(-435f + i * 174f, -195f), new Vector2(160f, 64f));
            b.onClick.AddListener(() => launcher.SetDifficulty(mode));
            // 방장이 아니면 누를 수 없지만, 색조가 비활성으로 바뀌면 버튼 배경이 사라져 「무엇을 골랐나」가 안 읽힌다.
            // 색은 RefreshRoom이 직접 칠한다(선택=노랑).
            b.transition = UnityEngine.UI.Selectable.Transition.None;
            difficultyButtons[i] = b;
            difficultyImages[i] = b.GetComponent<Image>();
        }

        // 준비 / 시작 / 나가기
        readyButton = CreateButton(c, "ReadyButton", "준비", ButtonAccent, 32);
        Place((RectTransform)readyButton.transform, new Vector2(0.5f, 0.5f), new Vector2(120f, -300f), new Vector2(380f, 84f));
        readyLabel = readyButton.GetComponentInChildren<TMP_Text>();
        readyButton.onClick.AddListener(() =>
        {
            if (NetPlayer.Local != null) launcher.SetReady(!NetPlayer.Local.Ready);
        });

        startButton = CreateButton(c, "StartButton", "시작", ButtonAccent, 32);
        Place((RectTransform)startButton.transform, new Vector2(0.5f, 0.5f), new Vector2(120f, -300f), new Vector2(380f, 84f));
        startButton.onClick.AddListener(() => launcher.StartMatch());
        startHint = CreateText(c, "StartHint", "", 20, font, TextDim, TextAlignmentOptions.Center);
        Place(startHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(120f, -362f), new Vector2(520f, 32f));

        Button leave = CreateButton(c, "LeaveButton", "나가기", ButtonDanger, 28);
        Place((RectTransform)leave.transform, new Vector2(0.5f, 0.5f), new Vector2(-330f, -300f), new Vector2(240f, 84f));
        leave.onClick.AddListener(() => launcher.Leave());

        // 대기실 채팅 — 게임 안 채팅과 같은 길(PlayerChat). 줄은 알림 자리(왼쪽 아래)에 쌓인다.
        chatInput = CreateInput(root, "LobbyChatInput", "친구에게 할 말 (Enter로 보내기)", PlayerChat.MaxLength);
        Place((RectTransform)chatInput.transform, new Vector2(0.5f, 0.5f), new Vector2(-90f, -475f), new Vector2(940f, 60f));
        chatInput.onSubmit.AddListener(_ => SendLobbyChat());
        Button send = CreateButton(root, "LobbyChatSend", "보내기", ButtonNormal, 24);
        Place((RectTransform)send.transform, new Vector2(0.5f, 0.5f), new Vector2(470f, -475f), new Vector2(160f, 60f));
        send.onClick.AddListener(SendLobbyChat);
        chatInput.transform.SetParent(card.transform, true);
        send.transform.SetParent(card.transform, true);

        roomStatus = CreateText(c, "Status", "", 20, font, TextDim, TextAlignmentOptions.Right);
        Place(roomStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(360f, 285f), new Vector2(340f, 64f));
    }

    void SendLobbyChat()
    {
        string text = chatInput.text;
        chatInput.text = "";
        chatInput.ActivateInputField();   // 이어서 칠 수 있게 포커스 유지
        if (string.IsNullOrWhiteSpace(text) || !PlayerChat.AllowLocalSend()) return;

        if (launcher.IsHost)
        {
            string name = NetPlayer.Local != null ? NetPlayer.Local.DisplayName : "방장";
            PlayerChat.HandleOnAuthority(LocalPlayer.LocalPlayerId, name, text, out _);
        }
        else NetCommands.RequestChat(text);
    }

    // ───────────── 부품 ─────────────

    void Label(RectTransform parent, string text, Vector2 position)
    {
        TMP_Text label = CreateText(parent, text, text, 24, font, TextDim, TextAlignmentOptions.Left);
        Place(label.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(600f, 36f));
    }

    static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = CreateRect(parent, name).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static TMP_Text CreateText(Transform parent, string name, string text, float size, TMP_FontAsset fontAsset, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI t = CreateRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) t.font = fontAsset;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    Button CreateButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        Image image = CreateImage(parent, name, color);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        button.colors = colors;

        TMP_Text text = CreateText(image.rectTransform, "Label", label, fontSize, boldFont, TextMain, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    TMP_InputField CreateInput(Transform parent, string name, string placeholder, int characterLimit)
    {
        Image background = CreateImage(parent, name, new Color(0.08f, 0.10f, 0.15f, 1f));
        TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();

        RectTransform area = CreateRect(background.rectTransform, "TextArea");
        Stretch(area, 18f, 8f);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholderText = CreateText(area, "Placeholder", placeholder, 28, font, TextDim, TextAlignmentOptions.Left);
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.enableWordWrapping = false;
        Stretch(placeholderText.rectTransform);

        TMP_Text text = CreateText(area, "Text", "", 30, font, TextMain, TextAlignmentOptions.Left);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);

        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.fontAsset = font;
        input.pointSize = 30;
        input.characterLimit = characterLimit;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padX, padY);
        rect.offsetMax = new Vector2(-padX, -padY);
    }
}
