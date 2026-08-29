using UnityEngine;
using UnityEngine.InputSystem;

// TODO(멀티플레이): 창고 입출고도 서버 권위로 옮겨야 한다.
public class WarehouseController : MonoBehaviour
{
    [SerializeField] SelectionManager selectionManager;
    [SerializeField] Warehouse warehouse;

    Warehouse TargetWarehouse => warehouse != null ? warehouse : PlayerContext.Local != null ? PlayerContext.Local.Warehouse : null;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.cKey.wasPressedThisFrame) return;
        if (selectionManager == null) return;

        Warehouse target = TargetWarehouse;
        if (target == null) return;

        foreach (Selectable selectable in selectionManager.Selected)
        {
            if (selectable == null) continue;
            target.Store(selectable.gameObject);
        }

        // 보관 성공 여부와 무관하게 선택 해제 — 안 그러면 비활성화된 유닛이 계속 "선택된" 상태로 남는다.
        selectionManager.ClearSelection();
    }
}
