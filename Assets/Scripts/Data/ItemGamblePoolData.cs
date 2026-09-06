using System.Collections.Generic;
using UnityEngine;

// 원작 아이템 도박(H0BS "메타몽" 판매, ITEM_POOL_FULL_CENSUS.md §① 재확인)의 두 풀을 담는다 —
// itpool[0..4](전체 22종) · itpool[5..10](축소 13종, 전설·초월·희귀 상위만). 원작은 플레이어마다
// 풀 사본을 따로 쓴다 — 이 자산 자체는 풀 "정의"(고정 데이터, 가중치·구성)만 담고, 소모
// 상태(이미 뽑은 종류)는 안 갖는다.
//
// ⚠️ 2026-09-07 정정(PM 지시, ITEMPOOL_REMOVAL_SEMANTICS.md — 리서치담당 18곳 전수) —
// "풀 구성 자체는 안 줄어든다"는 예전 서술은 **틀렸다**. 원작은 `ItemPoolRemoveItemType`로
// 뽑힌/지급된 아이템 타입을 그 플레이어의 풀에서 **영구히** 지운다(리필 없음, 18곳 전부
// 같은 패턴 — I00S만 특별한 게 아니라 38종 전체). 그래서 이 자산(공유 정의)은 그대로
// 두고, "이미 뽑은 종류" 제외 목록은 플레이어별 컴포넌트(ItemGambleState.drawnItemTypes)가
// 들고 Roll() 호출 시 넘긴다 — 자산을 직접 깎지 않는다(자산은 4명이 공유하는 SO라 깎으면
// 전역이 되어버린다, 원작은 플레이어별 독립 풀이다).
//
// 가중치·구성 확정(2026-09-06, ITEM_POOL_FULL_CENSUS.md/59db416): 항목별 정확한 가중치가
// 나왔다 — 전체 풀 1.25(I00H·I007)/1.33(I003·I004·I00J·I00Y·I010)/1.45(I00V·I00L·I00M·
// I00P·I00S·I00U)/2(I00Q·I00K·I00O·I00N·I00R·I00W·I00X·I00Z·I011), 축소 풀 1(I00H·I007)/
// 1.00(I003·I004·I00J·I00Y·I010)/0.8(I00V·I00L·I00M·I00P·I00S·I00U). 자산(`ItemGamblePool_
// H0BS.asset`)에 이 값 그대로 반영했다.
//
// ⚠️ 축소 풀 구성 — 등급 규칙이 아니라 원작이 리터럴로 하드코딩한 13개 목록이다. "특별함
// 제외"로 재구성하면 안 된다 — `I00Q`(하늘섬전사의창, 희귀함)가 특별함이 아닌데도 축소
// 풀에서 빠지는 게 그 증거다(전체 풀의 가중치=2 그룹 9종이 전부 빠지고, 그중 8종은
// 특별함이지만 I00Q 하나는 희귀함이다 — 등급으로 걸렀으면 안 빠졌을 항목). 다음에 이
// 자산을 재생성/수정할 사람은 등급 필터 코드를 짜지 말고 위 리터럴 목록을 그대로 옮길 것.
[CreateAssetMenu(fileName = "NewItemGamblePool", menuName = "GuilRandomDefense/Item Gamble Pool")]
public class ItemGamblePoolData : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ItemData item;
        public float weight;
    }

    [SerializeField] List<Entry> fullPool = new List<Entry>();     // itpool[0..4], 22종
    [SerializeField] List<Entry> reducedPool = new List<Entry>();  // itpool[5..10], 13종(원작 리터럴 목록, 위 주석 참고)

    /// <summary>
    /// useReducedPool이 true면 축소 풀(전설·초월·희귀 상위), false면 전체 풀에서 가중치대로
    /// 하나를 뽑는다. <paramref name="excluded"/>에 들어있는 항목은 이미 그 플레이어가
    /// 받은 종류라 후보에서 뺀다(원작 ItemPoolRemoveItemType, 전체·축소 양쪽에 동일 적용 —
    /// 호출부가 같은 집합을 두 Roll() 호출에 그대로 넘긴다). null을 넘기면 제외 없이 돈다.
    /// 풀이 비어 있거나(제외 후) 총 가중치가 0 이하면 null — GachaTable.Roll()과 같은
    /// 방어 규칙이다(뿌리: GachaTable.weight가 실제로 안 읽히는 경로가 있었던 전례 — 여기는
    /// Roll()이 유일한 진입점이라 그 문제가 구조적으로 재발할 수 없다, 이 메서드를 우회해서
    /// 아이템을 뽑는 다른 경로를 만들지 말 것). 2026-09-07 확인(PM 지시) — **원작도 빈
    /// 풀 방어 코드가 없다**(그냥 뽑고 엔진 네이티브 동작에 맡긴다). 그래서 우리도 별도
    /// 처리를 안 붙였다 — null을 그대로 쓰면 충분하다(호출부가 이미 null-tolerant).
    /// </summary>
    public ItemData Roll(bool useReducedPool, ISet<ItemData> excluded = null)
    {
        List<Entry> pool = useReducedPool ? reducedPool : fullPool;
        if (pool == null || pool.Count == 0) return null;

        float total = 0f;
        foreach (Entry e in pool)
            if (e != null && e.item != null && (excluded == null || !excluded.Contains(e.item))) total += e.weight;
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (Entry e in pool)
        {
            if (e == null || e.item == null) continue;
            if (excluded != null && excluded.Contains(e.item)) continue;
            cumulative += e.weight;
            if (roll <= cumulative) return e.item;
        }

        // 부동소수점 오차로 못 걸렸을 때의 방어적 fallback — 마지막 유효(미제외) 항목.
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] == null || pool[i].item == null) continue;
            if (excluded != null && excluded.Contains(pool[i].item)) continue;
            return pool[i].item;
        }
        return null;
    }
}
