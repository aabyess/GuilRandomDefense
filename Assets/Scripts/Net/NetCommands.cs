using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>클라 → 호스트로 보내는 유닛 명령 종류(UnitCommands의 함수와 1:1). 직렬화되니 맨 뒤에만 추가.</summary>
public enum NetUnitCommand : byte
{
    Stop = 0,
    Hold = 1,
    AttackTarget = 2,
    AttackMove = 3,
    Gather = 4,
    SendToPen = 5,
}

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
    static int commandsLogged;

    /// <summary>
    /// 클라: UnitCommands.*가 부른다. 선택한 내 유닛마다 요청을 하나씩 보낸다(Fusion RPC에 배열을 싣지 않고 유닛당 1개).
    /// 모으기는 뜻이 「기준 유닛 자리로 같은 이름 전부」라 기준 유닛 하나만 보낸다.
    /// 돌려주는 값은 보낸 요청 수 — 싱글에서 「움직인 수」를 돌려주던 자리라 로그 문구만 조금 달라진다.
    /// </summary>
    public static int RequestUnitCommand(NetUnitCommand command, IReadOnlyList<Selectable> selection, EnemyDummy enemy = null, Vector3 point = default)
    {
        if (NetPlayer.Local == null || selection == null) return 0;

        NetworkId enemyId = default;
        if (command == NetUnitCommand.AttackTarget)
        {
            NetEntity enemyEntity = enemy != null ? enemy.GetComponentInParent<NetEntity>() : null;
            if (enemyEntity == null || enemyEntity.Object == null || !enemyEntity.Object.IsValid) return 0;
            enemyId = enemyEntity.Object.Id;
        }

        int sent = 0;
        foreach (Selectable selected in selection)
        {
            if (selected == null) continue;
            NetEntity entity = selected.GetComponentInParent<NetEntity>();
            if (entity == null || entity.Object == null || !entity.Object.IsValid) continue;
            if (entity.Owner != LocalPlayer.LocalPlayerId) continue;

            NetPlayer.Local.RPC_UnitCommand(entity.Object.Id, (byte)command, enemyId, point);
            sent++;
            if (command == NetUnitCommand.Gather) break;
        }
        return sent;
    }

    /// <summary>호스트: NetPlayer.RPC_UnitCommand가 부른다. 소유자 검사 뒤 싱글과 같은 UnitCommands 함수를 그 유닛 하나로 부른다.</summary>
    public static void ExecuteUnitCommand(NetPlayer sender, NetworkId unit, NetUnitCommand command, NetworkId enemy, Vector3 point)
    {
        if (!TryGetOwnedReal(sender, unit, command.ToString(), out GameObject real)) return;
        if (!real.TryGetComponent(out Selectable selectable)) return;

        Selectable[] one = { selectable };
        int done = 0;
        switch (command)
        {
            case NetUnitCommand.Stop: done = UnitCommands.Stop(one); break;
            case NetUnitCommand.Hold: done = UnitCommands.Hold(one); break;
            case NetUnitCommand.AttackMove: done = UnitCommands.AttackMove(one, point); break;
            case NetUnitCommand.Gather: done = UnitCommands.Gather(one); break;
            case NetUnitCommand.SendToPen: done = UnitCommands.SendToPen(one); break;
            case NetUnitCommand.AttackTarget:
                EnemyDummy target = null;
                if (sender.Runner.TryFindObject(enemy, out NetworkObject enemyObj) && enemyObj.TryGetComponent(out NetEntity enemyEntity) && enemyEntity.Real != null)
                    enemyEntity.Real.TryGetComponent(out target);
                if (target == null) return;   // 요청이 오는 사이 죽었다
                done = UnitCommands.AttackTarget(one, target);
                break;
        }

        if (commandsLogged++ < 30) Debug.Log($"[MP] 명령 요청 수행: 슬롯 {sender.Slot} {command} → {real.name} (결과 {done})");
    }

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
