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
    // blender 시안(사장님 10-06 확정): 노을 섬 사진 배경 + 청동·돌 틀(제목 판·메뉴 틀·단추) + 금빛 글자. 그림은 Resources/UI/Lobby(LobbyArtSetup), 폰트 SongMyung(제목)·NanumMyeongjo ExtraBold(단추·본문).
    static readonly Color Backdrop = Color.black;
    static readonly Color Card = new Color(0.14f, 0.10f, 0.07f, 0.97f);            // 그림이 없을 때 물러나는 색
    static readonly Color Row = new Color(0.10f, 0.07f, 0.05f, 0.88f);
    static readonly Color ButtonNormal = Color.white;
    static readonly Color ButtonAccent = Color.white;                              // 시안엔 으뜸 단추 표식이 없다(호버 때 금테 그림으로 바뀐다)
    static readonly Color ButtonDanger = new Color(1f, 0.70f, 0.66f, 1f);
    static readonly Color Selected = new Color(1f, 0.84f, 0.25f, 1f);
    static readonly Color Gold = new Color(0.79f, 0.64f, 0.29f, 1f);
    static readonly Color ButtonText = new Color(0.96f, 0.86f, 0.58f, 1f);         // 단추 글자 금빛
    static readonly Color TextMain = new Color(0.96f, 0.93f, 0.84f, 1f);
    static readonly Color TextDim = new Color(0.80f, 0.72f, 0.54f, 1f);
    static readonly Color ReadyGreen = new Color(0.52f, 0.88f, 0.50f, 1f);
    static readonly Vector2 FrameSize = new Vector2(480f, 912f);
    static readonly Vector2 FramePos = new Vector2(-324f, -24f);                    // 화면 오른쪽 가운데 기준(시안 1600×900의 메뉴 틀 자리)
    static readonly Vector2 ButtonSize = new Vector2(348f, 84f);

    NetLauncher launcher;
    Canvas canvas;
    TMP_FontAsset font;        // 본문·단추(Nanum Myeongjo ExtraBold)
    TMP_FontAsset boldFont;
    TMP_FontAsset titleFont;   // 제목(Song Myung)
    TMP_FontAsset inputFont;   // 입력칸(읽기 쉬운 Pretendard)

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
        inputFont = GameHud.UiFontAsset;
        font = Resources.Load<TMP_FontAsset>("Fonts/NanumMyeongjo-ExtraBold SDF") ?? inputFont;
        boldFont = font;
        titleFont = Resources.Load<TMP_FontAsset>("Fonts/SongMyung SDF") ?? font;
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
            slotRows[slot].color = me ? new Color(0.34f, 0.22f, 0.08f, 0.95f) : Row;

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
        subtitle = CreateText(root, "Subtitle", "", 34, boldFont, ButtonText, TextAlignmentOptions.Center);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(636f, -262f), new Vector2(900f, 48f));
        subtitle.outlineWidth = 0.22f;
        subtitle.outlineColor = new Color32(40, 22, 6, 255);

        BuildModePanel(root);
        BuildMainPanel(root);
        BuildRoomPanel(root);
        mainPanel.SetActive(false);
        roomPanel.SetActive(false);
    }

    TMP_InputField saveNickInput;
    TMP_InputField saveCodeInput;
    TMP_Text saveLoadResult;
    GameObject saveGroup;
    bool saveOpen;
    RectTransform soloRect, togetherRect, toggleRect;

    // 메뉴 틀 안 단추 자리(틀 위에서 내려온 거리, 시안 1600×900 y 316·428·540 → 틀 안 271·406·541). 세이브 코드를 펴면 위 세 단추가 촘촘해지고 아래에 입력칸이 선다.
    static readonly float[] ClosedY = { 271f, 406f, 541f };
    static readonly float[] OpenY = { 235f, 330f, 425f };

    void BuildModePanel(RectTransform root)
    {
        Image card = CreateFrame(root, "ModePanel");
        modePanel = card.gameObject;
        RectTransform c = card.rectTransform;

        Button solo = CreateButton(c, "SoloButton", "혼자 하기", ButtonAccent, 38);
        soloRect = (RectTransform)solo.transform;
        solo.onClick.AddListener(() => launcher.PlaySolo());

        Button together = CreateButton(c, "TogetherButton", "같이 하기", ButtonNormal, 38);
        togetherRect = (RectTransform)together.transform;
        together.onClick.AddListener(() => multiplayerChosen = true);

        // 세이브 코드 불러오기(원작 -load, 사장님 10-03 확정) — 다른 PC에서 만든 코드로 클리어 횟수 등을 이어 받는다. 닉네임이 열쇠. 눌러 펼친다.
        Button toggle = CreateButton(c, "SaveCodeToggle", "세이브 코드 불러오기", ButtonNormal, 31);
        toggleRect = (RectTransform)toggle.transform;
        toggle.onClick.AddListener(() => SetSaveOpen(!saveOpen));

        RectTransform group = CreateRect(c, "SaveGroup");
        group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one; group.offsetMin = group.offsetMax = Vector2.zero;
        saveGroup = group.gameObject;

        saveNickInput = CreateInput(group, "SaveNickInput", "코드를 만들 때 쓴 닉네임", NetPlayer.MaxNicknameLength);
        PlaceFromTop((RectTransform)saveNickInput.transform, -515f, new Vector2(340f, 56f));
        saveNickInput.text = NetPlayer.LoadNickname();
        saveNickInput.onEndEdit.AddListener(value => { NetPlayer.SaveNickname(value); if (nicknameInput != null) nicknameInput.text = NetPlayer.LoadNickname(); });
        AddFieldTag((RectTransform)saveNickInput.transform, "닉네임");
        saveCodeInput = CreateInput(group, "SaveCodeInput", "세이브 코드 (GRD1-…)", 80);
        PlaceFromTop((RectTransform)saveCodeInput.transform, -585f, new Vector2(340f, 56f));
        AddFieldTag((RectTransform)saveCodeInput.transform, "코드");
        Button load = CreateButton(group, "SaveLoadButton", "불러오기", ButtonNormal, 28);
        PlaceFromTop((RectTransform)load.transform, -662f, new Vector2(240f, 64f));
        SizeButton(load, new Vector2(240f, 64f));
        load.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(saveNickInput.text);
            if (nicknameInput != null) nicknameInput.text = NetPlayer.LoadNickname();
            SaveCodeService.Load(NetPlayer.SanitizeNickname(saveNickInput.text), saveCodeInput.text, out string message);
            saveLoadResult.text = message;
        });
        saveLoadResult = CreateText(group, "SaveLoadResult", "", 21, inputFont, TextMain, TextAlignmentOptions.Center);
        PlaceFromTop(saveLoadResult.rectTransform, -742f, new Vector2(400f, 64f));
        SetSaveOpen(false);
    }

    void SetSaveOpen(bool open)
    {
        saveOpen = open;
        saveGroup.SetActive(open);
        float[] ys = open ? OpenY : ClosedY;
        PlaceFromTop(soloRect, -ys[0], ButtonSize);
        PlaceFromTop(togetherRect, -ys[1], ButtonSize);
        PlaceFromTop(toggleRect, -ys[2], ButtonSize);
    }

    // 입력칸 안 왼쪽 끝에 붙는 작은 이름표 — 입력칸이 어느 값인지 한눈에(사장님 10-06 「닉네임 이름표」). 글자는 이름표 폭만큼 오른쪽으로 민다.
    static void AddFieldTag(RectTransform input, string tag)
    {
        TMP_InputField field = input.GetComponent<TMP_InputField>();
        TMP_Text t = CreateText(input, "Tag", tag, 20, GameHud.UiFontAsset, Gold, TextAlignmentOptions.Left);
        t.rectTransform.anchorMin = new Vector2(0f, 0f); t.rectTransform.anchorMax = new Vector2(0f, 1f);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.rectTransform.anchoredPosition = new Vector2(24f, 0f);
        t.rectTransform.sizeDelta = new Vector2(78f, 0f);
        t.enableWordWrapping = false;
        RectTransform area = field.textViewport;
        area.offsetMin = new Vector2(112f, area.offsetMin.y);
    }

    // 같이 하기: 같은 메뉴 틀 안에 세로로 — 닉네임 · 방 만들기 · 방 코드로 참가.
    void BuildMainPanel(RectTransform root)
    {
        Image card = CreateFrame(root, "MainPanel");
        mainPanel = card.gameObject;
        RectTransform c = card.rectTransform;

        TMP_Text nickLabel = CreateText(c, "닉네임", "닉네임", 28, font, TextDim, TextAlignmentOptions.Left);
        PlaceFromTop(nickLabel.rectTransform, -233f, new Vector2(340f, 38f));
        nicknameInput = CreateInput(c, "NicknameInput", "이름을 입력하세요", NetPlayer.MaxNicknameLength);
        PlaceFromTop((RectTransform)nicknameInput.transform, -292f, new Vector2(340f, 60f));
        nicknameInput.text = launcher != null ? launcher.InitialNickname : NetPlayer.LoadNickname();
        nicknameInput.onEndEdit.AddListener(value => NetPlayer.SaveNickname(value));

        createButton = CreateButton(c, "CreateButton", "방 만들기", ButtonAccent, 36);
        PlaceFromTop((RectTransform)createButton.transform, -402f, ButtonSize);
        createButton.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(nicknameInput.text);
            launcher.CreateRoom();
        });

        TMP_Text or = CreateText(c, "Or", "또는 방 코드로 참가", 26, font, TextDim, TextAlignmentOptions.Center);
        PlaceFromTop(or.rectTransform, -492f, new Vector2(400f, 40f));

        codeInput = CreateInput(c, "CodeInput", "방 코드", 12);
        codeInput.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        codeInput.onValidateInput = (text, index, ch) => char.ToUpperInvariant(ch);
        PlaceFromTopX((RectTransform)codeInput.transform, -75f, -556f, new Vector2(200f, 58f));

        joinButton = CreateButton(c, "JoinButton", "참가", ButtonNormal, 28);
        PlaceFromTopX((RectTransform)joinButton.transform, 115f, -556f, new Vector2(124f, 58f));
        SizeButton(joinButton, new Vector2(124f, 58f));
        joinButton.onClick.AddListener(() =>
        {
            NetPlayer.SaveNickname(nicknameInput.text);
            launcher.JoinRoom(codeInput.text);
        });

        // 틀 위쪽 바깥(화면 위 가장자리와 틀 사이)에 작게 — 메뉴 틀 안은 단추 자리라 비운다.
        Button back = CreateButton(c, "BackButton", "뒤로", ButtonNormal, 26);
        PlaceFromTopX((RectTransform)back.transform, -150f, 52f, new Vector2(170f, 60f));
        SizeButton(back, new Vector2(170f, 60f));
        back.onClick.AddListener(() => { if (!launcher.IsBusy) multiplayerChosen = false; });

        // 판 도중 끊겨 돌아왔을 때만 보인다 — 기억해 둔 방으로 다시 붙는다(방장이 60초 동안 자리를 붙잡고 있다).
        rejoinButton = CreateButton(c, "RejoinButton", "다시 참가", ButtonAccent, 32);
        PlaceFromTop((RectTransform)rejoinButton.transform, -735f, new Vector2(348f, 76f));
        rejoinButton.onClick.AddListener(() => launcher.Rejoin());
        rejoinButton.gameObject.SetActive(false);

        mainStatus = CreateText(c, "Status", "", 22, inputFont, TextMain, TextAlignmentOptions.Center);
        PlaceFromTop(mainStatus.rectTransform, -640f, new Vector2(400f, 84f));
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

    static Sprite Lobby(string name) => Resources.Load<Sprite>("UI/Lobby/" + name);

    // 대기실 큰 카드: 시안의 메뉴 틀 그림을 얇은 테두리로(가로로 넓게 늘린다). 그림이 없으면 색 칸.
    static Image CreateCard(Transform parent, string name)
    {
        Image image = CreateImage(parent, name, Card);
        Sprite frame = Lobby("btn_normal");   // 가로로 넓은 카드엔 룬 머리띠가 늘어나 보여서 단추 판(청동 테두리 + 어두운 철판)을 쓴다
        if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; image.color = Color.white; image.pixelsPerUnitMultiplier = 1.1f; }
        return image;
    }

    // 첫 화면 메뉴 틀(시안: 청동 테두리 + 룬 머리·꼬리 + 갈색 가죽 속) — 화면 오른쪽, 크기 고정.
    static Image CreateFrame(Transform parent, string name)
    {
        Image image = CreateImage(parent, name, Card);
        Sprite frame = Lobby("menu_frame");
        if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; image.color = Color.white; image.pixelsPerUnitMultiplier = 512f / FrameSize.x; }
        Place(image.rectTransform, new Vector2(1f, 0.5f), FramePos, FrameSize);
        return image;
    }

    // 단추: 시안 그림(btn_normal/hover/pressed, 테두리 40). 호버·눌림 때 그림을 바꾼다(LobbyButtonFx). 글자는 Nanum Myeongjo ExtraBold 금빛.
    Button CreateButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        Image image = CreateImage(parent, name, Color.white);
        Sprite normal = Lobby("btn_normal");
        if (normal != null) { image.sprite = normal; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 512f / ButtonSize.x; }
        else image.color = new Color(0.20f, 0.18f, 0.16f, 1f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.None;   // 그림 교체는 LobbyButtonFx

        TMP_Text text = CreateText(image.rectTransform, "Label", label, fontSize, boldFont, ButtonText, TextAlignmentOptions.Center);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);

        LobbyButtonFx fx = image.gameObject.AddComponent<LobbyButtonFx>();
        fx.Init(image, text, normal, Lobby("btn_hover"), Lobby("btn_pressed"));
        if (color == ButtonDanger) { fx.baseTint = ButtonDanger; }
        return button;
    }

    // 작은 단추는 그림 테두리도 같이 작아져야 한다(테두리 40px가 단추 높이를 먹지 않게).
    static void SizeButton(Button button, Vector2 size)
    {
        Image image = button.GetComponent<Image>();
        if (image != null && image.type == Image.Type.Sliced) image.pixelsPerUnitMultiplier = 512f / size.x;
        ((RectTransform)button.transform).sizeDelta = size;
    }

    // 준비 단추처럼 상태에 따라 글자색만 바꾸는 으뜸 표시(시안은 단추 모양이 하나라 글자색으로 구분).
    static void SetAccent(Button button, bool accent)
    {
        LobbyButtonFx fx = button.GetComponent<LobbyButtonFx>();
        if (fx == null || fx.label == null) return;
        fx.accent = accent;
        fx.label.color = accent ? new Color(1f, 0.92f, 0.62f, 1f) : TextDim;
    }

    static GameObject AddGoldBorder(RectTransform parent, Color color, float t)
    {
        RectTransform holder = CreateRect(parent, "Border");
        holder.anchorMin = Vector2.zero; holder.anchorMax = Vector2.one; holder.offsetMin = holder.offsetMax = Vector2.zero;
        void Edge(Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            Image e = CreateImage(holder, "E", color);
            e.raycastTarget = false;
            e.rectTransform.anchorMin = min; e.rectTransform.anchorMax = max; e.rectTransform.offsetMin = offMin; e.rectTransform.offsetMax = offMax;
        }
        Edge(new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -t), Vector2.zero);
        Edge(Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t));
        Edge(Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f));
        Edge(new Vector2(1f, 0f), Vector2.one, new Vector2(-t, 0f), Vector2.zero);
        return holder.gameObject;
    }

    // 어두운 홈 입력창(console_cell_frame_9s) + 금빛 캐럿
    TMP_InputField CreateInput(Transform parent, string name, string placeholder, int characterLimit)
    {
        Image background = CreateImage(parent, name, new Color(0.05f, 0.035f, 0.025f, 0.96f));
        TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();

        RectTransform area = CreateRect(background.rectTransform, "TextArea");
        Stretch(area, 18f, 8f);
        area.gameObject.AddComponent<RectMask2D>();

        TMP_Text placeholderText = CreateText(area, "Placeholder", placeholder, 26, inputFont, new Color(0.45f, 0.47f, 0.52f, 1f), TextAlignmentOptions.Left);
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.enableWordWrapping = false;
        Stretch(placeholderText.rectTransform);

        TMP_Text text = CreateText(area, "Text", "", 28, inputFont, TextMain, TextAlignmentOptions.Left);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);

        // 어두운 홈 + 금빛이 죽은 가는 테두리(그림 프레임은 칸이 낮을 때 모서리가 줄무늬로 늘어났다)
        GameObject groove = AddGoldBorder(background.rectTransform, new Color(0.62f, 0.48f, 0.20f, 1f), 2f);
        groove.transform.SetAsFirstSibling();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.fontAsset = inputFont;
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
        // 시안의 노을 섬 사진 — 화면 높이가 1080을 넘으면 큰 쪽(2560×1440). 화면 비율이 달라도 꽉 채우고 넘치는 쪽은 잘린다.
        string name = Screen.height > 1100 || Screen.width > 1950 ? "bg_2560x1440" : "bg_1920x1080";
        Texture2D photo = Resources.Load<Texture2D>("UI/Lobby/" + name);
        if (photo == null) return;
        RawImage raw = AddRaw(root, "Photo", photo);
        Stretch(raw.rectTransform);
        AspectRatioFitter fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)photo.width / photo.height;
    }

    void BuildTitle(RectTransform root)
    {
        // 시안: 왼쪽 위 청동 제목 판(양끝 붉은 보석) + 「구랜디」 Song Myung 금빛 글자.
        Sprite plate = Lobby("title_plate");
        Vector2 plateCenter = new Vector2(636f, -136f);
        if (plate != null)
        {
            Image plateImage = CreateImage(root, "TitlePlate", Color.white);
            plateImage.sprite = plate;
            plateImage.preserveAspect = true;
            plateImage.raycastTarget = false;
            Place(plateImage.rectTransform, new Vector2(0f, 1f), plateCenter, new Vector2(984f, 211f));
        }
        TMP_Text shadow = CreateText(root, "TitleShadow", "구랜디", 104, titleFont, new Color(0f, 0f, 0f, 0.55f), TextAlignmentOptions.Center);
        Place(shadow.rectTransform, new Vector2(0f, 1f), plateCenter + new Vector2(3f, -9f), new Vector2(700f, 160f));
        TMP_Text title = CreateText(root, "Title", "구랜디", 104, titleFont, Color.white, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, 1f), plateCenter + new Vector2(0f, -4f), new Vector2(700f, 160f));
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(
            new Color(1f, 0.93f, 0.62f, 1f), new Color(1f, 0.93f, 0.62f, 1f),
            new Color(0.90f, 0.62f, 0.18f, 1f), new Color(0.90f, 0.62f, 0.18f, 1f));
        title.outlineWidth = 0.16f;
        title.outlineColor = new Color32(48, 26, 6, 255);
    }

    // 첫 화면 카드를 위쪽 기준으로 둔다(펴고 접어도 위가 고정). position.y = 화면 가운데에서 위로 올린 거리.
    static void PlaceTop(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // x 오프셋이 있는 PlaceFromTop.
    static void PlaceFromTopX(RectTransform rect, float x, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
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

/// <summary>첫 화면 단추 효과 — 호버/눌림 그림 바꾸기(시안 btn_normal·hover·pressed), 못 누르면 흐리게. 코드로 만든 단추라 Animator 없이 이것만 쓴다.</summary>
public class LobbyButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Image image; UnityEngine.UI.Selectable selectable;
    Sprite normalSprite, hoverSprite, pressedSprite;
    public TMP_Text label;
    public bool accent;
    public Color baseTint = Color.white;
    bool hover, down;

    public void Init(Image img, TMP_Text text, Sprite normal, Sprite hot, Sprite pressed)
    {
        image = img; label = text; normalSprite = normal; hoverSprite = hot; pressedSprite = pressed;
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
        if (normalSprite != null)
        {
            Sprite pick = normalSprite;
            if (ok && down && pressedSprite != null) pick = pressedSprite;
            else if (ok && (hover || down) && hoverSprite != null) pick = hoverSprite;
            image.sprite = pick;
        }
        Color tint = baseTint;
        if (!ok) tint *= new Color(0.55f, 0.55f, 0.55f, 0.8f);
        image.color = tint;
        if (label != null)
        {
            Color c = label.color; c.a = ok ? 1f : 0.5f; label.color = c;
            label.rectTransform.anchoredPosition = down && ok ? new Vector2(0f, -2f) : Vector2.zero;
        }
    }
}
