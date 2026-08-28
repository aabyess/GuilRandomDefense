using UnityEngine;
using UnityEngine.InputSystem;

// 임시 디버그용 트리거: G키로 뽑기 실행. 정식 UI가 붙으면 제거 예정.
public class DebugGachaTrigger : MonoBehaviour
{
    [SerializeField] GachaController gachaController;

    void Update()
    {
        if (Keyboard.current == null || gachaController == null) return;
        if (Keyboard.current.gKey.wasPressedThisFrame)
            gachaController.TryRoll();
    }
}
