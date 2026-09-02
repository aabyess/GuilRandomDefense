using System.Collections.Generic;
using UnityEngine;

// 강화소 공용 컴포넌트 — 유닛강화소(트랙 8개)·다른세계 강화소·영원함 강화소(각 트랙 1개) 전부
// 이 하나로 커버한다(구현담당2와 합의). 도박소·도움소와 같은 구조: 레인 안에 서 있고, 파괴 불가,
// 적 타겟에서 자동 제외된다(EnemyDummy.Active/DestructibleGate.Active 어디에도 등록 안 됨).
//
// 상점은 자원만 쓰고 UnitUpgrades에 레벨만 올린다 — 필드에 있는 유닛에 값을 직접 곱하거나
// 되돌리지 않는다. 실제 배율 적용은 UnitAttacker가 공격 시점에 UnitUpgrades.MultiplierFor(grade)를
// 읽어서 하므로(PM 지시 — 도움소 임시 버프와 겹쳐도 안 깨지는 설계), 여기서 살아있는 유닛을
// 순회할 필요가 없다. 사는 순간 전부 반영되고, 새로 스폰되는 유닛도 처음부터 맞는 값이다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class UnitUpgradeShop : MonoBehaviour, ILaneShop
{
    [SerializeField] List<UnitUpgradeTrackData> tracks = new List<UnitUpgradeTrackData>();

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

        UnitUpgradeTrackData track = tracks[index];
        if (track == null) return LaneShopSlotView.Empty;

        int level = LevelOf(track);

        // 문서 주석대로(ILaneShop) 라벨은 바뀌었을 때만 새로 조립한다 — 매 0.4초 호출에서
        // 레벨이 그대로면 문자열을 다시 안 만든다.
        ref SlotState state = ref slotState[index];
        if (!state.initialized || state.cachedLevel != level)
        {
            state.initialized = true;
            state.cachedLevel = level;
            state.cachedLabel = track.maxLevel > 0 && level >= track.maxLevel
                ? $"{track.trackName}\nLv.{level} (MAX)"
                : $"{track.trackName}\nLv.{level}";
        }

        return new LaneShopSlotView(state.cachedLabel, track.slotColor, CanUpgrade(track, level), LaneShopTargetKind.None);
    }

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다.
    public string GetSlotTooltip(int index)
    {
        if (index < 0 || index >= tracks.Count) return null;

        UnitUpgradeTrackData track = tracks[index];
        if (track == null) return null;

        int level = LevelOf(track);
        float multiplier = track.MultiplierForLevel(level);

        if (track.maxLevel > 0 && level >= track.maxLevel)
            return $"{track.trackName}\n{track.description}\n현재 Lv.{level} (공격력 x{multiplier:F2}) — 최대 레벨";

        int cost = track.CostForLevel(level);
        float nextMultiplier = track.MultiplierForLevel(level + 1);
        return $"{track.trackName}\n{track.description}\n"
             + $"현재 Lv.{level} (공격력 x{multiplier:F2})\n"
             + $"다음 레벨: x{nextMultiplier:F2} — 비용 {cost}엔";
    }

    public bool TryUse(int index, LaneShopTarget target)
    {
        if (index < 0 || index >= tracks.Count) return false;

        UnitUpgradeTrackData track = tracks[index];
        if (track == null) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;

        int level = context.UnitUpgrades.Level(track);
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        if (!context.GoldWallet.TrySpend(track.CostForLevel(level))) return false;

        context.UnitUpgrades.LevelUp(track);
        return true;
    }

    int LevelOf(UnitUpgradeTrackData track)
    {
        UnitUpgrades upgrades = OwnerContext?.UnitUpgrades;
        return upgrades != null ? upgrades.Level(track) : 0;
    }

    bool CanUpgrade(UnitUpgradeTrackData track, int level)
    {
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null) return false;

        return context.GoldWallet.Gold >= track.CostForLevel(level);
    }
}
