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
    }
}
