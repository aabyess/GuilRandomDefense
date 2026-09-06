using UnityEngine;

// 원작 Rerole_count_int 대응 — "희귀함 리롤"(A0VX) 시도 횟수, 플레이어당 판 전체 누적
// (Docs/reference/UNIQUE_REROLE_AND_SELL_FAMILY.md ⑤: 어느 유닛으로 시도하든 전부 같은
// 카운터에 쌓인다, 라운드·유닛 단위 아님). ItemGambleState와 같은 결 — PlayerContext의
// 형제 컴포넌트로 씬 슬롯에 붙는다. 저장하지 않는다(GamblingProgress·unlockedTraits와
// 같은 결, 한 판 한정 상태).
public class UniqueRerollState : MonoBehaviour
{
    [SerializeField] int attemptCount;

    [SerializeField] UniqueRerollAbilityData data;

    // NavigationState.Choice==Gambler("도박광")로 한도·실패확률을 바꾼다 — GamblingShop.
    // FailureLuckyTokens와 같은 패턴(ItemGambleState.ReducedPoolActive도 동일 결).
    [SerializeField] NavigationState navigationState;

    bool IsGambler => navigationState != null && navigationState.Choice == NavigationChoice.Gambler;

    public int Limit => data == null ? 0 : (IsGambler ? data.gamblerLimit : data.baseLimit);
    public float FailChancePercent => data == null ? 0f : (IsGambler ? data.gamblerFailChancePercent : data.baseFailChancePercent);
    public int AttemptsUsed => attemptCount;
    public bool HasAttemptsLeft => data != null && attemptCount < Limit;
    public UniqueRerollAbilityData Data => data;

    /// <summary>시도 1회 기록 — 성공/실패 무관하게 호출한다(원작: 목재 차감과 같은 지점,
    /// 실패 굴림 전에 먼저 증가한다).</summary>
    public void RecordAttempt() => attemptCount++;
}
