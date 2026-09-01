using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레인 섬에 붙어 "이 섬이 몇 번 레인인가"를 런타임에 알려준다.
/// 레인 좌표는 MapLayout(에디터 전용)에만 있어서, 게임 쪽에서는 이 표식으로 찾는다.
/// </summary>
public class LaneMarker : MonoBehaviour
{
    [SerializeField] int laneIndex;

    // 새로 만들어진 유닛이 처음 서는 자리. 레인 아래 상점 줄 위의 우리다 —
    // 레인 한가운데에 떨어뜨리면 적 한복판에 나오고, 플레이어가 손쓸 새 없이 맞는다.
    [SerializeField] Transform unitPen;

    static readonly List<LaneMarker> registry = new List<LaneMarker>();

    public int LaneIndex => laneIndex;

    /// <summary>이 레인 소유 유닛이 새로 생겨날 자리. 우리가 없으면 레인 한가운데.</summary>
    public Vector3 SpawnPosition => unitPen != null ? unitPen.position : transform.position;

    public void SetLaneIndex(int index)
    {
        laneIndex = index;
    }

    public void SetUnitPen(Transform pen)
    {
        unitPen = pen;
    }

    void OnEnable() => registry.Add(this);
    void OnDisable() => registry.Remove(this);

    public static LaneMarker Get(int laneIndex)
    {
        foreach (LaneMarker marker in registry)
            if (marker.laneIndex == laneIndex)
                return marker;

        return null;
    }
}
