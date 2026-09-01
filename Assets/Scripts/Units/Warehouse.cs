using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 플레이어별 창고 섬. 유닛을 "보관"하는 게 아니라 <b>그 섬으로 보낸다</b>.
///
/// 처음엔 비활성화해서 목록으로만 들고 있었는데, 사용자 설명은 "C키를 누르면 저 섬으로 간다"였다.
/// 눈에 보이는 곳에 서 있어야 몇 기를 치워뒀는지 알 수 있고, 다시 꺼낼 때도 그냥 고르면 된다.
///
/// 이 컴포넌트는 창고 섬 위에 붙는다 — 자기 위치가 곧 보낼 곳이다.
/// </summary>
public class Warehouse : MonoBehaviour
{
    [SerializeField] int ownerPlayerId;

    // 0이면 무제한 (한도 미정, 확정되면 값만 채우면 됨).
    [SerializeField] int capacity;

    [SerializeField] float placementRadius = 10f;   // 섬 안에서 흩어 놓을 반경

    readonly List<GameObject> stored = new List<GameObject>();

    public int OwnerPlayerId => ownerPlayerId;

    public IReadOnlyList<GameObject> Stored
    {
        get
        {
            PruneDestroyed();
            return stored;
        }
    }

    /// <summary>이 개체를 창고가 들고 있는지. 조합이 "필드 유닛보다 창고 유닛을 먼저 소모"하려고 물어본다.</summary>
    public bool Contains(GameObject unit)
    {
        // 파괴된 Object끼리는 Unity의 비교에서 서로 같아진다 — 목록에 파괴된 참조가 남아 있으면
        // 파괴된 유닛으로 물어봤을 때 엉뚱하게 true가 나온다. 매 프레임 도는 경로가 아니라 그냥 정리하고 답한다.
        PruneDestroyed();
        return stored.Contains(unit);
    }

    // 창고에 있던 유닛이 조합·연금술로 소모되면 목록에 파괴된 참조가 남는다. 창고는 그 파괴를
    // 알 방법이 없으니(구독을 걸면 결합만 는다) 읽고 쓰는 지점에서 걷어낸다 —
    // SelectionManager.PruneDestroyed와 같은 방식이다.
    void PruneDestroyed()
    {
        for (int i = stored.Count - 1; i >= 0; i--)
            if (stored[i] == null) stored.RemoveAt(i);
    }

    /// <summary>유닛을 창고 섬으로 보낸다.</summary>
    public bool Store(GameObject unit)
    {
        PruneDestroyed();

        if (unit == null || stored.Contains(unit)) return false;

        if (!unit.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId != ownerPlayerId)
        {
            Debug.LogWarning($"Warehouse: {unit.name}은(는) 이 창고(플레이어 {ownerPlayerId}) 소유가 아니라 보낼 수 없습니다.");
            return false;
        }

        if (capacity > 0 && stored.Count >= capacity)
        {
            Debug.LogWarning($"Warehouse: 보관 한도({capacity})에 도달했습니다.");
            return false;
        }

        Teleport(unit, ScatterPoint(stored.Count));
        stored.Add(unit);
        return true;
    }

    /// <summary>창고에 있던 유닛을 지정한 곳으로 되돌린다.</summary>
    public bool Retrieve(GameObject unit, Vector3 position)
    {
        PruneDestroyed();

        if (unit == null || !stored.Remove(unit)) return false;

        Teleport(unit, position);
        return true;
    }

    // 창고 섬은 바다로 둘러싸여 있어 지상 유닛은 걸어서 오갈 수 없다. 순간이동이 유일한 방법이다.
    static void Teleport(GameObject unit, Vector3 destination)
    {
        // NavMeshAgent는 내부에 자기 위치를 따로 들고 있어서 transform만 옮기면 어긋난다.
        // Warp를 써야 NavMesh 상의 위치까지 같이 옮겨진다.
        if (unit.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
        {
            agent.Warp(destination);
            agent.ResetPath();   // 옮기기 전 목적지를 계속 쫓아가지 않게
        }
        else
        {
            unit.transform.position = destination;
        }
    }

    // 한 점에 몰아 놓으면 서로 밀어내느라 흩어진다. 나선으로 자리를 벌려 놓는다.
    Vector3 ScatterPoint(int index)
    {
        if (index == 0) return transform.position;

        float angle = index * 2.399963f;                       // 황금각 — 고르게 퍼진다
        float distance = placementRadius * Mathf.Sqrt(index / 24f);
        return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Min(distance, placementRadius);
    }
}
