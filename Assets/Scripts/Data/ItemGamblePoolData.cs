using System.Collections.Generic;
using UnityEngine;

// 원작 아이템 도박(H0BS "메타몽" 판매, ITEM_POOL_FULL_CENSUS.md §① 재확인)의 두 풀을 담는다 —
// itpool[0..4](전체 22종) · itpool[5..10](축소 13종, 전설·초월·희귀 상위만). 원작은 플레이어마다
// 풀 사본을 따로 쓰지만(공유 소모 아님) 이 자산 자체는 풀 "정의"(고정 데이터)라 플레이어별
// 사본이 필요 없다 — 소모되는 건 재고 카운터(ItemGambleStock)뿐, 풀 구성 자체는 안 줄어든다.
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
    /// 하나를 뽑는다. 풀이 비어 있거나 총 가중치가 0 이하면 null — GachaTable.Roll()과 같은
    /// 방어 규칙이다(뿌리: GachaTable.weight가 실제로 안 읽히는 경로가 있었던 전례 — 여기는
    /// Roll()이 유일한 진입점이라 그 문제가 구조적으로 재발할 수 없다, 이 메서드를 우회해서
    /// 아이템을 뽑는 다른 경로를 만들지 말 것).
    /// </summary>
    public ItemData Roll(bool useReducedPool)
    {
        List<Entry> pool = useReducedPool ? reducedPool : fullPool;
        if (pool == null || pool.Count == 0) return null;

        float total = 0f;
        foreach (Entry e in pool)
            if (e != null && e.item != null) total += e.weight;
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (Entry e in pool)
        {
            if (e == null || e.item == null) continue;
            cumulative += e.weight;
            if (roll <= cumulative) return e.item;
        }

        // 부동소수점 오차로 못 걸렸을 때의 방어적 fallback — 마지막 유효 항목.
        for (int i = pool.Count - 1; i >= 0; i--)
            if (pool[i] != null && pool[i].item != null) return pool[i].item;
        return null;
    }
}
