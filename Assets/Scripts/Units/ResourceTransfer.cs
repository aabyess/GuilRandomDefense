using UnityEngine;

/// <summary>
/// 자원 전송 채팅 명령(S2 2.323 j:48900~48950, CIn·CIq): 다른 플레이어의 유닛을 고른 채
/// 「-골드 N」「-금 N」「-돈 N」「-목재 N」을 치면 그 유닛 주인에게 내 엔/목재를 보낸다.
/// 파싱·표적 고르기는 보내는 PC(선택이 로컬이라), 검사·이동은 호스트가 한다.
/// </summary>
public static class ResourceTransfer
{
    public enum Kind : byte { Gold = 0, Wood = 1 }

    static readonly string[] GoldWords = { "-골드", "-금", "-돈" };
    static readonly string[] WoodWords = { "-목재" };

    /// <summary>명령이 아니면 false. 명령이면 true — amount는 숫자가 아니면 0(오류 문구는 호출부가).</summary>
    public static bool TryParse(string text, out Kind kind, out int amount)
    {
        if (!S2Features.Enabled) { kind = Kind.Gold; amount = 0; return false; }
        kind = Kind.Gold;
        amount = 0;
        string t = (text ?? "").Trim();
        string matched = null;
        foreach (string w in GoldWords) if (t.StartsWith(w)) { matched = w; kind = Kind.Gold; break; }
        if (matched == null)
            foreach (string w in WoodWords) if (t.StartsWith(w)) { matched = w; kind = Kind.Wood; break; }
        if (matched == null) return false;
        // 「-금」이 「-금액…」 같은 다른 말의 앞부분이 아니게: 뒤는 비었거나 공백/숫자여야 한다.
        string rest = t.Substring(matched.Length);
        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0]) && !char.IsDigit(rest[0])) return false;
        int.TryParse(rest.Trim(), out amount);
        return true;
    }

    /// <summary>이 PC에서 고른 유닛의 주인 슬롯. 없으면 -1.</summary>
    public static int SelectedOwner()
    {
        SelectionManager sm = Object.FindFirstObjectByType<SelectionManager>();
        if (sm == null || sm.Selected.Count == 0) return -1;
        Selectable s = sm.Selected[0];
        if (s == null) return -1;
        NetEntity entity = s.GetComponentInParent<NetEntity>();
        if (entity != null) return entity.Owner;
        return s.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : -1;
    }

    /// <summary>판정 쪽(호스트·싱글). 오류 문구를 돌려주고, 성공이면 null — 양쪽에 알림을 띄운다.</summary>
    public static string ExecuteOnAuthority(int from, int to, Kind kind, int amount)
    {
        if (!S2Features.Enabled) { return "S2 기능이 꺼져 있습니다."; }
        if (amount <= 0) return "전송량은 1 이상이어야 합니다.";
        PlayerContext src = PlayerContext.GetOccupied(from);
        PlayerContext dst = to >= 0 ? PlayerContext.GetOccupied(to) : null;
        if (src == null || dst == null || to == from || dst.IsDead) return "자원을 받을 다른 플레이어의 유닛을 선택하세요.";

        string label = kind == Kind.Gold ? "<color=#FFD700>엔</color>" : "<color=#7AD823>목재</color>";
        if (kind == Kind.Gold)
        {
            if (src.GoldWallet == null || dst.GoldWallet == null || !src.GoldWallet.TrySpend(amount)) return "엔이 부족합니다.";
            dst.GoldWallet.Add(amount);
        }
        else
        {
            if (src.ResourceWallet == null || dst.ResourceWallet == null || !src.ResourceWallet.TrySpend(ResourceType.Wood, amount)) return "목재가 부족합니다.";
            dst.ResourceWallet.Add(ResourceType.Wood, amount);
        }

        string fromName = PlayerDisplayName.Of(from);
        string toName = PlayerDisplayName.Of(to);
        PlayerNotification.Show(from, $"<color=#04FFC4>[자원 전송]</color> {toName} 님에게 {amount:N0} {label} 전송", 5f);
        PlayerNotification.Show(to, $"<color=#04FFC4>[자원 전송]</color> {fromName} 님에게서 {amount:N0} {label} 획득", 5f);
        return null;
    }
}
