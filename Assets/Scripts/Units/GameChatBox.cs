using UnityEngine;
using UnityEngine.InputSystem;

// 채팅 코드(ChatUnlockManager: 초월·불멸·영원·니카)와 히든 조합(HiddenCombineManager)을
// 하나의 입력창으로 받는다(사장님 지시 2026-09-05: "채팅창 하나로"). 원작 채팅 동작 그대로 —
// 평소엔 아무것도 안 뜨고, 엔터를 누르면 입력창이 열리고, 다시 엔터를 누르면 그 문구로 두
// 판정기를 차례로 시도한 뒤 창을 닫는다. Esc로도 닫힌다(제출 안 함).
//
// 위치는 하단 명령 HUD 바로 위 왼쪽이다 — GameHud.cs 주석의 "하단 HUD(y 0~0.22)"와 같은
// 값을 쓴다(GameHud를 직접 참조하진 않는다 — 그 파일은 오늘 다른 세션들과 겹쳐서 커밋이
// 엉킨 적이 있어 PM 지시로 손대지 않는다).
public class GameChatBox : MonoBehaviour
{
    [SerializeField] ChatUnlockManager chatUnlockManager;
    [SerializeField] HiddenCombineManager hiddenCombineManager;

    const float BottomHudHeightFraction = 0.27f;
    // 사장님 10-07 「채팅도 너무 작다」 — 입력칸 폭 320→520·높이 28→42·글자 24, 옛 Stone 톤(검정 바탕 + 금테), 「[전체]」 표시, 위/아래 화살표 = 최근 입력.
    const float BoxWidth = 520f;
    const float BoxHeight = 42f;
    const float BottomGap = 8f;
    const float LeftMargin = 10f;

    // 제출 뒤 결과를 잠깐 보여준다 — 원작 채팅창도 보낸 말이 잠깐 남았다 사라진다.
    const float StatusDisplaySeconds = 4f;

    bool isOpen;
    string inputText = "";
    readonly System.Collections.Generic.List<string> history = new System.Collections.Generic.List<string>();   // 최근 입력(최신이 뒤) — 위/아래 화살표
    int historyIndex = -1;
    GUIStyle boxStyle, fieldStyle, labelStyle;
    bool wc3Chat;
    string statusMessage = "";
    float statusHideTime;

