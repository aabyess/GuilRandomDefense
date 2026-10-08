using UnityEngine;

/// <summary>
/// 동맹 「유닛 공유」(사장님 10-08, 워크3식): 내가 B에게 체크하면 B가 <b>내</b> 유닛을 조종할 수 있다(소유는 그대로 — 이동·공격·정지·스킬만, 판매·조합·창고·아이템·강화는 원래 주인만).
/// 상태는 NetGameState.ShareMasks[주인 슬롯] 비트 = 조종을 열어 준 슬롯. 호스트가 판정한다(NetCommands.TryGetOwnedReal) — 클라 UI 막기는 보기 좋게 하려는 것뿐.
/// 혼자 하기에선 NetGameState가 없어 늘 「내 것만」.
/// </summary>
public static class AllianceShare
{
    public const string ViewOnlyMessage = "다른 플레이어의 유닛입니다.";
    public const int MaxSlots = 4;

    /// <summary>주인(ownerSlot)이 controllerSlot에게 유닛 조종을 열어 줬나.</summary>
    public static bool IsShared(int ownerSlot, int controllerSlot)
    {
        NetGameState gs = NetGameState.Instance;
        if (gs == null || ownerSlot < 0 || ownerSlot >= MaxSlots || controllerSlot < 0 || controllerSlot >= MaxSlots || ownerSlot == controllerSlot) return false;
        return (gs.ShareMasks.Get(ownerSlot) & (1 << controllerSlot)) != 0;
    }

    /// <summary>이 슬롯이 그 주인의 유닛을 조종할 수 있나(주인 본인이거나 공유 받음).</summary>
    public static bool CanControl(int ownerSlot, int controllerSlot) => ownerSlot == controllerSlot || IsShared(ownerSlot, controllerSlot);

    /// <summary>이 PC의 플레이어가 그 오브젝트를 조종할 수 있나. 소유자 표시가 없으면 true(옛 규칙), 공유는 <b>유닛</b>(UnitIdentity)에만 적용된다 — 위습·건물은 주인만.</summary>
    public static bool CanControlLocal(GameObject go)
    {
        if (go == null) return false;
        if (!go.TryGetComponent(out OwnedByPlayer owner)) return true;
        if (owner.OwnerId == LocalPlayer.LocalPlayerId) return true;
        return go.TryGetComponent(out UnitIdentity _) && IsShared(owner.OwnerId, LocalPlayer.LocalPlayerId);
    }

    /// <summary>남의 유닛인데 보기만 되는 대상인가(UnitIdentity가 있고 내가 조종 못 함).</summary>
    public static bool IsViewOnly(GameObject go) =>
        go != null && go.TryGetComponent(out OwnedByPlayer owner) && owner.OwnerId != LocalPlayer.LocalPlayerId && go.TryGetComponent(out UnitIdentity _) && !IsShared(owner.OwnerId, LocalPlayer.LocalPlayerId);

    /// <summary>내가 이 슬롯에게 열어 줬나(창 체크박스 값).</summary>
    public static bool IsSharingTo(int targetSlot) => IsShared(LocalPlayer.LocalPlayerId, targetSlot);
}
