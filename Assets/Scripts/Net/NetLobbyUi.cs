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

    // 게임 HUD·조합 검색 서랍(RecipeSearchDrawer)과 같은 워크3 결: 돌/금 9-슬라이스 패널 · 남색 단추 · 금빛 글자/테두리 · 어두운 홈 입력창.
    static readonly Color Backdrop = new Color(0.02f, 0.03f, 0.07f, 1f);
    static readonly Color Card = new Color(0.12f, 0.19f, 0.36f, 0.97f);          // 그림이 없을 때 물러나는 색
    static readonly Color Row = new Color(0.05f, 0.08f, 0.17f, 0.96f);
    static readonly Color ButtonNormal = Color.white;                              // 남색 그림 그대로
    static readonly Color ButtonAccent = new Color(0.79f, 0.64f, 0.29f, 1f);       // 으뜸 단추 = 금빛 글자·테두리(색 자체가 표식으로도 쓰인다)
    static readonly Color ButtonDanger = new Color(1f, 0.62f, 0.60f, 1f);
    static readonly Color Selected = new Color(1f, 0.84f, 0.25f, 1f);
    static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    static readonly Color TextMain = new Color(0.93f, 0.92f, 0.87f, 1f);
    static readonly Color TextDim = new Color(0.65f, 0.64f, 0.59f, 1f);
    static readonly Color ReadyGreen = new Color(0.44f, 0.83f, 0.44f, 1f);

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
            slotRows[slot].color = me ? new Color(0.12f, 0.20f, 0.40f, 1f) : Row;

            if (player.IsHost) { slotTags[slot].text = "방장"; slotTags[slot].color = Selected; }
            else if (player.Ready) { slotTags[slot].text = "준비 완료"; slotTags[slot].color = ReadyGreen; }
            else { slotTags[slot].text = "준비 중…"; slotTags[slot].color = TextDim; }
        }

        bool isHost = launcher.IsHost;

        NetPlayer local = NetPlayer.Local;
        readyButton.gameObject.SetActive(!isHost);
        startButton.gameObject.SetActive(isHost);
        if (!isHost && local != null)
        {
            readyLabel.text = local.Ready ? "준비 취소" : "준비";
            SetAccent(readyButton, !local.Ready);
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
        BuildBackdrop(root);
        BuildTitle(root);
        subtitle = CreateText(root, "Subtitle", "", 30, boldFont, Gold, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(900f, 44f));

        BuildModePanel(root);
        BuildMainPanel(root);
        BuildRoomPanel(root);
        mainPanel.SetActive(false);
        roomPanel.SetActive(false);
    }

    TMP_InputField saveNickInput;
    TMP_InputField saveCodeInput;
    TMP_Text saveLoadResult;
    RectTransform modeCard;
    GameObject saveGroup;
    TMP_Text saveToggleLabel;
    bool saveOpen;

    // 카드 위쪽 기준(피벗 위)으로 쌓는다 — 세이브 코드 영역을 펴고 접어도 단추 자리가 안 움직인다. y는 카드 위에서 내려온 거리.
    const float ModeCardCollapsed = 360f, ModeCardOpen = 730f;

    void BuildModePanel(RectTransform root)
    {
        Image card = CreateCard(root, "ModePanel");
        PlaceTop(card.rectTransform, new Vector2(0f, 20f), new Vector2(640f, ModeCardCollapsed));
        modePanel = card.gameObject;
        modeCard = card.rectTransform;
        RectTransform c = card.rectTransform;

        Button solo = CreateButton(c, "SoloButton", "혼자 하기", ButtonAccent, 36);
        PlaceFromTop((RectTransform)solo.transform, -84f, new Vector2(500f, 100f));
        solo.onClick.AddListener(() => launcher.PlaySolo());

        Button together = CreateButton(c, "TogetherButton", "같이 하기", ButtonNormal, 36);
        PlaceFromTop((RectTransform)together.transform, -200f, new Vector2(500f, 100f));
        together.onClick.AddListener(() => multiplayerChosen = true);

        // 세이브 코드 불러오기(원작 -load, 사장님 10-03 확정) — 다른 PC에서 만든 코드로 클리어 횟수 등을 이어 받는다. 닉네임이 열쇠.
        // 보조 영역: 평소엔 한 줄 글자 단추로만 보이고, 누르면 펴진다(사장님 10-06 「첫 화면이 너무 구리다」).
        Button toggle = CreateLinkButton(c, "SaveCodeToggle", "세이브 코드 불러오기 ▸", 24);
        saveToggleLabel = toggle.GetComponentInChildren<TMP_Text>();
        PlaceFromTop((RectTransform)toggle.transform, -296f, new Vector2(500f, 44f));
        toggle.onClick.AddListener(() => SetSaveOpen(!saveOpen));

        RectTransform group = CreateRect(c, "SaveGroup");
        group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one; group.offsetMin = group.offsetMax = Vector2.zero;
        saveGroup = group.gameObject;

        TMP_Text hint = CreateText(group, "SaveHint", "다른 PC에서 만든 코드로 기록을 이어받습니다. 닉네임이 열쇠입니다.", 21, font, TextDim, TextAlignmentOptions.Center);
        PlaceFromTop(hint.rectTransform, -352f, new Vector2(560f, 30f));

        saveNickInput = CreateInput(group, "SaveNickInput", "코드를 만들 때 쓴 닉네임", NetPlayer.MaxNicknameLength);
        PlaceFromTop((RectTransform)saveNickInput.transform, -410f, new Vector2(540f, 56f));
        saveNickInput.text = NetPlayer.LoadNickname();
        saveNickInput.onEndEdit.AddListener(value => { NetPlayer.SaveNickname(value); if (nicknameInput != null) nicknameInput.text = NetPlayer.LoadNickname(); });
        AddFieldTag((RectTransform)saveNickInput.transform, "닉네임");
        saveCodeInput = CreateInput(group, "SaveCodeInput", "세이브 코드 (GRD1-…)", 80);
        PlaceFromTop((RectTransform)saveCodeInput.transform, -480f, new Vector2(540f, 56f));
        AddFieldTag((RectTransform)saveCodeInput.transform, "코드");
        Button load = CreateButton(group, "SaveLoadButton", "불러오기", ButtonNormal, 28);
        PlaceFromTop((RectTransform)load.transform, -556f, new Vector2(300f, 60f));
        load.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(saveNickInput.text);
            if (nicknameInput != null) nicknameInput.text = NetPlayer.LoadNickname();
            SaveCodeService.Load(NetPlayer.SanitizeNickname(saveNickInput.text), saveCodeInput.text, out string message);
            saveLoadResult.text = message;
        });
        saveLoadResult = CreateText(group, "SaveLoadResult", "", 22, font, TextMain, TextAlignmentOptions.Center);
        PlaceFromTop(saveLoadResult.rectTransform, -646f, new Vector2(580f, 70f));
        SetSaveOpen(false);
    }

    void SetSaveOpen(bool open)
    {
        saveOpen = open;
        saveGroup.SetActive(open);
        saveToggleLabel.text = open ? "세이브 코드 불러오기 ▾" : "세이브 코드 불러오기 ▸";
        modeCard.sizeDelta = new Vector2(modeCard.sizeDelta.x, open ? ModeCardOpen : ModeCardCollapsed);
    }

    // 입력칸 안 왼쪽 끝에 붙는 작은 이름표 — 입력칸이 어느 값인지 한눈에(사장님 10-06 「닉네임 이름표」). 글자는 이름표 폭만큼 오른쪽으로 민다.
    static void AddFieldTag(RectTransform input, string tag)
    {
        TMP_InputField field = input.GetComponent<TMP_InputField>();
        TMP_Text t = CreateText(input, "Tag", tag, 20, GameHud.UiFontAsset, Gold, TextAlignmentOptions.Left);
        t.rectTransform.anchorMin = new Vector2(0f, 0f); t.rectTransform.anchorMax = new Vector2(0f, 1f);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.rectTransform.anchoredPosition = new Vector2(16f, 0f);
        t.rectTransform.sizeDelta = new Vector2(78f, 0f);
        t.enableWordWrapping = false;
        RectTransform area = field.textViewport;
        area.offsetMin = new Vector2(100f, area.offsetMin.y);
    }

    void BuildMainPanel(RectTransform root)
    {
        Image card = CreateCard(root, "MainPanel");
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
        Image card = CreateCard(root, "RoomPanel");
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

        // 난이도는 방에서 고르지 않는다 — 원작처럼 게임에 들어간 뒤 방장이 대화상자로 정한다(사장님 확정 10-03, j 15263~15285).
        TMP_Text difficultyInfo = CreateText(c, "DifficultyInfo", "난이도는 시작한 뒤 방장이 「모드를 선택하세요.」 창에서 고릅니다.", 24, font, TextDim, TextAlignmentOptions.Center);
        Place(difficultyInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(1040f, 40f));

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

    // 돌/금 9-슬라이스 패널(조합 검색 서랍과 같은 dialog_panel_9s). 그림이 없으면 색 칸으로 물러난다.
    static Image CreateCard(Transform parent, string name)
    {
        Image image = CreateImage(parent, name, Card);
        if (UiSkin.Apply(image, "dialog_panel_9s", Card)) image.pixelsPerUnitMultiplier = 1.7f;
        return image;
    }

    Button CreateButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        Image image = CreateImage(parent, name, Color.white);
        bool skinned = UiSkin.Apply(image, "button_navy_9s", ButtonNormal);
        if (skinned) image.pixelsPerUnitMultiplier = 1.5f;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(image.rectTransform, "Label", label, fontSize, boldFont, TextMain, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        // 호버=밝은 남색 그림, 눌림=어둡게, 못 누름=흐리게(LobbyButtonFx). 으뜸 단추는 금빛 글자 + 금 테두리, 위험 단추는 붉게.
        LobbyButtonFx fx = image.gameObject.AddComponent<LobbyButtonFx>();
        fx.Init(image, text, skinned ? UiSkin.Get("button_navy_9s") : null, skinned ? UiSkin.Get("button_navy_hover_9s") : null);
        GameObject border = AddGoldBorder(image.rectTransform);
        border.name = "GoldBorder";
        fx.border = border;
        if (color == ButtonAccent) SetAccent(button, true);
        else
        {
            SetAccent(button, false);
            if (color == ButtonDanger) { fx.baseTint = ButtonDanger; text.color = new Color(1f, 0.82f, 0.78f, 1f); }
        }
        return button;
    }

    // 으뜸(금빛) ↔ 보통. 준비 단추처럼 상태에 따라 바뀌는 것도 이걸로.
    static void SetAccent(Button button, bool accent)
    {
        LobbyButtonFx fx = button.GetComponent<LobbyButtonFx>();
        if (fx == null) return;
        if (fx.border != null) fx.border.SetActive(accent);
        fx.label.color = accent ? new Color(1f, 0.88f, 0.45f, 1f) : TextMain;
        fx.accent = accent;
    }

    // 글자만 있는 보조 단추(밑줄 없이 금빛, 호버 땐 밝게) — 세이브 코드 영역 접이식 머리 같은 곳.
    Button CreateLinkButton(Transform parent, string name, string label, float fontSize)
    {
        Image hit = CreateImage(parent, name, new Color(0f, 0f, 0f, 0.001f));
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        TMP_Text text = CreateText(hit.rectTransform, "Label", label, fontSize, font, TextDim, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        LinkHover hover = hit.gameObject.AddComponent<LinkHover>();
        hover.label = text; hover.normal = TextDim; hover.hot = Selected;
        return button;
    }

    static GameObject AddGoldBorder(RectTransform parent)
    {
        RectTransform holder = CreateRect(parent, "Border");
        holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one; holder.offsetMin = holder.offsetMax = Vector2.zero;
        void Edge(Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            Image e = CreateImage(holder, "E", Gold);
            e.raycastTarget = false;
            e.rectTransform.anchorMin = min; e.rectTransform.anchorMax = max; e.rectTransform.offsetMin = offMin; e.rectTransform.offsetMax = offMax;
        }
        const float t = 2f;
        Edge(new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -t), Vector2.zero);
        Edge(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t));
        Edge(Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f));
        Edge(new Vector2(1f, 0f), Vector2.one, new Vector2(-t, 0f), Vector2.zero);
        return holder.gameObject;
    }

    // 어두운 홈 입력창(console_cell_frame_9s) + 금빛 캐럿
    TMP_InputField CreateInput(Transform parent, string name, string placeholder, int characterLimit)
    {
        Image background = CreateImage(parent, name, new Color(0.02f, 0.035f, 0.09f, 1f));
        if (UiSkin.Apply(background, "console_cell_frame_9s", new Color(0.02f, 0.035f, 0.09f, 1f))) background.pixelsPerUnitMultiplier = 2.2f;
        TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();

        RectTransform area = CreateRect(background.rectTransform, "TextArea");
        Stretch(area, 18f, 8f);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholderText = CreateText(area, "Placeholder", placeholder, 26, font, new Color(0.45f, 0.47f, 0.52f, 1f), TextAlignmentOptions.Left);
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.enableWordWrapping = false;
        Stretch(placeholderText.rectTransform);

        TMP_Text text = CreateText(area, "Text", "", 28, font, TextMain, TextAlignmentOptions.Left);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);

        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.fontAsset = font;
        input.pointSize = 28;
        input.characterLimit = characterLimit;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.caretColor = Selected;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.79f, 0.64f, 0.29f, 0.35f);
        return input;
    }

    // ───────────── 배경 · 제목 ─────────────
    // 단색 대신: 위가 남색인 세로 그라데이션 + 돌 무늬(stone_tile) 은은히 + 가장자리 어둡게(비네트) + 제목 뒤 금빛 후광. 전부 코드로 그려 파일이 안 는다.

    static Texture2D MakeTexture(int w, int h, System.Func<float, float, Color> pixel)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel((x + 0.5f) / w, (y + 0.5f) / h);
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static RawImage AddRaw(Transform parent, string name, Texture tex)
    {
        RawImage raw = CreateRect(parent, name).gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        return raw;
    }

    void BuildBackdrop(RectTransform root)
    {
        Image solid = CreateImage(root, "Backdrop", Backdrop);
        Stretch(solid.rectTransform);

        // 세로 그라데이션: 위 (0.09,0.14,0.30) → 가운데 (0.04,0.07,0.16) → 아래 (0.01,0.015,0.04)
        Texture2D grad = MakeTexture(2, 256, (u, v) =>
        {
            Color top = new Color(0.10f, 0.15f, 0.32f), mid = new Color(0.04f, 0.07f, 0.16f), bottom = new Color(0.01f, 0.015f, 0.04f);
            Color c = v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, mid, v * 2f);
            c.a = 1f;
            return c;
        });
        Stretch(AddRaw(root, "Gradient", grad).rectTransform);

        Sprite stone = UiSkin.Get("stone_tile");
        if (stone != null)
        {
            Image tile = CreateImage(root, "StoneTexture", new Color(1f, 1f, 1f, 0.07f));
            tile.sprite = stone;
            tile.type = Image.Type.Tiled;
            tile.raycastTarget = false;
            Stretch(tile.rectTransform);
        }

        // 제목 뒤 금빛 후광
        Texture2D glow = MakeTexture(128, 128, (u, v) =>
        {
            float d = Mathf.Clamp01(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f);
            float a = Mathf.Pow(1f - d, 2.2f) * 0.30f;
            return new Color(1f, 0.78f, 0.30f, a);
        });
        RawImage halo = AddRaw(root, "TitleGlow", glow);
        Place(halo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1300f, 420f));

        // 비네트: 가운데 투명 → 가장자리 검정
        Texture2D vignette = MakeTexture(128, 128, (u, v) =>
        {
            float d = Mathf.Clamp01(Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 1.45f);
            return new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.78f, Mathf.Clamp01((d - 0.35f) / 0.65f)));
        });
        Stretch(AddRaw(root, "Vignette", vignette).rectTransform);
    }

    void BuildTitle(RectTransform root)
    {
        // 로고처럼: 금빛 위→아래 그라데이션 글자 + 짙은 갈색 외곽선 + 아래로 깔리는 그림자. 글자 사이를 띄운 「구 랜 디」는 버렸다.
        TMP_Text shadow = CreateText(root, "TitleShadow", "구랜디", 128, boldFont, new Color(0f, 0f, 0f, 0.65f), TextAlignmentOptions.Center);
        Place(shadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(4f, -124f), new Vector2(900f, 170f));
        shadow.characterSpacing = 6f;

        TMP_Text title = CreateText(root, "Title", "구랜디", 128, boldFont, Color.white, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(900f, 170f));
        title.characterSpacing = 6f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(
            new Color(1f, 0.96f, 0.70f, 1f), new Color(1f, 0.96f, 0.70f, 1f),
            new Color(0.88f, 0.60f, 0.16f, 1f), new Color(0.88f, 0.60f, 0.16f, 1f));
        title.outlineWidth = 0.22f;
        title.outlineColor = new Color32(58, 32, 8, 255);

        // 제목 아래 금 줄(양 끝이 옅어진다)
        Texture2D line = MakeTexture(256, 4, (u, v) => new Color(0.79f, 0.64f, 0.29f, Mathf.Sin(u * Mathf.PI) * 0.9f));
        RawImage rule = AddRaw(root, "TitleRule", line);
        Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -208f), new Vector2(560f, 3f));
    }

    // 첫 화면 카드를 위쪽 기준으로 둔다(펴고 접어도 위가 고정). position.y = 화면 가운데에서 위로 올린 거리.
    static void PlaceTop(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 카드 안에서 위 끝 기준으로 놓는다(y는 위에서 내려온 거리, 음수 = 아래쪽 · 가운데 정렬).
    static void PlaceFromTop(RectTransform rect, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
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

/// <summary>첫 화면 단추 효과 — 호버/눌림 그림 바꾸기, 못 누르면 흐리게. 코드로 만든 단추라 Animator 없이 이것만 쓴다.</summary>
public class LobbyButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Image image; UnityEngine.UI.Selectable selectable;
    Sprite normalSprite, hoverSprite;
    public TMP_Text label;
    public GameObject border;
    public bool accent;
    public Color baseTint = Color.white;
    bool hover, down;

    public void Init(Image img, TMP_Text text, Sprite normal, Sprite hot)
    {
        image = img; label = text; normalSprite = normal; hoverSprite = hot;
        selectable = GetComponent<UnityEngine.UI.Selectable>();
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { hover = false; down = false; Apply(); }
    public void OnPointerDown(PointerEventData e) { down = true; Apply(); }
    public void OnPointerUp(PointerEventData e) { down = false; Apply(); }

    bool lastInteractable = true;
    void Update()
    {
        bool ok = selectable == null || selectable.interactable;
        if (ok != lastInteractable) { lastInteractable = ok; if (!ok) { hover = down = false; } Apply(); }
    }

    void OnDisable() { hover = down = false; }

    void Apply()
    {
        bool ok = selectable == null || selectable.interactable;
        if (image == null) return;
        if (normalSprite != null) image.sprite = ok && (hover || down) ? hoverSprite : normalSprite;
        Color tint = baseTint;
        if (!ok) tint *= new Color(0.55f, 0.55f, 0.55f, 0.75f);
        else if (down) tint *= new Color(0.78f, 0.78f, 0.82f, 1f);
        else if (hover && normalSprite == null) tint *= new Color(1.12f, 1.12f, 1.12f, 1f);
        image.color = tint;
        if (label != null)
        {
            Color c = label.color; c.a = ok ? 1f : 0.5f; label.color = c;
            label.rectTransform.anchoredPosition = down && ok ? new Vector2(0f, -2f) : Vector2.zero;
        }
        if (border != null)
        {
            foreach (Image e in border.GetComponentsInChildren<Image>(true))
                e.color = ok ? (hover ? new Color(1f, 0.84f, 0.25f, 1f) : new Color(0.79f, 0.64f, 0.29f, 1f)) : new Color(0.79f, 0.64f, 0.29f, 0.35f);
        }
    }
}

public class LinkHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label; public Color normal, hot;
    public void OnPointerEnter(PointerEventData e) { if (label != null) label.color = hot; }
    public void OnPointerExit(PointerEventData e) { if (label != null) label.color = normal; }
}
