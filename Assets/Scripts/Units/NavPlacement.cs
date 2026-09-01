using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent를 확실히 NavMesh 위에 올려놓는다.
///
/// <c>Instantiate</c>로 좌표만 주고 놓으면, 그 자리가 NavMesh에서 조금만 벗어나도 에이전트가
/// 붙지 않는다. 그 상태에서 <c>SetDestination</c>은 <b>아무 일도 하지 않고 조용히 실패한다</b> —
/// 화면에는 "클릭이 안 먹는다"로만 보인다. 실제로 위습이 이것 때문에 안 움직였다.
/// </summary>
public static class NavPlacement
{
    // 원하는 자리에서 이만큼 안에 NavMesh가 있으면 거기로 끌어다 놓는다.
    // 너무 넓게 잡으면 벽 너머로 순간이동하므로 몸집 정도로만 둔다.
    const float SearchRadius = 6f;

    /// <summary>agent를 position 근처의 NavMesh 위에 올린다. 못 올리면 false.</summary>
    public static bool Place(NavMeshAgent agent, Vector3 position)
    {
        if (agent == null) return false;

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, SearchRadius, agent.areaMask))
        {
            Debug.LogWarning($"NavPlacement: {agent.name}을(를) {position} 근처 {SearchRadius} 안의 " +
                             "NavMesh에 올리지 못했습니다. 그 자리에 길이 안 구워졌는지 확인하세요.", agent);
            return false;
        }

        // Warp는 경로를 버리고 좌표를 다시 붙인다. transform.position만 바꾸면 에이전트는
        // 원래 자리에 있다고 믿은 채로 남는다.
        return agent.Warp(hit.position);
    }

    /// <summary>에이전트가 있으면 NavMesh 위로, 없으면 좌표만 옮긴다.</summary>
    public static void PlaceObject(GameObject instance, Vector3 position)
    {
        if (instance == null) return;

        if (instance.TryGetComponent(out NavMeshAgent agent) && Place(agent, position)) return;

        instance.transform.position = position;
    }
}