    void Update()
    {
        if (Keyboard.current == null) return;

        if (isOpen)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (SubmitPressed())
            {
                Submit();
                return;
            }

            // 최근 입력 불러오기 — 위 = 더 예전, 아래 = 더 최근(맨 아래를 넘으면 빈 칸)
            if (history.Count > 0)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    historyIndex = historyIndex < 0 ? history.Count - 1 : Mathf.Max(0, historyIndex - 1);
                    inputText = history[historyIndex];
                }
                else if (Keyboard.current.downArrowKey.wasPressedThisFrame && historyIndex >= 0)
                {
                    historyIndex++;
                    if (historyIndex >= history.Count) { historyIndex = -1; inputText = ""; }
                    else inputText = history[historyIndex];
                }
            }
            return;
        }

        if (SubmitPressed())
        {
            Open();
        }
    }

    static bool SubmitPressed() =>
        Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;

    void Open()
    {
        isOpen = true;
        inputText = "";
        historyIndex = -1;
        ChatInputGate.IsOpen = true;
    }

    void Close()
    {
        isOpen = false;
        inputText = "";
        ChatInputGate.IsOpen = false;
    }

    // ChatUnlockManager 먼저, 그 매니저가 문구 자체를 못 알아보면(message==null) HiddenCombineManager로
    // 넘긴다. 어느 한쪽이 "안다"고 하면(성공이든 실패든 message!=null) 거기서 끝 — 둘 다 모르면
    // "인식할 수 없는 코드"를 낸다. 겹치는 문구가 없다는 전제다(원작 CSV·HiddenCombineData
    // 문구가 서로 다르다).
    void Submit()
    {
        PlayerContext local = PlayerContext.Local;
        string text = inputText;
        if (!string.IsNullOrWhiteSpace(text) && (history.Count == 0 || history[history.Count - 1] != text))
        {
            history.Add(text);
            if (history.Count > 20) history.RemoveAt(0);
        }
        Close();

        if (local == null || string.IsNullOrWhiteSpace(text)) return;

        // 원작 OnChatLoad: 「-load 코드」 — 판정은 이 PC에서(내 파일·내 닉네임이 열쇠). 말로는 뿌리지 않는다(코드 노출 방지).
        if (text.TrimStart().StartsWith("-load ", System.StringComparison.OrdinalIgnoreCase))
        {
            ShowStatus(HandleLoadCommand(local, text.TrimStart().Substring(6)));
            return;
        }

        // 10-03 「시야 N」 카메라 줌 — 이 PC 화면만 바꾸니 호스트로 안 보내고 말로도 안 뿌린다.
        string sight = RtsCameraController.TryHandleSightChat(text);
        if (sight != null)
        {
            ShowStatus(sight);
            return;
        }

        // MP: 채팅 한 줄이 곧 코드 입력(원작 워크3). 판정은 PlayerChat이 한다 — 코드면 실행, 말은 전원(싱글은 나)에게 한 줄.
        //     멀티 클라는 호스트에 보내기만 한다(코드 결과는 알림으로, 채팅 줄은 전원에게 돌아온다).
        if (!PlayerChat.AllowLocalSend()) return;
        if (!GameAuthority.IsServer)
        {
            NetCommands.RequestChat(text);
            return;
        }

        string name = MatchConfig.Active && NetPlayer.Local != null ? NetPlayer.Local.DisplayName : LocalName();
        string codeResult = PlayerChat.HandleOnAuthority(local.PlayerId, name, text, out _);
        if (codeResult != null) ShowStatus(codeResult);   // 코드 결과는 지금처럼 입력창 자리에(보낸 사람만)
    }

    // 원작 「-load는 1라운드 전까지만 가능합니다.」. 같이 하기에선 판 시작 때 호스트가 세이브를 이미 받았으니 대기실에서만 받는다.
    static string HandleLoadCommand(PlayerContext local, string code)
    {
        RoundManager rm = FindFirstObjectByType<RoundManager>();
        if (rm != null && !(rm.CurrentRound <= 1 && rm.PreRoundTimeLeft > 0f)) return "-load는 1라운드 전까지만 가능합니다.";
        if (MatchConfig.Active) return "같이 하기에선 대기실의 「세이브 코드 불러오기」로 입력하세요.";
        SaveCodeService.Load(PlayerDisplayName.RawNickname(local.PlayerId), code, out string message, data => local.PersistentSave?.ReplaceData(data));
        return message;
    }

    // 싱글에서 채팅 줄 앞에 붙일 이름 — 같이 하기에서 쓰던 닉네임 기억값, 없으면 「나」.
    static string LocalName()
    {
        string nick = NetPlayer.LoadNickname();
        return string.IsNullOrWhiteSpace(nick) ? "나" : nick;
    }

    // MP: 입력창·멀티 호스트(PlayerChat)가 같이 쓰는 코드 판정 — 코드가 아니면 null(일반 말에
    //     「인식할 수 없는 코드입니다」가 뜨지 않게, PM 09-26). 결과 문구가 있으면 코드로 인식된 것이다.
    public string TryExecuteCode(int playerId, string text)
    {
        if (chatUnlockManager != null)
        {
            chatUnlockManager.TryUnlockByPhrase(playerId, text, out string chatMessage);
            if (chatMessage != null) return chatMessage;
        }

        if (hiddenCombineManager != null)
        {
            hiddenCombineManager.TryUnlockByPhrase(playerId, text, out string hiddenMessage);
            if (hiddenMessage != null) return hiddenMessage;
        }

        // 10-06 히든·불멸·초월·영원함 조합은 조합 버튼이 아니라 여기 채팅 코드로만(CombineSystem.IsChatOnly).
        CombineSystem combine = FindFirstObjectByType<CombineSystem>();
        if (combine != null)
        {
            string combineMessage = combine.TryCombineByChat(playerId, text);
            if (combineMessage != null) return combineMessage;
        }

        return null;
    }

    void ShowStatus(string message)
    {
        statusMessage = message;
        statusHideTime = Time.unscaledTime + StatusDisplaySeconds;
    }

    // IMGUI 스타일(한 번만 만든다): 어두운 바탕 + 금테 한 줄, 크림색 글자. 크기는 1080 기준이고 화면 높이에 비례한다(1366×768에선 ×0.71).
    static Texture2D Solid(Color c) { var t = new Texture2D(1, 1, TextureFormat.RGBA32, false); t.SetPixel(0, 0, c); t.Apply(); return t; }

    void EnsureStyles()
    {
        if (boxStyle != null) return;
        boxStyle = new GUIStyle { normal = { background = Solid(new Color(0.02f, 0.02f, 0.03f, 0.88f)) } };
        Texture2D wc3Input = UiSkin.Wc3Texture("chat_input");
        if (wc3Input != null) { wc3Chat = true; boxStyle = new GUIStyle { normal = { background = wc3Input }, border = new RectOffset(40, 20, 20, 20) }; }   // 워크3풍: 돌 홈 + 금테(왼쪽 78px에 [전체] 자리)
        fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { background = Solid(new Color(0.06f, 0.06f, 0.08f, UiSkin.Wc3Has("chat_input") ? 0f : 0.95f)), textColor = new Color(1f, 0.97f, 0.88f) },   // 워크3풍은 틀 그림이 바탕이라 입력칸은 투명
            focused = { background = Solid(new Color(0.06f, 0.06f, 0.08f, UiSkin.Wc3Has("chat_input") ? 0f : 0.95f)), textColor = Color.white },
            hover = { background = Solid(new Color(0.06f, 0.06f, 0.08f, UiSkin.Wc3Has("chat_input") ? 0f : 0.95f)), textColor = Color.white },
            padding = new RectOffset(10, 8, 4, 4),
        };
        labelStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = new Color(1f, 0.84f, 0.25f) } };
    }

    static void DrawBorder(Rect r, Color c, float t)
    {
        Color old = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
        if (isOpen)
        {
            EnsureStyles();
            Rect boxRect = ComputeRect();
            GUI.Box(boxRect, GUIContent.none, boxStyle);
            if (!wc3Chat) DrawBorder(boxRect, new Color(0.79f, 0.64f, 0.29f, 1f), Mathf.Max(1f, 2f * scale));
            labelStyle.fontSize = fieldStyle.fontSize = Mathf.RoundToInt(24f * scale);
            float labelW = 78f * scale;
            GUI.Label(new Rect(boxRect.x + 10f * scale, boxRect.y, labelW, boxRect.height), "[전체]", labelStyle);
            GUI.SetNextControlName(TextFieldControlName);
            inputText = GUI.TextField(new Rect(boxRect.x + labelW + 10f * scale, boxRect.y + 3f * scale, boxRect.width - labelW - 16f * scale, boxRect.height - 6f * scale), inputText, 200, fieldStyle);
            GUI.FocusControl(TextFieldControlName);
            return;
        }

        if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusHideTime)
        {
            EnsureStyles();
            Rect boxRect = ComputeRect();
            labelStyle.fontSize = Mathf.RoundToInt(24f * scale);
            float alpha = Mathf.Clamp01((statusHideTime - Time.unscaledTime) / 1.2f);
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f * alpha);
            GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width * 1.6f, boxRect.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            labelStyle.normal.textColor = new Color(1f, 0.97f, 0.88f);
            GUI.Label(new Rect(boxRect.x + 10f * scale, boxRect.y, boxRect.width * 1.6f, boxRect.height), statusMessage, labelStyle);
            GUI.color = old;
            labelStyle.normal.textColor = new Color(1f, 0.84f, 0.25f);
        }
    }

    const string TextFieldControlName = "GameChatBoxInput";

    static Rect ComputeRect()
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
        float bottomHudTop = Screen.height * (1f - BottomHudHeightFraction);
        float y = bottomHudTop - BoxHeight * scale - BottomGap;
        return new Rect(LeftMargin * scale + 66f * scale, y, BoxWidth * scale, BoxHeight * scale);   // 영웅 단추 열(왼쪽 끝)과 안 겹치게 알림 줄과 같은 들여쓰기
    }
}
