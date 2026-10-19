using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 좌클릭 누름·뗌 이벤트를 **하나도 안 잃게** 쌓아 두는 큐(10-20 「빠르게 클릭하면 안 먹는다」).
/// 옛 방식은 매 프레임 Mouse.leftButton.wasPressedThisFrame을 읽는다 — 프레임이 길어지면(버벅임) 한 프레임 안에 누름·뗌·누름·뗌이 몰려 와도 「한 번 눌림」으로만 읽혀
/// 두 번째 클릭이 사라진다. InputAction 콜백은 이벤트마다 불리므로 모든 클릭이 순서대로 큐에 쌓이고, SelectionManager가 프레임마다 전부 꺼내 처리한다.
/// 위치는 콜백이 불린 시점의 포인터 위치(한 프레임 안 여러 이벤트면 그 프레임 마지막 위치에 가까움 — 클릭 자리 근사).
/// Input System을 못 만들면(Active false) 호출부가 옛 폴링으로 물러난다.
/// </summary>
public static class ClickQueue
{
    public struct Evt { public bool down; public Vector2 pos; public float time; }

    static readonly Queue<Evt> queue = new Queue<Evt>();
    static InputAction left;
    public static bool Active { get; private set; }

    /// <summary>시험·계측용 누적 수: Raw = InputAction이 받은 좌클릭 누름 수 · Polled = 옛 폴링(wasPressedThisFrame)이 본 프레임 수 · Handled = 큐에서 꺼내 처리한 누름 수.</summary>
    public static int RawPresses, PolledPresses, HandledPresses;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { queue.Clear(); left = null; Active = false; RawPresses = PolledPresses = HandledPresses = 0; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Init()
    {
        try
        {
            left = new InputAction("GrdLeftClick", InputActionType.Button, "<Mouse>/leftButton");
            left.started += _ => Enqueue(true);      // 누름
            left.canceled += _ => Enqueue(false);    // 뗌
            left.Enable();
            Active = true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[입력] 클릭 큐를 못 만들어 옛 폴링으로 물러납니다: {e.Message}"); Active = false; }
    }

    static void Enqueue(bool down)
    {
        Vector2 pos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        queue.Enqueue(new Evt { down = down, pos = pos, time = Time.unscaledTime });
        if (down) RawPresses++;
        if (queue.Count > 64) queue.Dequeue();   // 안전장치(소비자가 죽었을 때 무한히 안 쌓게)
    }

    /// <summary>쌓인 이벤트 하나 꺼내기.</summary>
    public static bool TryDequeue(out Evt evt)
    {
        if (queue.Count > 0) { evt = queue.Dequeue(); return true; }
        evt = default; return false;
    }

    /// <summary>큐를 비운다(채팅 입력 중 등 클릭을 버려야 할 때).</summary>
    public static void Clear() => queue.Clear();
}
