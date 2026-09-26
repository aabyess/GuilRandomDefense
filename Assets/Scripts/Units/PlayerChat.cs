using UnityEngine;

/// <summary>
/// 플레이어끼리 채팅(사장님 「채팅 기능도 있나? 있으면 추가해줘」, 2026-09-26). 원작(워크3)처럼 **채팅 한 줄이 곧 코드 입력**이다 —
/// 모두가 그 말을 보고, 코드면 따로 실행된다. 판정은 호스트(싱글은 자기 자신)가 한다:
///   1. 코드면 GameChatBox.TryExecuteCode로 실행 → 결과 문구는 보낸 사람에게만
///   2. 코드든 아니든 「닉네임: 내용」 한 줄을 전원(싱글은 나)에게 — 알림 자리(PlayerNotification), 10초
/// 🔴 알림은 richText라 입력의 '&lt;'를 비슷한 글자로 바꿔 &lt;color&gt;·&lt;size=500&gt; 끼워 넣기를 막는다. 100자, 0.5초에 한 줄.
/// </summary>
public static class PlayerChat
{
    public const int MaxLength = 100;
    public const float MinInterval = 0.5f;
    public const float LineSeconds = 10f;

    static readonly float[] lastAccepted = { -99f, -99f, -99f, -99f };
    static float lastLocalSend = -99f;

    /// <summary>한 줄로 · 앞뒤 공백 제거 · '&lt;' 무력화 · 100자.</summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string text = raw.Replace("\r", " ").Replace("\n", " ").Trim().Replace('<', '‹');
        return text.Length > MaxLength ? text.Substring(0, MaxLength) : text;
    }

    public static string FormatLine(int slot, string name, string text) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerColors.Get(slot))}>{Sanitize(name)}</color>: {text}";

    /// <summary>보내는 쪽(이 PC) 연타 막기 — 요청을 아예 안 보낸다. 판정 쪽도 따로 막는다(조작된 클라 대비).</summary>
    public static bool AllowLocalSend()
    {
        if (Time.unscaledTime - lastLocalSend < MinInterval) return false;
        lastLocalSend = Time.unscaledTime;
        return true;
    }

    /// <summary>
    /// 판정 쪽(호스트·싱글): 코드 시도 → 채팅 한 줄 뿌리기. 돌려주는 값은 코드 결과 문구(코드가 아니면 null) —
    /// 보낸 사람이 이 PC면 호출부가 자기 자리에 보여 주고, 원격이면 NetCommands가 그 사람에게 알림으로 보낸다.
    /// </summary>
    public static string HandleOnAuthority(int senderSlot, string senderName, string rawText, out bool accepted)
    {
        accepted = false;
        string text = Sanitize(rawText);
        if (text.Length == 0) return null;

        int index = Mathf.Clamp(senderSlot, 0, lastAccepted.Length - 1);
        if (Time.unscaledTime - lastAccepted[index] < MinInterval) return null;
        lastAccepted[index] = Time.unscaledTime;
        accepted = true;

        GameChatBox box = Object.FindFirstObjectByType<GameChatBox>();   // 대기실엔 없다 — 그땐 말만
        string codeResult = box != null ? box.TryExecuteCode(senderSlot, text) : null;

        string line = FormatLine(senderSlot, senderName, text);
        if (MatchConfig.Active)
        {
            foreach (NetPlayer player in NetPlayer.All)
                if (player != null) PlayerNotification.Show(player.Slot, line, LineSeconds);
        }
        else
        {
            PlayerNotification.Show(LocalPlayer.LocalPlayerId, line, LineSeconds);
        }

        return codeResult;
    }
}
