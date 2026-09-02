using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// B키로 선택한 유닛을 창고 섬으로 보내고, 창고에 있는 유닛을 고른 상태로 누르면 자기 레인으로 되돌린다.
/// 키 하나로 양방향인 이유: 창고 섬은 바다로 둘러싸여 있어 걸어서 나올 수가 없다.
/// 보내기만 되면 유닛이 영영 갇힌다.
/// 원래 C였는데 사장님이 C를 "우리로 정렬"로 명시해서 B로 옮겼다(PM 지시) — 원작 키가 따로 있으면 그때 다시 옮긴다.
/// </summary>
public class WarehouseController : MonoBehaviour
{
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] Warehouse warehouse;

    Warehouse TargetWarehouse => warehouse != null
        ? warehouse
        : PlayerContext.Local != null ? PlayerContext.Local.Warehouse : null;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame) return;
        if (selectionManager == null) return;

        Warehouse target = TargetWarehouse;
        if (target == null)
        {
            Debug.LogWarning("WarehouseController: 내 창고를 찾지 못했습니다.", this);
            return;
        }

        Vector3 laneCenter = ResolveLaneCenter();
        int sent = 0, returned = 0;

        // 바뀌는 건 창고 목록이지 선택 목록이 아니라, 도는 중에 컬렉션이 변하지 않는다.
        foreach (Selectable selectable in selectionManager.Selected)
        {
            if (selectable == null) continue;

            GameObject unit = selectable.gameObject;
            if (target.Contains(unit))
            {
                if (target.Retrieve(unit, laneCenter)) returned++;
            }
            else if (target.Store(unit))
            {
                sent++;
            }
        }

        if (sent > 0 || returned > 0)
            Debug.Log($"창고: {sent}기 보관, {returned}기 회수 (보관 중 {target.Stored.Count}기)");

        // 순간이동 후에도 선택을 유지하면 화면 밖 유닛이 선택된 채로 남아 헷갈린다.
        selectionManager.ClearSelection();
    }

    static Vector3 ResolveLaneCenter()
    {
        LaneMarker lane = LaneMarker.Get(LocalPlayer.LocalPlayerId);
        return lane != null ? lane.transform.position : Vector3.zero;
    }
}
