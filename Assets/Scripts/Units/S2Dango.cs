using UnityEngine;

/// <summary>
/// S2 2.323 「경단먹이기」(오타마 스킬, j:41480~41530 CCI·CCJ). 남의 희귀함 이하 유닛을 고르고 채팅 「-경단」 →
/// 그 주인에게 「소유권을 변경하시겠습니까? 보상 12목재」가 알림으로 가고, 주인이 채팅 「-수락」하면
/// 주인은 12목재, 요청자는 그 유닛 + 4목재. 「-거절」(또는 30초 무응답)이면 없던 일. 동시에 한 건만.
/// 자기 유닛에 쓰면 즉시 16목재. 어느 쪽이든 쓰면 스킬이 사라진다(한 판 1회 — 원작 UnitRemoveAbility).
/// 판정은 호스트. (원작 확인 창은 우리에 대화창 뼈대가 없어 채팅 수락/거절로.)
/// </summary>
public static class S2Dango
{
    public const int OwnerReward = 12, CasterReward = 4, SelfReward = 16;
    public const float ExpireSeconds = 30f;

    static readonly bool[] used = new bool[4];
    static Selectable pendingUnit;
    static int pendingCaster = -1, pendingTarget = -1;
    static float pendingUntil;

    public static string LastResult { get; private set; }
    public static void ResetMatch() { for (int i = 0; i < used.Length; i++) used[i] = false; pendingUnit = null; pendingCaster = pendingTarget = -1; pendingUntil = 0f; LastResult = null; }

    static bool Pending => pendingUnit != null && pendingCaster >= 0 && Time.time < pendingUntil;

    static bool EligibleGrade(UnitGrade g) =>
        g == UnitGrade.Common || g == UnitGrade.Uncommon || g == UnitGrade.Special || g == UnitGrade.Rare;

    /// <summary>호스트: 요청자(casterSlot)가 유닛을 골라 「-경단」을 쳤다. 오류 문구를 돌려주고 정상이면 null.</summary>
    public static string Request(int casterSlot, Selectable unit)
    {
        int ci = Mathf.Clamp(casterSlot, 0, 3);
        if (used[ci]) return "경단먹이기는 이미 사용했습니다.";
        if (unit == null || !unit.TryGetComponent(out UnitIdentity identity) || identity.Data == null || !unit.TryGetComponent(out OwnedByPlayer owner))
            return "경단먹이기를 쓸 유닛을 선택하세요.";

        PlayerContext caster = PlayerContext.GetOccupied(casterSlot);
        if (caster == null || caster.ResourceWallet == null) return "사용할 수 없습니다.";

        if (owner.OwnerId == casterSlot)
        {
            caster.ResourceWallet.Add(ResourceType.Wood, SelfReward);
            used[ci] = true;
            S2Notify.Show(casterSlot, $"<color=#04FFC4>[알림]</color> 자신의 유닛에게 사용해 즉시 <color=#7AD823>{SelfReward} 목재</color>를 획득했습니다.", 5f);
            LastResult = "자기";
            return null;
        }

        if (!EligibleGrade(identity.Data.grade) || identity.IsSummon || identity.IsCopy) return "희귀 등급 이하 유닛에게만 사용 가능합니다.";
        PlayerContext target = PlayerContext.GetOccupied(owner.OwnerId);
        if (target == null || target.IsDead) return "받을 수 없는 플레이어입니다.";
        if (Pending) return "누가 '경단먹이기' 스킬을 사용 중입니다. 잠시 후 다시 시도해주세요.";

        pendingUnit = unit; pendingCaster = casterSlot; pendingTarget = owner.OwnerId; pendingUntil = Time.time + ExpireSeconds;
        string casterName = PlayerDisplayName.Of(casterSlot);
        S2Notify.Show(pendingTarget,
            $"<color=#FF8200>{casterName}님이 '경단먹이기'를 사용합니다</color>\n◎ 대상: {identity.Data.unitName}\n◎ 소유권 변경 보상: <color=#7AD823>{OwnerReward} 목재</color>\n채팅에 <color=#04FFC4>-수락</color> 또는 <color=#FF6060>-거절</color> ({ExpireSeconds:0}초)", ExpireSeconds);
        S2Notify.Show(casterSlot, "<color=#04FFC4>[알림]</color> 상대의 수락을 기다립니다.", 5f);
        LastResult = "요청";
        return null;
    }

    /// <summary>호스트: 채팅 「-수락」「-거절」. 이 명령이 아니면 null, 맞으면 결과 문구(""은 알림으로 이미 알림).</summary>
    public static string TryRespond(int slot, string text)
    {
        string t = (text ?? "").Trim();
        bool accept = t == "-수락", decline = t == "-거절";
        if (!accept && !decline) return null;
        if (!Pending || pendingTarget != slot) return "응답할 경단먹이기 요청이 없습니다.";

        int caster = pendingCaster, target = pendingTarget;
        Selectable unit = pendingUnit;
        pendingUnit = null; pendingCaster = -1; pendingTarget = -1;

        if (decline)
        {
            S2Notify.Show(caster, "<color=#04FFC4>[알림]</color> 유닛 소유권 변경을 상대방이 거절했습니다", 5f);
            LastResult = "거절";
            return "";
        }

        UnitIdentity identity = unit != null ? unit.GetComponent<UnitIdentity>() : null;
        // 30초 사이에 조합·창고·주인 변경으로 달라졌을 수 있다 — 아직 살아 있고 주인이 그대로인지 다시 본다.
        if (unit == null || identity == null || !unit.TryGetComponent(out OwnedByPlayer nowOwner) || nowOwner.OwnerId != target || !unit.gameObject.activeInHierarchy)
        {
            S2Notify.Show(caster, "<color=#FF0000>[오류]</color> 경단먹이기 대상 유닛이 달라져 취소되었습니다.", 5f);
            S2Notify.Show(target, "경단먹이기 대상 유닛이 달라져 취소되었습니다.", 4f);
            return "";
        }
        PlayerContext c = PlayerContext.GetOccupied(caster), t2 = PlayerContext.GetOccupied(target);
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (identity == null || identity.Data == null || c == null || t2 == null || spawner == null)
        {
            S2Notify.Show(caster, "<color=#FF0000>[오류]</color> 경단먹이기로 유닛이 지정되지 않았습니다.", 5f);
            return "";
        }

        UnitData data = identity.Data;
        LaneMarker lane = LaneMarker.Get(caster);
        Vector3 at = lane != null ? lane.TakeSpawnPosition(data) : unit.transform.position;
        identity.Consume();
        spawner.Spawn(data, at, caster);
        used[Mathf.Clamp(caster, 0, 3)] = true;
        t2.ResourceWallet?.Add(ResourceType.Wood, OwnerReward);
        c.ResourceWallet?.Add(ResourceType.Wood, CasterReward);
        S2Notify.Show(caster, $"<color=#04FFC4>[알림]</color> {data.unitName} 유닛과 <color=#7AD823>{CasterReward} 목재</color>를 획득!", 5f);
        S2Notify.Show(target, $"<color=#04FFC4>[알림]</color> <color=#FF8200>'경단먹이기'</color>를 수락하여 <color=#7AD823>{OwnerReward} 목재</color>를 획득!", 5f);
        LastResult = "수락";
        return "";
    }
}
