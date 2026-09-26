using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>GameHud 버튼 중 「선택한 유닛 하나」에 거는 것. 직렬화되니 맨 뒤에만 추가.</summary>
public enum NetHudAction : byte
{
    Gamble = 0,
    Sell = 1,
    Trait = 2,
    Reroll = 3,
}

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

    // ───────────── 조합 · 상점 · HUD 유닛 버튼(2단계 ①) ─────────────

    public static void RequestCombine(CombineSystem system, CombineRecipe recipe, Vector3? casterPosition)
    {
        int index = system != null ? system.IndexOfRecipe(recipe) : -1;
        if (index < 0 || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_Combine((short)index, casterPosition.HasValue, casterPosition ?? default);
    }

    public static void ExecuteCombine(NetPlayer sender, int recipeIndex, bool hasCaster, Vector3 caster)
    {
        CombineSystem system = Object.FindFirstObjectByType<CombineSystem>();
        CombineRecipe recipe = system != null ? system.RecipeAt(recipeIndex) : null;
        if (recipe == null) return;

        // 조합기는 씬에 하나 — 이 요청 동안만 「조합하는 사람 = 요청자」로 세운다(CombineSystem.ActingPlayerOverride).
        CombineSystem.ActingPlayerOverride = sender.Slot;
        bool ok;
        try { ok = system.TryCombine(recipe, hasCaster ? caster : (Vector3?)null); }
        finally { CombineSystem.ActingPlayerOverride = -1; }

        if (!ok) PlayerNotification.Show(sender.Slot, "지금은 조합할 수 없습니다.");
        if (commandsLogged++ < 30) Debug.Log($"[MP] 조합 요청 수행: 슬롯 {sender.Slot} 조합식 {recipeIndex} → {(ok ? "성공" : "실패")}");
    }

    public static bool RequestShopUse(ILaneShop shop, int slot, LaneShopTarget target)
    {
        int shopId = NetShops.IdOf(shop);
        if (shopId < 0 || NetPlayer.Local == null) return false;

        NetworkId targetId = default;
        byte kind = 0;
        if (target.unit != null)
        {
            NetEntity entity = target.unit.GetComponentInParent<NetEntity>();
            if (entity == null || entity.Object == null || !entity.Object.IsValid) return false;
            targetId = entity.Object.Id;
            kind = 2;
        }
        else if (target.point != default) kind = 1;

        NetPlayer.Local.RPC_ShopUse((short)shopId, (byte)slot, kind, target.point, targetId);
        return true;
    }

    public static void ExecuteShopUse(NetPlayer sender, int shopId, int slot, byte kind, Vector3 point, NetworkId targetId)
    {
        ILaneShop shop = NetShops.Get(shopId);
        if (shop == null) return;

        int owner = ((Component)shop).TryGetComponent(out OwnedByPlayer ownedBy) ? ownedBy.OwnerId : -1;
        if (owner != sender.Slot)
        {
            Debug.LogWarning($"[MP] 상점 거절: 슬롯 {sender.Slot}이 슬롯 {owner}의 {((Component)shop).name}을(를) 쓰려 했습니다.");
            return;
        }

        LaneShopTarget target = default;
        if (kind == 1) target = LaneShopTarget.AtPoint(point);
        else if (kind == 2)
        {
            if (!sender.Runner.TryFindObject(targetId, out NetworkObject obj) || !obj.TryGetComponent(out NetEntity entity) || entity.Real == null)
            {
                PlayerNotification.Show(sender.Slot, "대상을 찾을 수 없습니다.");
                return;
            }
            target = LaneShopTarget.OnUnit(entity.Real);
        }

        bool used = shop.TryUse(slot, target, out string reason);
        if (!used) PlayerNotification.Show(sender.Slot, reason ?? "지금은 사용할 수 없습니다.");
        if (commandsLogged++ < 30) Debug.Log($"[MP] 상점 요청 수행: 슬롯 {sender.Slot} {((Component)shop).name} 칸 {slot} → {(used ? "성공" : "실패: " + reason)}");
    }

    public static void RequestHudUnitAction(NetHudAction action, Selectable unit, int argument)
    {
        NetEntity entity = unit != null ? unit.GetComponentInParent<NetEntity>() : null;
        if (entity == null || entity.Object == null || !entity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_HudUnitAction(entity.Object.Id, (byte)action, (byte)argument);
    }

    public static void ExecuteHudUnitAction(NetPlayer sender, NetworkId unit, NetHudAction action, int argument)
    {
        if (!TryGetOwnedReal(sender, unit, action.ToString(), out GameObject real)) return;
        if (!real.TryGetComponent(out Selectable selectable)) return;

        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return;

        switch (action)
        {
            case NetHudAction.Gamble: hud.ExecuteGambleOn(selectable, argument); break;
            case NetHudAction.Sell: hud.ExecuteSellOn(selectable); break;
            case NetHudAction.Trait: hud.ExecuteTraitOn(selectable); break;
            case NetHudAction.Reroll:
                if (real.TryGetComponent(out UniqueRerollAbility reroll))
                {
                    reroll.TryCast(out string message);
                    if (message != null) PlayerNotification.Show(sender.Slot, message);
                }
                break;
        }
        if (commandsLogged++ < 30) Debug.Log($"[MP] 유닛 버튼 요청 수행: 슬롯 {sender.Slot} {action}({argument}) → {real.name}");
    }

    public static void RequestTraitTarget(UnitTraitData trait, UnitIdentity target)
    {
        int traitIndex = NetLauncher.Catalog != null ? NetLauncher.Catalog.IndexOf(trait) : -1;
        NetEntity entity = target != null ? target.GetComponentInParent<NetEntity>() : null;
        if (traitIndex < 0 || entity == null || entity.Object == null || !entity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_TraitTarget((short)traitIndex, entity.Object.Id);
    }

    /// <summary>호스트: 대상 지정 특성(로빈 등). 특성 포인트는 요청자 슬롯 것, 대상은 요청자 소유 유닛만.</summary>
    public static void ExecuteTraitTarget(NetPlayer sender, int traitIndex, NetworkId target)
    {
        NetCatalog catalog = NetLauncher.Catalog;
        UnitTraitData trait = catalog != null && traitIndex >= 0 && traitIndex < catalog.traits.Count ? catalog.traits[traitIndex] : null;
        if (trait == null || !trait.targetsOtherUnit) return;
        if (!TryGetOwnedReal(sender, target, "특성 대상", out GameObject real) || !real.TryGetComponent(out UnitIdentity identity)) return;

        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return;
        hud.ExecuteTraitTargetOn(trait, PlayerContext.Get(sender.Slot), identity, sender.Slot);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 특성 대상 요청 수행: 슬롯 {sender.Slot} {trait.name} → {real.name}");
    }

    /// <summary>클라: 채팅 한 줄을 호스트로(대기실·게임 모두). 연타는 보내는 쪽(PlayerChat.AllowLocalSend)에서 먼저 막는다.</summary>
    public static void RequestChat(string text)
    {
        string clean = PlayerChat.Sanitize(text);
        if (NetPlayer.Local == null || clean.Length == 0) return;
        NetPlayer.Local.RPC_Chat(clean);
    }

    /// <summary>호스트: 코드 시도(결과는 보낸 사람에게) + 채팅 줄을 전원에게. 이름은 호스트가 아는 닉네임(클라가 이름을 못 속인다).</summary>
    public static void ExecuteChat(NetPlayer sender, string text)
    {
        string codeResult = PlayerChat.HandleOnAuthority(sender.Slot, sender.DisplayName, text, out bool accepted);
        if (codeResult != null) PlayerNotification.Show(sender.Slot, codeResult);
        if (accepted && commandsLogged++ < 30) Debug.Log($"[MP] 채팅: 슬롯 {sender.Slot} 「{PlayerChat.Sanitize(text)}」{(codeResult != null ? " → 코드: " + codeResult : "")}");
    }

    // ───────────── 창고 · 항법(2단계 ③) ─────────────

    public static bool RequestWarehouse(Selectable unit)
    {
        NetEntity entity = unit != null ? unit.GetComponentInParent<NetEntity>() : null;
        if (entity == null || entity.Object == null || !entity.Object.IsValid || NetPlayer.Local == null) return false;
        if (entity.EntityKind != NetEntityKind.Unit || entity.Owner != LocalPlayer.LocalPlayerId) return false;
        NetPlayer.Local.RPC_Warehouse(entity.Object.Id);
        return true;
    }

    /// <summary>호스트: 요청자 슬롯의 창고·레인으로(WarehouseController는 씬에 하나라 Local 창고를 잡는다 — 여기선 안 쓴다).</summary>
    public static void ExecuteWarehouse(NetPlayer sender, NetworkId unit)
    {
        if (!TryGetOwnedReal(sender, unit, "창고", out GameObject real)) return;
        PlayerContext context = PlayerContext.Get(sender.Slot);
        Warehouse warehouse = context != null ? context.Warehouse : null;
        if (warehouse == null) return;

        bool ok;
        string what;
        if (warehouse.Contains(real))
        {
            LaneMarker lane = LaneMarker.Get(sender.Slot);
            ok = warehouse.Retrieve(real, lane != null ? lane.transform.position : real.transform.position);
            what = "회수";
        }
        else
        {
            ok = warehouse.Store(real);
            what = "보관";
        }

        if (ok) PlayerNotification.Show(sender.Slot, $"창고: {what} (보관 중 {warehouse.Stored.Count}기)");
        if (commandsLogged++ < 30) Debug.Log($"[MP] 창고 요청 수행: 슬롯 {sender.Slot} {real.name} {what} → {(ok ? "성공" : "실패")}");
    }

    public static void RequestNavigation(NavigationChoice choice)
    {
        if (NetPlayer.Local != null) NetPlayer.Local.RPC_Navigation((byte)choice);
    }

    public static void ExecuteNavigation(NetPlayer sender, NavigationChoice choice)
    {
        NavigationState state = PlayerContext.Get(sender.Slot)?.NavigationState;
        bool ok = state != null && state.TrySelect(choice);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 항법 요청 수행: 슬롯 {sender.Slot} {choice} → {(ok ? "성공" : "실패(이미 고름)")}");
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
