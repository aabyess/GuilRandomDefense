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
    Yoonseo = 4,   // 초월 노태현 「최윤서 강화」(10-06)
    CastActive = 5,   // 액티브(누르는) 스킬 시전 — 초월 최상호 「바지사장」(10-06)
    Talent = 6,    // 초월 박민수 「재능투자」(10-06) — argument = 투자처(0 공격력·1 공속·2 방깎·3 스턴)
    Bomb = 8,   // 초월 엄태웅 「폭탄제조」(10-06) — 목재 1개 → 사거리 안 밀집 지점 범위 폭탄
    Toto = 10,   // 초월 유재헌 「토토」(10-06) — 엔 1,000 → 33% 금화 3,000엔/목재 1/위습 1 중 하나
    Enhance = 9,   // 영원함 서민성 「강화」(10-06) — 엔 + 위습을 내고 이 유닛 강화 레벨 +1
    GambleBoost = 7,   // 초월 엄태웅 「웅교교주」(10-06) — 엔 10000 → 도박 성공 확률 +4%p(개인 누적, 최대 5회)
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
    Patrol = 6,   // 반복(P, 10-07)
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
        if (!TryGetOwnedReal(sender, target, "이동", out GameObject real, allowShared: true)) return;

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
            bool ownerOnly = command == NetUnitCommand.Gather || command == NetUnitCommand.SendToPen;
            if (ownerOnly ? entity.Owner != LocalPlayer.LocalPlayerId : !AllianceShare.CanControl(entity.Owner, LocalPlayer.LocalPlayerId)) continue;   // 공유 받은 유닛은 이동·공격·정지 등만(호스트가 다시 검사)

            NetPlayer.Local.RPC_UnitCommand(entity.Object.Id, (byte)command, enemyId, point);
            sent++;
            if (command == NetUnitCommand.Gather) break;
        }
        return sent;
    }

    /// <summary>호스트: NetPlayer.RPC_UnitCommand가 부른다. 소유자 검사 뒤 싱글과 같은 UnitCommands 함수를 그 유닛 하나로 부른다.</summary>
    public static void ExecuteUnitCommand(NetPlayer sender, NetworkId unit, NetUnitCommand command, NetworkId enemy, Vector3 point)
    {
        if (!TryGetOwnedReal(sender, unit, command.ToString(), out GameObject real, allowShared: command != NetUnitCommand.Gather && command != NetUnitCommand.SendToPen)) return;
        if (!real.TryGetComponent(out Selectable selectable)) return;

        Selectable[] one = { selectable };
        int done = 0;
        switch (command)
        {
            case NetUnitCommand.Stop: done = UnitCommands.Stop(one); break;
            case NetUnitCommand.Hold: done = UnitCommands.Hold(one); break;
            case NetUnitCommand.AttackMove: done = UnitCommands.AttackMove(one, point); break;
            case NetUnitCommand.Patrol: done = UnitCommands.Patrol(one, point); break;
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

    /// <summary>클라: [조합]을 누른 유닛(caster)과 조합식 번호를 보낸다 — 호스트가 그 유닛 기준으로 검증한다.</summary>
    public static void RequestCombine(CombineSystem system, CombineRecipe recipe, Selectable caster)
    {
        int index = system != null ? system.IndexOfRecipe(recipe) : -1;
        NetEntity entity = caster != null ? caster.GetComponentInParent<NetEntity>() : null;
        if (index < 0 || NetPlayer.Local == null || entity == null || entity.Object == null || !entity.Object.IsValid) return;
        NetPlayer.Local.RPC_Combine((short)index, entity.Object.Id);
    }

    /// <summary>
    /// 호스트: 클라가 보낸 식 번호를 그대로 믿지 않는다(PM 09-26). 조합은 다음을 모두 만족할 때만 —
    ///   ① [조합]을 누른 유닛이 요청자 소유 ② 그 식이 그 유닛의 GetRecipesStartingWith 안 ③ 요청자 기준 CanCombineNow.
    /// 결과 자리는 원작처럼 누른 유닛 자리(호스트 실물 위치)를 넘긴다(CombineSystem이 자리를 어떻게 쓰는지는 그쪽 몫).
    /// </summary>
    public static void ExecuteCombine(NetPlayer sender, int recipeIndex, NetworkId casterId)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        CombineSystem system = Object.FindFirstObjectByType<CombineSystem>();
        CombineRecipe recipe = system != null ? system.RecipeAt(recipeIndex) : null;
        if (recipe == null) { PlayerNotification.ShowFailure(sender.Slot, "지금은 조합할 수 없습니다."); return; }

        if (!TryGetOwnedReal(sender, casterId, "조합", out GameObject caster) || !caster.TryGetComponent(out UnitIdentity identity) || identity.Data == null)
        {
            PlayerNotification.Show(sender.Slot, "조합할 유닛을 찾을 수 없습니다.");
            return;
        }

        // 조합기는 씬에 하나 — 이 요청 동안만 「조합하는 사람 = 요청자」로 세운다(CombineSystem.ActingPlayerOverride).
        CombineSystem.ActingPlayerOverride = sender.Slot;
        bool ok;
        string rejected = null;
        try
        {
            List<CombineRecipe> allowed = system.GetRecipesStartingWith(identity.Data);
            if (allowed == null || !allowed.Contains(recipe)) rejected = "이 유닛으로는 그 조합을 할 수 없습니다.";
            else if (!system.CanCombineNow(recipe))
            {
                // 원작 문구(알림 묶음 5) — 요청한 친구에게 모자란 것마다 한 줄.
                List<string> shortage = system.DescribeShortage(recipe);
                for (int i = 1; i < shortage.Count; i++) PlayerNotification.Show(sender.Slot, shortage[i], 5f);
                rejected = shortage.Count > 0 ? shortage[0] : "지금은 조합할 수 없습니다.";
            }
            ok = rejected == null && system.TryCombine(recipe, caster.transform.position);
        }
        finally { CombineSystem.ActingPlayerOverride = -1; }

        if (!ok) PlayerNotification.ShowFailure(sender.Slot, rejected ?? "지금은 조합할 수 없습니다.");
        if (commandsLogged++ < 30) Debug.Log($"[MP] 조합 요청 수행: 슬롯 {sender.Slot} 조합식 {recipeIndex}({identity.Data.unitName}) → {(ok ? "성공" : "거절: " + (rejected ?? "TryCombine 실패"))}");
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
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
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
        if (!used) PlayerNotification.ShowFailure(sender.Slot, reason ?? "지금은 사용할 수 없습니다.");
        if (commandsLogged++ < 30) Debug.Log($"[MP] 상점 요청 수행: 슬롯 {sender.Slot} {((Component)shop).name} 칸 {slot} → {(used ? "성공" : "실패: " + reason)}");
    }

    /// <summary>클라 → 호스트: 아이템 칸의 사용형 아이템을 쓴다(ItemUseKind 바이트 — 카탈로그 인덱스를 안 쓴다).</summary>
    public static void RequestUseItem(ItemUseKind kind)
    {
        if (NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_UseItem((byte)kind);
    }

    public static void ExecuteUseItem(NetPlayer sender, byte useKind)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        PlayerContext context = PlayerContext.Get(sender.Slot);
        if (context == null || RewardDistributor.Instance == null) return;
        bool used = RewardDistributor.Instance.UseItem(context, (ItemUseKind)useKind);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 아이템 사용 요청: 슬롯 {sender.Slot} {(ItemUseKind)useKind} → {(used ? "수행" : "보유 없음/불가")}");
    }

    public static void RequestHudUnitAction(NetHudAction action, Selectable unit, int argument)
    {
        NetEntity entity = unit != null ? unit.GetComponentInParent<NetEntity>() : null;
        if (entity == null || entity.Object == null || !entity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_HudUnitAction(entity.Object.Id, (byte)action, (byte)argument);
    }

    public static void ExecuteHudUnitAction(NetPlayer sender, NetworkId unit, NetHudAction action, int argument)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        if (!TryGetOwnedReal(sender, unit, action.ToString(), out GameObject real, allowShared: action == NetHudAction.CastActive)) return;
        if (!real.TryGetComponent(out Selectable selectable)) return;

        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return;

        switch (action)
        {
            case NetHudAction.Gamble: hud.ExecuteGambleOn(selectable, argument); break;
            case NetHudAction.Sell: hud.ExecuteSellOn(selectable); break;
            case NetHudAction.Trait: hud.ExecuteTraitOn(selectable); break;
            case NetHudAction.Yoonseo: hud.ExecuteYoonseoOn(selectable); break;
            case NetHudAction.Talent: hud.ExecuteTalentOn(selectable, argument); break;
            case NetHudAction.GambleBoost: hud.ExecuteGambleBoostOn(selectable); break;
            case NetHudAction.Bomb: hud.ExecuteBombOn(selectable); break;
            case NetHudAction.Enhance: hud.ExecuteEnhanceOn(selectable); break;
            case NetHudAction.Toto: hud.ExecuteTotoOn(selectable); break;
            case NetHudAction.CastActive: hud.ExecuteCastActiveOn(selectable); break;
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

    /// <summary>클라 → 호스트: 아군 지정 액티브(needsAllyClick — 신문철 엄마간식·고도현 약처방·임채민 축복의땅). 시전자·아군 거울 둘의 네트워크 번호만 보낸다.</summary>
    public static void RequestCastActiveOnAlly(Selectable caster, UnitIdentity ally)
    {
        NetEntity casterEntity = caster != null ? caster.GetComponentInParent<NetEntity>() : null;
        NetEntity allyEntity = ally != null ? ally.GetComponentInParent<NetEntity>() : null;
        if (casterEntity == null || allyEntity == null || casterEntity.Object == null || allyEntity.Object == null || !casterEntity.Object.IsValid || !allyEntity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_CastActiveOnAlly(casterEntity.Object.Id, allyEntity.Object.Id);
    }

    public static void ExecuteCastActiveOnAlly(NetPlayer sender, NetworkId caster, NetworkId ally)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        if (!TryGetOwnedReal(sender, caster, "CastActiveOnAlly", out GameObject real, allowShared: true)) return;
        if (!real.TryGetComponent(out Selectable selectable) || !real.TryGetComponent(out UnitAttacker attacker)) return;
        if (!sender.Runner.TryFindObject(ally, out NetworkObject allyObject) || !allyObject.TryGetComponent(out NetEntity allyEntity) || allyEntity.Real == null || !allyEntity.Real.TryGetComponent(out UnitIdentity allyIdentity)) return;
        SkillData skill = attacker.ActiveSkill;
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (skill == null || hud == null) return;
        hud.ExecuteCastActiveOnAlly(selectable, allyIdentity, skill);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 아군 지정 요청 수행: 슬롯 {sender.Slot} {real.name} → {allyEntity.Real.name}");
    }

    /// <summary>클라 → 호스트: 지점 지정 액티브(needsPointClick — 배성령 순간이동). 시전자 거울 번호와 클릭한 땅 좌표만 보낸다. 사거리·쿨·가능 여부는 호스트가 진짜 유닛에서 검증한다.</summary>
    public static void RequestCastActiveAtPoint(Selectable caster, Vector3 point)
    {
        NetEntity casterEntity = caster != null ? caster.GetComponentInParent<NetEntity>() : null;
        if (casterEntity == null || casterEntity.Object == null || !casterEntity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_CastActiveAtPoint(casterEntity.Object.Id, point);
    }

    public static void ExecuteCastActiveAtPoint(NetPlayer sender, NetworkId caster, Vector3 point)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        if (float.IsNaN(point.x + point.y + point.z) || float.IsInfinity(point.x + point.y + point.z)) return;
        if (!TryGetOwnedReal(sender, caster, "CastActiveAtPoint", out GameObject real, allowShared: true)) return;
        if (!real.TryGetComponent(out Selectable selectable) || !real.TryGetComponent(out UnitAttacker attacker)) return;
        SkillData skill = attacker.ActiveSkill;
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (skill == null || hud == null) return;
        hud.ExecuteCastActiveAtPoint(selectable, point, skill);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 지점 지정 요청 수행: 슬롯 {sender.Slot} {real.name} → {point}");
    }

    /// <summary>클라 → 호스트: 적 대상 지정 액티브(needsTargetClick — 강재규 단일도킹). 시전자·적 거울 번호만 보낸다.</summary>
    public static void RequestCastActiveOnEnemy(Selectable caster, EnemyDummy enemy)
    {
        NetEntity casterEntity = caster != null ? caster.GetComponentInParent<NetEntity>() : null;
        NetEntity enemyEntity = enemy != null ? enemy.GetComponentInParent<NetEntity>() : null;
        if (casterEntity == null || enemyEntity == null || casterEntity.Object == null || enemyEntity.Object == null || !casterEntity.Object.IsValid || !enemyEntity.Object.IsValid || NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_CastActiveOnEnemy(casterEntity.Object.Id, enemyEntity.Object.Id);
    }

    public static void ExecuteCastActiveOnEnemy(NetPlayer sender, NetworkId caster, NetworkId enemy)
    {
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        if (!TryGetOwnedReal(sender, caster, "CastActiveOnEnemy", out GameObject real, allowShared: true)) return;
        if (!real.TryGetComponent(out Selectable selectable)) return;
        EnemyDummy target = null;
        if (sender.Runner.TryFindObject(enemy, out NetworkObject enemyObject) && enemyObject.TryGetComponent(out NetEntity enemyEntity) && enemyEntity.Real != null)
            enemyEntity.Real.TryGetComponent(out target);
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (target == null || hud == null) return;   // 요청이 오는 사이 죽었다
        hud.ExecuteCastActiveOnTarget(selectable, target);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 적 지정 요청 수행: 슬롯 {sender.Slot} {real.name} → {target.name}");
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
        if (GamePause.Frozen) return;   // 일시정지·컷인 정지 중엔 호스트가 거절(클라 UI도 막지만 늦게 온 요청 방어)
        NetCatalog catalog = NetLauncher.Catalog;
        UnitTraitData trait = catalog != null && traitIndex >= 0 && traitIndex < catalog.traits.Count ? catalog.traits[traitIndex] : null;
        if (trait == null || !trait.targetsOtherUnit) return;
        if (!TryGetOwnedReal(sender, target, "특성 대상", out GameObject real) || !real.TryGetComponent(out UnitIdentity identity)) return;

        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        if (hud == null) return;
        hud.ExecuteTraitTargetOn(trait, PlayerContext.Get(sender.Slot), identity, sender.Slot);
        if (commandsLogged++ < 30) Debug.Log($"[MP] 특성 대상 요청 수행: 슬롯 {sender.Slot} {trait.name} → {real.name}");
    }

    // ───────────── 동맹 유닛 공유(10-08) ─────────────
    /// <summary>클라·호스트: 「내 유닛 조종을 target 슬롯에게 열기/닫기」 요청.</summary>
    public static void RequestSetShare(int targetSlot, bool on)
    {
        if (NetPlayer.Local == null || targetSlot < 0 || targetSlot >= AllianceShare.MaxSlots) return;
        NetPlayer.Local.RPC_SetShare((byte)targetSlot, on);
    }

    public static void ExecuteSetShare(NetPlayer sender, byte targetSlot, bool on)
    {
        NetGameState state = NetGameState.Instance;
        if (sender == null || sender.Runner == null || !sender.Runner.IsServer || state == null) return;
        int me = sender.Slot;
        if (me < 0 || me >= AllianceShare.MaxSlots || targetSlot >= AllianceShare.MaxSlots || targetSlot == me) return;
        if (PlayerContext.Get(targetSlot) == null) return;   // 앉은 사람에게만
        int mask = state.ShareMasks.Get(me);
        int next = on ? (mask | (1 << targetSlot)) : (mask & ~(1 << targetSlot));
        if (next == mask) return;
        state.ShareMasks.Set(me, next);
        string who = sender.DisplayName;
        PlayerNotification.Show(targetSlot, on ? $"<color=#FFD700>{who}님이 유닛 공유를 켰습니다.</color> 이제 그 유닛을 조종할 수 있습니다." : $"<color=#FFD700>{who}님이 유닛 공유를 껐습니다.</color>", 4f);
        Debug.Log($"[MP] 동맹 유닛 공유 {(on ? "켬" : "끔")}: 슬롯 {me}({who}) → 슬롯 {targetSlot}");
    }

    // ───────────── 일시정지(같이 하기, 10-08) ─────────────
    readonly static int[] pausesUsed = new int[8];

    /// <summary>클라·호스트 모두: 멈춤/풀기 요청. 호스트는 자기 RPC도 같은 경로(Execute)로 처리한다.</summary>
    public static void RequestPause(bool pause)
    {
        if (NetPlayer.Local == null) return;
        NetPlayer.Local.RPC_Pause(pause);
    }

    public static void ExecutePause(NetPlayer sender, bool pause)
    {
        NetGameState state = NetGameState.Instance;
        if (sender == null || sender.Runner == null || !sender.Runner.IsServer || state == null || !state.Started) return;
        if (pause == state.Paused) return;
        int slot = sender.Slot;
        if (pause)
        {
            if (GamePause.PausesPerPlayer > 0 && slot >= 0 && slot < pausesUsed.Length && pausesUsed[slot] >= GamePause.PausesPerPlayer)
            { PlayerNotification.Show(slot, $"일시정지는 한 판에 {GamePause.PausesPerPlayer}번까지입니다.", 3f); return; }
            if (slot >= 0 && slot < pausesUsed.Length) pausesUsed[slot]++;
        }
        else if (!GamePause.AnyoneCanResume && slot != state.PausedBy && slot != LocalPlayer.LocalPlayerId)   // 호스트(내 슬롯)는 늘 풀 수 있다
        { PlayerNotification.Show(slot, "멈춘 사람만 풀 수 있습니다.", 3f); return; }
        state.Paused = pause;
        state.PausedBy = (sbyte)(pause ? slot : -1);
        GamePause.ApplyNetworked(pause, pause ? slot : -1);   // 호스트 PC는 바로
        string who = sender.DisplayName;
        string msg = pause ? $"<color=#FFD700>{who}님이 일시정지했습니다.</color> (P로 계속)" : $"<color=#FFD700>{who}님이 재개했습니다.</color>";
        foreach (PlayerContext c in PlayerContext.Occupied) PlayerNotification.Show(c.PlayerId, msg, 4f);
        Debug.Log($"[MP] 일시정지 {(pause ? "멈춤" : "재개")}: 슬롯 {slot} {who}");
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

    static bool TryGetOwnedReal(NetPlayer sender, NetworkId target, string what, out GameObject real, bool allowShared = false)
    {
        real = null;
        if (sender == null || sender.Runner == null || !sender.Runner.IsServer) return false;

        if (!sender.Runner.TryFindObject(target, out NetworkObject obj) || !obj.TryGetComponent(out NetEntity entity) || entity.Real == null)
        {
            // 요청이 오는 사이 실물이 사라졌다(조합·소모·사망) — 흔한 일이라 조용히 넘긴다.
            return false;
        }

        int owner = entity.Real.TryGetComponent(out OwnedByPlayer ownedBy) ? ownedBy.OwnerId : -1;
        bool shared = allowShared && owner >= 0 && entity.Real.TryGetComponent(out UnitIdentity _) && AllianceShare.IsShared(owner, sender.Slot);   // 동맹 유닛 공유: 이동·공격·정지·스킬만(allowShared) — 소유가 바뀌는 동작은 주인만
        if (owner != sender.Slot && !shared)
        {
            Debug.LogWarning($"[MP] {what} 거절: 슬롯 {sender.Slot}이 슬롯 {owner}의 {entity.Real.name}을(를) 움직이려 했습니다.");
            PlayerNotification.Show(sender.Slot, AllianceShare.ViewOnlyMessage, 3f);
            return false;
        }
        if (shared && owner != sender.Slot) Debug.Log($"[MP] {what}: 슬롯 {sender.Slot}이 공유받은 슬롯 {owner}의 {entity.Real.name}을(를) 조종");

        real = entity.Real;
        return true;
    }
}
