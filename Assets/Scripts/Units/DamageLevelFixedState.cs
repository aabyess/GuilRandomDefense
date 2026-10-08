using UnityEngine;

// 원작 udg_Damage_level_Fixed[플레이어] — 그 플레이어(라인)의 일반 몹이 받는 A11S
// (취약도) 레벨에 영구히 더해지는 카운터. NAVIGATION_ROUTES_FULL.md/PM 확정
// (2026-09-06, 리서치담당 c3b8c42 정정 반영):
//
//   피해 = 대상현재체력 × 0.10 × (0.20 + 0.05 × A11S레벨), A11S레벨 = 14 + 이 값
//   기본(0) → 레벨14 → 계수 0.90(지금 "일반" 값과 동일, 회귀 없음)
//   항법 "패왕의길" → +2 → 레벨16 → 계수 1.00(지금 "보스" 값과 같아짐)
//
// 히든 이벤트 3개(Hidden_Aokiji+2·Eternal_Lucci+2·IM_dragon+4)도 같은 변수를
// 누적시키는 것으로 원작에 확인됐지만, 달성 조건과 상호배제 여부가 아직
// [미확인]이라(리서치 진행 중) 여기서는 Add()만 열어두고 아무도 안 부른다 — PM
// 지시, "카운터에 더하는 자리만 열어두세요."
//
// ⚠️ 신세계 사이드보스·라운드보스는 이 축을 안 탄다(Round_Unit과 별개 경로,
// A11S가 그쪽은 고정) — WaveSpawner.SpawnEnemyInternal의 `!enemyData.isBoss`
// 게이트가 그 구분을 담당한다, 여기서는 값만 들고 있는다.
public class DamageLevelFixedState : MonoBehaviour
{
    [SerializeField] int value;

    public int Value => value;

    public void Add(int amount) => value += amount;

    // 아오키지 히든(원작 Hidden_Aokiji +2, 「한 플레이어 1번만」) — 우리는 히든_성탄이 조합식 없이 도박으로만 나와서(PM 10-08), 그 플레이어가
    // 히든_성탄을 **처음 얻을 때** +2를 한 번 준다. 호스트(싱글 포함)만 OnAcquired를 쏜다.
    public const string AokijiUnitAsset = "히든_성탄";
    public const int AokijiBonus = 2;
    [SerializeField] bool aokijiGranted;

    public bool TryGrantAokijiOnce()
    {
        if (aokijiGranted) return false;
        aokijiGranted = true;
        value += AokijiBonus;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        UnitIdentity.OnAcquired -= HandleAcquired;
        UnitIdentity.OnAcquired += HandleAcquired;
    }

    static void HandleAcquired(UnitIdentity unit, UnitInventory inventory)
    {
        if (unit == null || unit.Data == null || unit.Data.name != AokijiUnitAsset) return;
        PlayerContext.Get(unit.OwnerId)?.DamageLevelFixedState?.TryGrantAokijiOnce();
    }
}
