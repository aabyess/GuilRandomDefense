/// <summary>
/// 알림에 쓰는 플레이어 이름 — 원작 GetPlayerName(워크3 닉네임) 자리. 같이 하기면 그 슬롯의 대기실 닉네임,
/// 혼자 하기면 이 PC에 저장된 닉네임, 없으면 「플레이어 N」.
/// </summary>
public static class PlayerDisplayName
{
    // 원작 Trig_ch4(j:5156-5174): 1라운드 시작 때 누적 클리어 횟수(Load_PlayCount)가 문턱을 넘은 가장 높은 칭호를 닉네임 앞에 붙인다.
    // 조건이 각각 독립이라 뒤 것이 덮는다 = 가장 높은 단계. D.는 정확히 300회일 때만(301 이상은 [불멸의]).
    static readonly (int over, string hex, string title)[] ClearTitles =
    {
        (0, "FF7F50", "견습선원"), (5, "20B2AA", "신참해적"), (15, "FFD700", "루키"), (25, "4169E1", "초신성"),
        (35, "00FF7F", "간부"), (50, "1FBF00", "대해적"), (70, "7B68EE", "칠무해"), (90, "FF0000", "전설적인"),
        (125, "C15AF4", "사황"), (150, "80FFFF", "패왕"), (200, "4B4B4B", "오로성"), (250, "BC4346", "불멸의"),
    };

    public static string ClearTitleOf(int clearCount)
    {
        if (clearCount == 300) return "<color=#A4D1FF>[D.]</color>";
        string result = "";
        foreach (var t in ClearTitles)
            if (clearCount > t.over) result = $"<color=#{t.hex}>[{t.title}]</color>";
        return result;
    }

    public static string Of(int slot)
    {
        PlayerContext context = PlayerContext.GetOccupied(slot);
        PlayerSaveData save = context != null && context.PersistentSave != null ? context.PersistentSave.Data : null;
        string title = ClearTitleOf(save != null ? save.cumulativeClearCount : 0);
        // 워크3 「|r」는 색을 기본(흰색)으로 되돌리므로, 칭호 뒤 닉네임은 바깥 색(주황 등)이 아니라 흰색이다(원작 그대로).
        return title.Length == 0 ? BareName(slot) : title + $"<color=#FFFFFF>{BareName(slot)}</color>";
    }

    /// <summary>세이브 코드 열쇠로 쓰는 원본 닉네임 — 칭호·「플레이어 N」 대체 이름 없이, 비어 있으면 빈 문자열.</summary>
    public static string RawNickname(int slot)
    {
        if (MatchConfig.Active)
        {
            foreach (NetPlayer player in NetPlayer.All)
                if (player != null && player.Slot == slot) return NetPlayer.SanitizeNickname(player.Nickname.ToString());
            return "";
        }
        return slot == LocalPlayer.LocalPlayerId ? NetPlayer.SanitizeNickname(NetPlayer.LoadNickname()) : "";
    }

    static string BareName(int slot)
    {
        if (MatchConfig.Active)
            foreach (NetPlayer player in NetPlayer.All)
                if (player != null && player.Slot == slot) return player.DisplayName;
        string saved = slot == LocalPlayer.LocalPlayerId ? NetPlayer.LoadNickname() : "";
        return string.IsNullOrWhiteSpace(saved) ? $"플레이어 {slot + 1}" : saved;
    }
}
