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

    const float BottomHudHeightFraction = 0.22f;
    const float BoxWidth = 320f;
    const float BoxHeight = 28f;
    const float BottomGap = 8f;
    const float LeftMargin = 10f;

    // 제출 뒤 결과를 잠깐 보여준다 — 원작 채팅창도 보낸 말이 잠깐 남았다 사라진다.
    const float StatusDisplaySeconds = 4f;

    bool isOpen;
    string inputText = "";
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
        Close();

        if (local == null || string.IsNullOrWhiteSpace(text)) return;

        if (chatUnlockManager != null)
        {
            chatUnlockManager.TryUnlockByPhrase(local.PlayerId, text, out string chatMessage);
            if (chatMessage != null) { ShowStatus(chatMessage); return; }
        }

        if (hiddenCombineManager != null)
        {
            hiddenCombineManager.TryUnlockByPhrase(local.PlayerId, text, out string hiddenMessage);
            if (hiddenMessage != null) { ShowStatus(hiddenMessage); return; }
        }

        ShowStatus("인식할 수 없는 코드입니다.");
    }

    void ShowStatus(string message)
    {
        statusMessage = message;
        statusHideTime = Time.unscaledTime + StatusDisplaySeconds;
    }

    void OnGUI()
    {
        if (isOpen)
        {
            Rect boxRect = ComputeRect();
            GUILayout.BeginArea(boxRect);
            GUILayout.BeginHorizontal();
            GUILayout.Label("코드:", GUILayout.Width(40));
            GUI.SetNextControlName(TextFieldControlName);
            inputText = GUILayout.TextField(inputText, GUILayout.Width(BoxWidth - 50f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUI.FocusControl(TextFieldControlName);
            return;
        }

        if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusHideTime)
        {
            Rect boxRect = ComputeRect();
            GUILayout.BeginArea(boxRect);
            GUILayout.Label(statusMessage);
            GUILayout.EndArea();
        }
    }

    const string TextFieldControlName = "GameChatBoxInput";

    static Rect ComputeRect()
    {
        float bottomHudTop = Screen.height * (1f - BottomHudHeightFraction);
        float y = bottomHudTop - BoxHeight - BottomGap;
        return new Rect(LeftMargin, y, BoxWidth, BoxHeight);
    }
}
