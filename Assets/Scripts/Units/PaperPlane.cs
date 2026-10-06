using UnityEngine;

/// <summary>
/// 유물 「종이비행기」(초월 이재윤, 사장님 10-06) — 스토리 7 보상(원작 버스터콜 1/20 자리). 인벤토리에서 눌러 쓴다: 적 하나(보스 가능)를 **직접 골라** 영구 정지 +
/// 쓴 플레이어 **레인의 적 전부**의 현재체력 −25%(한 번만, 영구 디버프 아님 — 보스·스토리 적도 그 레인에 있으면 포함). 한 판에 한 번, 아이템은 남는다. 호스트 판정.
/// </summary>
public static class PaperPlane
{
    public const float HpCutFraction = 0.25f;

    public static bool HasItem(PlayerContext context)
    {
        if (context == null || context.ItemInventory == null) return false;
        foreach (ItemData item in context.ItemInventory.Items)
            if (item != null && item.useKind == ItemUseKind.PaperPlane) return true;
        return false;
    }

    /// <summary>쓸 수 있으면 영구 정지 + 레인 −25%를 하고 true. 못 쓰면 이유를 돌려주고 false.</summary>
    public static bool TryUse(PlayerContext context, EnemyDummy target, out string reason)
    {
        reason = null;
        if (!GameAuthority.IsServer) { reason = "호스트만 쓸 수 있습니다."; return false; }
        if (!HasItem(context)) { reason = "종이비행기가 없습니다."; return false; }
        if (context.ItemInventory.PaperPlaneUsed) { reason = "종이비행기는 한 번만 쓸 수 있습니다(이미 썼습니다)."; return false; }
        if (target == null || target.IsDead) { reason = "대상 적을 찾을 수 없습니다."; return false; }

        context.ItemInventory.PaperPlaneUsed = true;
        target.FreezeForever();
        int lane = context.PlayerId;
        int cut = 0;
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            if (enemy == null || enemy.IsDead || enemy.LaneIndex != lane) continue;
            enemy.CutCurrentHpPercent(HpCutFraction);
            cut++;
        }
        PlayerNotification.Show(context.PlayerId, $"<color=#FFD700>종이비행기!</color> {target.name.Replace("(Clone)", "")} 영구 정지 · 내 레인 적 {cut}기 체력 −25%", 6f);
        return true;
    }
}
