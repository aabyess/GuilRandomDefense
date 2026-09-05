using System;
using System.Collections.Generic;

// 플레이어별 강화 상태. 특성강화(유닛 1종당 {효과, 수치} 리스트, 특성포인트 소모)로 쓴다 —
// GamblingProgress와 같은 결로 상태만 들고, 비용 확인·소비는 상점이 한다.
//
// 획득처는 사장님 확정(2026-09-05, 07번 갱신): 게임 시작 1개 + 15,000엔 구매 1개 +
// 스토리 12 클리어 1개 + 피카 퀘스트 성공 1개, 한 판에 최대 4개(이전엔 3개 + "8라운드"를
// 스토리 8로 잘못 해석한 상태였다 — PirateQuestManager.HandleSuccess·RewardDistributor 주석
// 참고). 넷 다 플레이어별로 딱 한 번씩만 — GamblingProgress처럼 임의 개수의 옵션을
// HashSet으로 추적하는 대신, 출처가 정확히 4개로 고정돼 있어 이름 붙은 bool 네 개로 더 명확하게
// 표현했다. 각 출처의 실제 호출은: RewardDistributor(시작·스토리 클리어), GamblingShop(구매),
// PirateQuestManager(피카).
public class UnitUpgrades : UnityEngine.MonoBehaviour
{
    // ---- 특성강화(신규) ----

    public int TraitPoints { get; private set; }
    public event Action OnTraitPointsChanged;

    public void AddTraitPoints(int amount)
    {
        if (amount <= 0) return;
        TraitPoints += amount;
        OnTraitPointsChanged?.Invoke();
    }

    bool startingPointGranted;
    bool purchasedPointGranted;
    bool storyPointGranted;
    bool pirateQuestPointGranted;

    public bool HasStartingPoint => startingPointGranted;
    public bool HasPurchasedPoint => purchasedPointGranted;
    public bool HasStoryPoint => storyPointGranted;
    public bool HasPirateQuestPoint => pirateQuestPointGranted;

    // 게임 시작 시 1개. RewardDistributor.GrantStartingTraitPoints가 부른다.
    public void GrantStartingPoint()
    {
        if (startingPointGranted) return;
        startingPointGranted = true;
        AddTraitPoints(1);
    }

    // 15,000엔으로 1개(1회 한정). 골드 차감까지 여기서 같이 한다 — TryUnlock류와 같은 원자적 형태
    // (차감과 지급 사이에 실패가 끼어들 여지를 없앤다). GamblingShop이 부른다.
    public bool TryPurchasePoint(GoldWallet wallet, int cost)
    {
        if (purchasedPointGranted || wallet == null) return false;
        if (!wallet.TrySpend(cost)) return false;

        purchasedPointGranted = true;
        AddTraitPoints(1);
        return true;
    }

    // 스토리 12 클리어 1개(사장님 확정 2026-09-05 — 이전엔 "8라운드"를 스토리 8로 잘못 해석해
    // order==8이었다). RewardDistributor.GrantStoryReward가 부른다.
    public void GrantStoryPoint()
    {
        if (storyPointGranted) return;
        storyPointGranted = true;
        AddTraitPoints(1);
    }

    // 피카 퀘스트 성공 1개(사장님 확정 2026-09-05, 07번 — "피카 퀘스트도 준다"). 한 플레이어당
    // 피카 토큰이 하나뿐이라 사실상 1회 한정이지만, 다른 셋과 같은 방어선을 둔다.
    // PirateQuestManager.HandleSuccess가 quest.successTraitPoints > 0일 때 부른다 — 지금은
    // 피카 하나뿐이라 "받는다/안 받는다"만 의미 있고, 필드 값 자체(항상 1)는 안 쓴다.
    public void GrantPirateQuestPoint()
    {
        if (pirateQuestPointGranted) return;
        pirateQuestPointGranted = true;
        AddTraitPoints(1);
    }

    readonly HashSet<UnitTraitData> unlockedTraits = new HashSet<UnitTraitData>();

    public bool IsUnlocked(UnitTraitData trait) => trait != null && unlockedTraits.Contains(trait);

    // 포인트 차감·비용 확인은 상점(아직 없음) 몫이다 — 여기선 언락 상태만 바꾼다.
    public void Unlock(UnitTraitData trait)
    {
        if (trait == null || !unlockedTraits.Add(trait)) return;
        OnLevelChanged?.Invoke();
    }

    // UnitAttacker가 자기 유닛(UnitData)에 해당하는 특성 효과 합을 조회할 때 쓴다. 한 유닛에
    // 트레잇 에셋이 여러 개 걸릴 일은 없게 설계했지만(유닛 1종 = 에셋 1개), 혹시 겹쳐도 전부 더한다.
    public float EffectSum(UnitData unit, TraitEffectKind kind)
    {
        if (unit == null) return 0f;

        float sum = 0f;
        foreach (UnitTraitData trait in unlockedTraits)
        {
            if (trait == null || trait.targetUnit != unit || trait.effects == null) continue;

            foreach (TraitEffect effect in trait.effects)
                if (effect.kind == kind) sum += effect.value;
        }

        return sum;
    }

    public event Action OnLevelChanged;

    // ---- 구 등급강화(레거시) ----
    //
    // UnitUpgradeShop.cs(구 유닛강화소 — 등급 전체 강화)가 아직 이 두 메서드를 그대로 부른다.
    // 그 UI는 구현담당2의 ILaneShop 리팩터와 맞물려 있어 지금 손대면 안 된다(PM 지시:
    // "UI는 빼세요, 둘이 합의한 뒤에 손대세요"). 특성강화용 UI가 그 자리를 대체하면
    // UnitUpgradeShop.cs·UnitUpgradeTrackData.cs와 함께 이 블록도 통째로 지운다.
    readonly Dictionary<UnitUpgradeTrackData, int> legacyGradeLevels = new Dictionary<UnitUpgradeTrackData, int>();

    public int Level(UnitUpgradeTrackData track) =>
        track != null && legacyGradeLevels.TryGetValue(track, out int level) ? level : 0;

    public void LevelUp(UnitUpgradeTrackData track)
    {
        if (track == null) return;
        legacyGradeLevels[track] = Level(track) + 1;
        OnLevelChanged?.Invoke();
    }
}
