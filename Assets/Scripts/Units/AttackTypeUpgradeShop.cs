using System.Collections.Generic;
using UnityEngine;

// 원작 "강화소 3"(공격타입×등급 업그레이드) 구매 경로 — 2026-09-06. `UnitUpgradeShop`
// (원작 "강화소 1", 등급트랙)과 같은 패턴이되 별개 컴포넌트다 — 원작에서 서로 다른
// 건물이라 하나로 합치지 않는다(PM 지시, "강화소를 합치지 말라"의 데이터 판에 이어
// 컴포넌트 판). 도박소·도움소·UnitUpgradeShop과 같은 구조: 레인 안에 서 있고, 파괴
// 불가, 적 타겟에서 자동 제외된다.
//
// 원작 마스터 버튼 4개(일반·공성·관통·패기)만 플레이어가 산다 — 자식 12개는
// `UnitUpgrades.LevelUp`이 부르는 즉시(레벨이 오르는 즉시) 자동으로 딸려온다는 뜻이
// 아니라,애초에 우리는 자식을 별도 데이터로 안 담으므로 "마스터 레벨이 오른다"가
// 곧 "자식이 다 딸려온 것과 같은 효과"다(AttackTypeUpgradeTrackData.cs 주석 참고 —
// 자식 절대값은 SkillEffectBasis.ResearchLevel 소비 스킬이 이미 갖고 있어 안 옮김).
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class AttackTypeUpgradeShop : MonoBehaviour, ILaneShop
{
    [SerializeField] List<AttackTypeUpgradeTrackData> tracks = new List<AttackTypeUpgradeTrackData>();

    struct SlotState
    {
        public bool initialized;
        public int cachedLevel;
        public string cachedLabel;
    }

    SlotState[] slotState;

    OwnedByPlayer owner;

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
        slotState = new SlotState[tracks.Count];
    }

    // ---- ILaneShop ----

    public int SlotCount => tracks.Count;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (index < 0 || index >= tracks.Count) return LaneShopSlotView.Empty;

        AttackTypeUpgradeTrackData track = tracks[index];
        if (track == null) return LaneShopSlotView.Empty;

        int level = LevelOf(track);

        ref SlotState state = ref slotState[index];
        if (!state.initialized || state.cachedLevel != level)
        {
            state.initialized = true;
            state.cachedLevel = level;
            state.cachedLabel = track.maxLevel > 0 && level >= track.maxLevel
                ? $"{track.trackName}\nLv.{level} (MAX)"
                : $"{track.trackName}\nLv.{level}";
        }

        return new LaneShopSlotView(state.cachedLabel, Color.white, CanUpgrade(track, level), LaneShopTargetKind.None);
    }

    public string GetSlotTooltip(int index)
    {
        if (index < 0 || index >= tracks.Count) return null;

        AttackTypeUpgradeTrackData track = tracks[index];
        if (track == null) return null;

        int level = LevelOf(track);

        if (track.maxLevel > 0 && level >= track.maxLevel)
            return $"{track.trackName}\n{track.description}\n현재 Lv.{level} — 최대 레벨";

        int goldCost = track.CostForLevel(level);
        int woodCost = track.WoodCostForLevel(level);
        return $"{track.trackName}\n{track.description}\n"
             + $"현재 Lv.{level}\n"
             + $"다음 레벨 — 비용 {goldCost}엔 + 목재 {woodCost}개";
    }

    public bool TryUse(int index, LaneShopTarget target, out string failReason)
    {
        failReason = null;

        if (index < 0 || index >= tracks.Count) return false;

        AttackTypeUpgradeTrackData track = tracks[index];
        if (track == null) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null
            || context.GoldWallet == null || context.ResourceWallet == null) return false;

        int level = context.UnitUpgrades.Level(track);
        if (track.maxLevel > 0 && level >= track.maxLevel)
        {
            failReason = "이미 최대 레벨입니다.";
            return false;
        }

        int goldCost = track.CostForLevel(level);
        int woodCost = track.WoodCostForLevel(level);

        // 확인부터 하고 나서 차감한다(CombineSystem.CanAfford와 같은 순서 원칙) — 골드는
        // 있는데 목재가 모자라 절반만 깎이는 사고를 막는다.
        if (context.GoldWallet.Gold < goldCost)
        {
            failReason = "골드가 부족합니다!";
            return false;
        }
        if (context.ResourceWallet.Get(ResourceType.Wood) < woodCost)
        {
            failReason = "목재가 부족합니다!";
            return false;
        }

        context.GoldWallet.TrySpend(goldCost);
        context.ResourceWallet.TrySpend(ResourceType.Wood, woodCost);
        context.UnitUpgrades.LevelUp(track);
        return true;
    }

    int LevelOf(AttackTypeUpgradeTrackData track)
    {
        UnitUpgrades upgrades = OwnerContext?.UnitUpgrades;
        return upgrades != null ? upgrades.Level(track) : 0;
    }

    bool CanUpgrade(AttackTypeUpgradeTrackData track, int level)
    {
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null || context.ResourceWallet == null) return false;

        return context.GoldWallet.Gold >= track.CostForLevel(level)
            && context.ResourceWallet.Get(ResourceType.Wood) >= track.WoodCostForLevel(level);
    }
}
