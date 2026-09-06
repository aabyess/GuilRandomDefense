using System.Collections.Generic;
using UnityEngine;

// H0BS("메타몽") 도박 재고 — ITEM_POOL_FULL_CENSUS.md 후속(f0d3da1) 확정: 골드로 사는 게
// 아니라 udg_Item_Int[플레이어]+1개가 상점 재고로 직접 꽂힌다(AddUnitToStockBJ). 이 필드가
// 그 udg_Item_Int 대응이다.
// - 시작값에 +1 보정이 있어(원작 재고식이 Item_Int+1) 처음부터 1회는 항상 가능하다 — 그래서
//   기본값을 1로 둔다(0에 +1을 미리 계산해 저장한 것).
// - 도박 1회당 -1(원작 트리거가 ItemGet 직전에 Item_Int를 1 차감).
// - 스토리 보상(원작 Trig_Story_reward6/_reward9)에서 +1 — ⚠️ 이번 작업 범위에서 배선 안 함.
//   우리 스토리(Story01~13)는 원작 챕터와 번호·내용이 무관한 창작 콘텐츠다(TEAM_RULES
//   "스토리 번호는 원작과 별개, 겹쳐도 다른 것") — 어느 우리 스토리가 원작 reward6/9에
//   대응하는지 확정할 근거가 없어, 근거 없이 붙이면 도박 획득 횟수가 원작과 달라진다.
//   AddStock()은 만들어 뒀으니, 대응이 확정되면 그 지점에서 호출하면 된다.
//
// GamblingProgress와 별개 컴포넌트로 뗀 이유: GamblingProgress는 "GamblingOptionData별
// 진행 상태"(옵션이 늘어도 필드 안 늘린다)라는 좁은 목적이 있는데, 이 재고는 그 어떤
// GamblingOptionData와도 무관하다(옵션 자체가 없는, 상점 재고 직접 주입 방식) — 같이
// 두면 그 클래스가 명시한 "필드를 안 늘린다" 원칙이 흐려진다.
public class ItemGambleState : MonoBehaviour
{
    [SerializeField] int stock = 1; // 원작 Item_Int(0 시작) + 1 보정 = 1

    // ⚠️ 맨 뒤에 추가(2026-09-06, "항법" 5택1 연결, NAVIGATION_ROUTES_FULL.md) — 직렬화
    // 순서를 지킨다. PlayerContext의 다른 형제 컴포넌트(NavigationState)를 직접
    // 참조한다 — 씬에서 같은 슬롯(GameObject)에 같이 붙는다.
    [SerializeField] NavigationState navigationState;

    public bool HasStock => stock > 0;
    public int Stock => stock;

    // udg_Tech_No_support 대응 — 원작 "항법: 도움소 잠금"(NavigationChoice.SupportLock)을
    // 고르면 켜지는 플래그(ITEM_POOL_FULL_CENSUS.md 후속 ②, f0d3da1) — 아이템 도박이
    // 축소풀(13종)로 바뀐다. navigationState가 안 붙어 있으면(씬 배선 전) 기존과 같이
    // false — 회귀 없음.
    public bool ReducedPoolActive => navigationState != null && navigationState.Choice == NavigationChoice.SupportLock;

    // 2026-09-07 정정(PM 지시, ITEMPOOL_REMOVAL_SEMANTICS.md) — 원작은 뽑힌/지급된 아이템
    // 타입을 그 플레이어의 풀에서 영구히 지운다(리필 없음, 전체·축소 풀 양쪽에 동시 적용 —
    // ItemGamblePoolData.Roll 참고). 자산(ItemGamblePoolData)은 4명이 공유하는 SO라 여기에
    // 못 두고, 플레이어별 컴포넌트인 여기(ItemGambleState)가 "이미 받은 종류"를 든다.
    //
    // ⚠️ 저장하지 않는다(unlockedTraits·GamblingProgress와 같은 결) — 한 판 한정 상태다.
    readonly HashSet<ItemData> drawnItemTypes = new HashSet<ItemData>();

    /// <summary>이 플레이어가 이 아이템 타입을 이미 받았는지 — 전체·축소 풀 공통(원작이
    /// 두 풀 다 지우므로 하나만 있으면 된다).</summary>
    public bool HasDrawn(ItemData item) => item != null && drawnItemTypes.Contains(item);

    /// <summary>도박이 아닌 경로(보스 확정지급·스토리 보상·퀘스트 등, 아직 우리에 없음)로
    /// 이 풀 소속 아이템을 지급할 때도 반드시 이걸 불러야 한다 — "획득 지점 하나로 모으기"
    /// (PM 지시). 지금은 TryGamble이 유일한 호출부다.</summary>
    public void RegisterAcquired(ItemData item)
    {
        if (item != null) drawnItemTypes.Add(item);
    }

    /// <summary>
    /// 도박 1회 시도 — 재고가 없으면 false(아무 것도 안 줄어들고 안 뽑힘). 재고가 있으면
    /// 원작과 같은 순서(차감 먼저, 그다음 뽑기)로 1을 먼저 소모한 뒤 풀에서 하나를 뽑는다.
    /// pool.Roll이 null을 돌려줘도(이미 다 뽑았거나 풀 자체가 비어있는 등) 재고는 이미
    /// 소모된 채로 유지한다 — 원작 트리거도 차감을 ItemGet 성공 여부와 무관하게 먼저 한다.
    /// 뽑힌 결과는 즉시 drawnItemTypes에 등록해 다음 도박부터 같은 종류가 다시 안 나온다.
    /// </summary>
    public bool TryGamble(ItemGamblePoolData pool, out ItemData result)
    {
        result = null;
        if (pool == null || stock <= 0) return false;

        stock--;
        result = pool.Roll(ReducedPoolActive, drawnItemTypes);
        if (result != null) RegisterAcquired(result);
        return true;
    }

    /// <summary>스토리 보상 등에서 재고를 늘릴 때 부른다. 지금은 아무도 안 부른다(위 주석 참고).</summary>
    public void AddStock(int amount)
    {
        stock += amount;
    }
}
