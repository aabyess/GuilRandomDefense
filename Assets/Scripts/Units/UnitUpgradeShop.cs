using System.Collections.Generic;
using UnityEngine;

// 강화소 공용 컴포넌트 — 유닛강화소(트랙 9개, 2026-09-05 히든 추가)·다른세계 강화소·영원함
// 강화소(각 트랙 1개) 전부 이 하나로 커버한다(구현담당2와 합의). 도박소·도움소와 같은 구조:
// 레인 안에 서 있고, 파괴 불가, 적 타겟에서 자동 제외된다(EnemyDummy.Active/
// DestructibleGate.Active 어디에도 등록 안 됨).
//
// 상점은 자원만 쓰고 UnitUpgrades에 레벨만 올린다 — 필드에 있는 유닛에 값을 직접 곱하거나
// 되돌리지 않는다. 사는 순간 전부 반영되고 새로 스폰되는 유닛도 맞는 값이라는 것이 이 설계의
// 의도였다(PM 지시 — 도움소 임시 버프와 겹쳐도 안 깨진다).
//
// ✅ 2026-09-05, 원작 연구소로 완성됐다(사장님 결정 05번). 지금까지 있던 함정 셋을 여기 남긴다
// (다음 사람이 같은 데 안 빠지게):
// 1. **읽는 쪽이 없었다.** `UnitAttacker.UpgradeMultiplier`가 옛날엔 존재하지도 않는
//    `UnitUpgrades.MultiplierFor(grade)`를 부른다고 주석에 거짓으로 적혀 있었다 — 실제로는
//    `EffectSum(DamageIncrease)`(특성강화)만 봤다. 지금은 `UnitUpgrades.MultiplierForGrade(grade)`
//    (이 상점이 올리는 legacyGradeLevels를 실제로 읽는다)를 추가로 곱한다.
// 2. **비용·배율 공식이 옛 지수식이었다.** 원작은 선형식이라 값만 바꿔서는 재현이 안 됐다
//    (`UnitUpgradeTrackData.cs` 주석 참고, `statLevel1Multiplier` 필드 추가로 해결).
// 3. **`PlayerContext.unitUpgrades`가 씬에서 null이었다**(`WIRING_AUDIT.md` §1) — 그 우연한
//    안전장치가 이 상점을 사실상 잠가주고 있었다. `MapGenerator`가 이제 붙이므로, 그 안전장치
//    없이도 정직하게 동작해야 한다. `ResearchLabImplemented`가 그 자리를 대신하다가 이제 true다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class UnitUpgradeShop : MonoBehaviour, ILaneShop
{
    // 연구소(올린 레벨을 읽어 실제 공격력 배율에 곱하는 코드)가 완성돼 true로 바뀌었다
    // (사장님 결정 05번, 2026-09-05). 다시 잠가야 할 일이 생기면 이 한 줄만 false로.
    const bool ResearchLabImplemented = true;

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

        if (!ResearchLabImplemented)
            return $"{track.trackName}\n{track.description}\n현재 Lv.{level} — 연구소 준비 중, 아직 강화할 수 없습니다";

        if (!track.hasOriginalResearch)
            return $"{track.trackName}\n{track.description}\n원작에 대응하는 연구소가 없는 등급입니다 — 강화할 수 없습니다";

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
        if (!ResearchLabImplemented) return false;   // 골드를 쓰기 전에 막는다 — 위 클래스 주석 참고
        if (index < 0 || index >= tracks.Count) return false;

        UnitUpgradeTrackData track = tracks[index];
        if (track == null || !track.hasOriginalResearch) return false;   // 원작에 대응 없는 트랙은 계속 잠김

        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;

        int level = context.UnitUpgrades.Level(track);
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        // ⚠️ 나머지 실패(연구소 준비 중·대응 없는 트랙·최대 레벨)는 GetSlotTooltip이 이미
        // 문구로 설명한다 — 골드 부족만 툴팁 없이 조용히 막혀 있었다(PM 지시, 2026-09-05).
        if (!context.GoldWallet.TrySpend(track.CostForLevel(level)))
        {
            PlayerNotification.Show(owner.OwnerId, "골드가 부족합니다!");
            return false;
        }

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
        if (!ResearchLabImplemented) return false;   // 슬롯이 눌러도 되는 것처럼 안 보이게
        if (!track.hasOriginalResearch) return false;
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null) return false;

        return context.GoldWallet.Gold >= track.CostForLevel(level);
    }
}
