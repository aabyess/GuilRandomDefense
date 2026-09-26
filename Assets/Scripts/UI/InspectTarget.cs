using UnityEngine;

/// <summary>
/// 「살펴보기」 대상 — 조작은 못 하지만 클릭하면 정보가 보이는 것(적 · 조합표 인형). 친구 베타 피드백 ⑤(2026-09-26).
/// 선택(SelectionManager.Selected)과 따로 둔다 — 선택 목록에 넣으면 이동·공격 명령이 그쪽으로 가 버린다.
/// SelectionManager가 좌클릭으로 세우고 지우며, GameHud 선택 정보칸이 선택이 없을 때 이걸 보여 준다.
/// </summary>
public static class InspectTarget
{
    static GameObject current;

    /// <summary>살펴보는 대상. 적이 죽어 파괴되면 유니티 null이 되어 저절로 비는 것으로 보인다.</summary>
    public static GameObject Current => current != null ? current : null;

    public static void Set(GameObject target) => current = target;
    public static void Clear() => current = null;

    /// <summary>광선이 맞은 콜라이더에서 살펴볼 대상을 찾는다 — 적(EnemyDummy) 또는 조합표 인형(DollInfo).</summary>
    public static GameObject FindFrom(Collider hit)
    {
        if (hit == null) return null;
        EnemyDummy enemy = hit.GetComponentInParent<EnemyDummy>();
        if (enemy != null) return enemy.gameObject;
        DollInfo doll = hit.GetComponentInParent<DollInfo>();
        return doll != null ? doll.gameObject : null;
    }
}
