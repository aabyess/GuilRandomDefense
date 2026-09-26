using Fusion;
using UnityEngine;

/// <summary>
/// 클라 → 호스트 「요청」의 양 끝(설계 §5-3). 선택은 로컬이고, 바꾸는 것은 전부 여기를 거쳐 호스트가 한다.
///   클라:   Request* — 겉모습에서 거울(NetEntity)의 NetworkId를 찾아 내 NetPlayer의 RPC로 보낸다.
///   호스트: Execute* — 받은 NetworkId로 실물을 찾고, **요청자 슬롯 == 실물 소유자**를 검사한 뒤
///           싱글에서 쓰는 그 함수를 부른다(UnitMover.MoveToGroundPoint 등).
/// </summary>
public static class NetCommands
{
    public static void RequestMove(Component visualPart, Vector3 groundPoint)
    {
        NetEntity entity = visualPart != null ? visualPart.GetComponentInParent<NetEntity>() : null;
        if (entity == null || entity.Object == null || !entity.Object.IsValid)
        {
            Debug.Log($"[MP] 이동 요청: {visualPart?.name}의 거울을 못 찾았습니다.");
            return;
        }

        if (NetPlayer.Local == null)
        {
            Debug.Log("[MP] 이동 요청: 내 NetPlayer가 아직 없습니다.");
            return;
        }

        NetPlayer.Local.RPC_Move(entity.Object.Id, groundPoint);
    }

    /// <summary>호스트: NetPlayer.RPC_Move가 부른다. sender는 요청을 보낸 접속자의 NetPlayer다.</summary>
    public static void ExecuteMove(NetPlayer sender, NetworkId target, Vector3 groundPoint)
    {
        if (!TryGetOwnedReal(sender, target, "이동", out GameObject real)) return;

        if (!real.TryGetComponent(out UnitMover mover))
        {
            Debug.Log($"[MP] 이동 거절: {real.name}에는 UnitMover가 없습니다.");
            return;
        }

        mover.MoveToGroundPoint(groundPoint, $"슬롯{sender.Slot} 요청");
        if (movesLogged++ < 20) Debug.Log($"[MP] 이동 요청 수행: 슬롯 {sender.Slot} → {real.name} → ({groundPoint.x:F0},{groundPoint.z:F0})");
    }

    static int movesLogged;

    static bool TryGetOwnedReal(NetPlayer sender, NetworkId target, string what, out GameObject real)
    {
        real = null;
        if (sender == null || sender.Runner == null || !sender.Runner.IsServer) return false;

        if (!sender.Runner.TryFindObject(target, out NetworkObject obj) || !obj.TryGetComponent(out NetEntity entity) || entity.Real == null)
        {
            // 요청이 오는 사이 실물이 사라졌다(조합·소모·사망) — 흔한 일이라 조용히 넘긴다.
            return false;
        }

        int owner = entity.Real.TryGetComponent(out OwnedByPlayer ownedBy) ? ownedBy.OwnerId : -1;
        if (owner != sender.Slot)
        {
            Debug.LogWarning($"[MP] {what} 거절: 슬롯 {sender.Slot}이 슬롯 {owner}의 {entity.Real.name}을(를) 움직이려 했습니다.");
            return false;
        }

        real = entity.Real;
        return true;
    }
}
