using System.Collections.Generic;
using UnityEngine;

// 강화소 공용 컴포넌트 — 유닛강화소(트랙 8개)·다른세계 강화소·영원함 강화소(각 트랙 1개) 전부
// 이 하나로 커버한다(구현담당2와 합의). 도박소·도움소와 같은 구조: 레인 안에 서 있고, 파괴 불가,
// 적 타겟에서 자동 제외된다(EnemyDummy.Active/DestructibleGate.Active 어디에도 등록 안 됨).
//
// 상점은 자원만 쓰고 UnitUpgrades에 레벨만 올린다 — 필드에 있는 유닛에 값을 직접 곱하거나
// 되돌리지 않는다. 사는 순간 전부 반영되고 새로 스폰되는 유닛도 맞는 값이라는 것이 이 설계의
// 의도였다(PM 지시 — 도움소 임시 버프와 겹쳐도 안 깨진다).
//
// 🚨 **그런데 그 배율을 읽는 쪽이 없다. 지금 열면 골드만 먹는 상점이 된다.**
//
// 여기 원래 "실제 배율 적용은 UnitAttacker가 UnitUpgrades.MultiplierFor(grade)를 읽어서 한다"고
// 적혀 있었는데, **`MultiplierFor`라는 메서드는 존재한 적이 없다.** UnitAttacker가 보는 것은
// UnitUpgrades.EffectSum(unitData, DamageIncrease) 하나뿐이고, 그건 특성(UnitTraitData)을 훑지
// 이 상점이 올리는 legacyGradeLevels를 안 본다.
//
// 2026-09-05까지는 "PlayerContext.unitUpgrades가 씬에서 null이라 안 눌린다"는 게 우연한
// 안전장치였다(`Docs/reference/WIRING_AUDIT.md` §1). 오늘 사장님이 "원작 연구소로 만들어라"고
// 확정하면서(05번) 그 null을 없애기로 했다 — `MapGenerator`가 이제 `UnitUpgrades`를 붙인다.
// **그 우연한 안전장치가 사라지므로, 이 파일이 대신 잠근다.** `ResearchLabImplemented`
// 하나가 그 스위치다 — 연구소(레벨을 읽어 실제 배율에 곱하는 코드)가 완성되면 그 값만
// `true`로 바꾸면 된다. 그전까지 `TryUse`는 골드를 쓰기 전에 막고, 툴팁도 "다음 레벨: xN"을
// 약속하지 않고 준비 중이라고만 말한다 — 지킬 수 없는 약속을 화면에 남겨두지 않기 위해서다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class UnitUpgradeShop : MonoBehaviour, ILaneShop
{
    // 연구소(올린 레벨을 읽어 실제 공격력 배율에 곱하는 코드)가 완성되면 이 값 하나만
    // true로 바꾼다 — 그 외에는 아무것도 안 건드려도 된다. false인 동안 TryUse는 골드를
    // 쓰기 전에 막고, 툴팁도 "다음 레벨" 약속 대신 준비 중이라고만 말한다(사장님 결정
    // 05번, 2026-09-05 — "원작 연구소로 만들어라"가 아직 안 끝난 상태에서 상점만 먼저 열지
    // 않기 위함).
    const bool ResearchLabImplemented = false;

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
        if (!ResearchLabImplemented) return false;   // 슬롯이 눌러도 되는 것처럼 안 보이게
        if (track.maxLevel > 0 && level >= track.maxLevel) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null) return false;

        return context.GoldWallet.Gold >= track.CostForLevel(level);
    }
}
