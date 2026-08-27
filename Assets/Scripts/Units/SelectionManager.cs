using UnityEngine;
using UnityEngine.InputSystem;

// M1 뼈대: 단일 클릭 선택만 처리. 드래그 박스 다중 선택은 M2에서 확장.
public class SelectionManager : MonoBehaviour
{
    Camera cam;
    Selectable current;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectAtCursor();
        }
    }

    void TrySelectAtCursor()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Selectable hitSelectable = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            hit.collider.TryGetComponent(out hitSelectable);

        if (current != null)
            current.SetSelected(false);

        current = hitSelectable;

        if (current != null)
            current.SetSelected(true);
    }
}
