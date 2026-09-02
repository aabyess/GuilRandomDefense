using System;
using System.Collections.Generic;

// 플레이어별 강화 상태. 특성강화(유닛 1종당 {효과, 수치} 리스트, 특성포인트 소모)로 쓴다 —
// GamblingProgress와 같은 결로 상태만 들고, 비용 확인·소비는 상점(아직 없음, PM 지시로 이번
// 라운드엔 안 만든다)이 한다.
//
// 획득처(폐문 보스 처치·도박 누적 졸업 등)는 사장님 결정 대기 중이라 AddTraitPoints를 아직
// 아무도 안 부른다 — 저장소·이벤트 자리만 미리 만들어둔다(PM 지시).
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
