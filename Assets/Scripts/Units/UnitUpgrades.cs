using System.Collections.Generic;

// 강화소(유닛/다른세계/영원함 공용)가 쓰는 플레이어별 강화 레벨 저장소. GamblingProgress와 같은 구조 —
// 상태만 들고, 비용·배율 계산은 UnitUpgradeTrackData(에셋)가, 실제 전투 반영은 UnitAttacker가 한다.
//
// UnitAttacker는 원본 attackDamage를 그대로 두고 때릴 때만 MultiplierFor(grade)를 곱한다
// (SupportShop의 임시 버프가 ApplyStats로 원본값을 기억했다 되돌리는 방식과 겹쳐도 안 깨지게 하려는
// 설계 — PM 지시). 그래서 레벨이 바뀌면 즉시 반영되도록 OnLevelChanged를 구독하게 한다.
public class UnitUpgrades : UnityEngine.MonoBehaviour
{
    readonly Dictionary<UnitUpgradeTrackData, int> levels = new Dictionary<UnitUpgradeTrackData, int>();

    public event System.Action OnLevelChanged;

    public int Level(UnitUpgradeTrackData track) =>
        track != null && levels.TryGetValue(track, out int level) ? level : 0;

    public void LevelUp(UnitUpgradeTrackData track)
    {
        if (track == null) return;

        levels[track] = Level(track) + 1;
        OnLevelChanged?.Invoke();
    }

    // 여러 트랙이 같은 등급을 걸 일은 없게 설계했지만(유닛강화소/다른세계/영원함이 등급을 나눠 갖는다),
    // 혹시 겹쳐도 안전하게 전부 곱해서 반환한다 — 안 산 트랙은 딕셔너리에 아예 없어 자동으로 1배다.
    public float MultiplierFor(UnitGrade grade)
    {
        float multiplier = 1f;

        foreach (KeyValuePair<UnitUpgradeTrackData, int> pair in levels)
        {
            if (pair.Key != null && pair.Key.targetGrades != null && pair.Key.targetGrades.Contains(grade))
                multiplier *= pair.Key.MultiplierForLevel(pair.Value);
        }

        return multiplier;
    }
}
