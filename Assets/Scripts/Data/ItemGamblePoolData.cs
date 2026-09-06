using System.Collections.Generic;
using UnityEngine;

// 원작 아이템 도박(H0BS "메타몽" 판매, ITEM_POOL_FULL_CENSUS.md §① 재확인)의 두 풀을 담는다 —
// itpool[0..4](전체 22종, 가중치 1.25~2) · itpool[5..10](축소 13종, 전설·초월·희귀 상위만,
// 특별함 전부 제외, 가중치 1.0/0.8). 원작은 플레이어마다 풀 사본을 따로 쓰지만(공유 소모 아님)
// 이 자산 자체는 풀 "정의"(고정 데이터)라 플레이어별 사본이 필요 없다 — 소모되는 건 재고
// 카운터(ItemGambleStock)뿐, 풀 구성 자체는 안 줄어든다.
//
// ⚠️ 가중치 [미확인] — 원작 조사가 "전체 풀 1.25~2 사이" "축소 풀 1.0/0.8"이라는 범위만
// 확정했고, 어느 아이템이 정확히 몇을 받는지 항목별 매핑은 아직 안 나왔다. 지금 이 자산에
// 채운 값은 실제 원작 분포가 아니라 "메커니즘이 가중치를 실제로 읽는지"를 검증하기 위한
// 자리채움이다 — 리서치담당이 항목별 정확한 가중치를 확정하면 그 값으로 교체해야 한다.
// 지어낸 값으로 실제 밸런스를 잰 것처럼 쓰지 말 것.
//
// ⚠️ 축소 풀 구성 [미확인 — 개수 불일치]: 등급만으로 "특별함 제외"를 적용하면 22종 중
// 특별함 8종을 뺀 14종이 남는데, 원작 조사 문서는 "13종"이라고 적었다. 어느 항목이
// 추가로 빠지는지 문서에 안 나와 있어 임의로 하나를 못 뺐다 — 지금은 14종으로 채워두고
// 이 불일치를 그대로 남긴다. 리서치담당 확인 후 정정할 것.
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
    [SerializeField] List<Entry> reducedPool = new List<Entry>();  // itpool[5..10], 14종(위 불일치 참고)

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
