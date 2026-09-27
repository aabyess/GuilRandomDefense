/// <summary>
/// 알림에 쓰는 플레이어 이름 — 원작 GetPlayerName(워크3 닉네임) 자리. 같이 하기면 그 슬롯의 대기실 닉네임,
/// 혼자 하기면 이 PC에 저장된 닉네임, 없으면 「플레이어 N」.
/// </summary>
public static class PlayerDisplayName
{
    public static string Of(int slot)
    {
        if (MatchConfig.Active)
            foreach (NetPlayer player in NetPlayer.All)
                if (player != null && player.Slot == slot) return player.DisplayName;
        string saved = slot == LocalPlayer.LocalPlayerId ? NetPlayer.LoadNickname() : "";
        return string.IsNullOrWhiteSpace(saved) ? $"플레이어 {slot + 1}" : saved;
    }
}
