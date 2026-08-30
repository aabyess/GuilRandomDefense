using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 워크래프트·스타·롤 방식의 전략 게임 카메라.
/// 화면 가장자리로 마우스를 밀거나 WASD/방향키로 이동하고, 휠로 확대·축소한다.
/// 카메라는 기울기를 유지한 채 수평으로만 움직인다(회전 없음).
/// </summary>
public class RtsCameraController : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 60f;
    [SerializeField] float edgeThickness = 12f;      // 화면 가장자리에서 몇 픽셀 안쪽까지를 밀기 영역으로 볼지
    [SerializeField] bool edgeScrollEnabled = true;

    [Header("확대·축소")]
    [SerializeField] float zoomSpeed = 40f;
    [SerializeField] float minHeight = 20f;
    [SerializeField] float maxHeight = 220f;

    [Header("이동 범위")]
    [SerializeField] Vector2 boundsMin = new Vector2(-220f, -220f);
    [SerializeField] Vector2 boundsMax = new Vector2(220f, 220f);

    [Header("감속")]
    [SerializeField, Range(0f, 1f)] float smoothing = 0.15f;

    Vector3 velocity;

    void Update()
    {
        Vector3 target = transform.position
                       + PlanarMove() * (moveSpeed * HeightScale() * Time.deltaTime)
                       + Vector3.up * (ZoomDelta() * zoomSpeed);

        target.x = Mathf.Clamp(target.x, boundsMin.x, boundsMax.x);
        target.z = Mathf.Clamp(target.z, boundsMin.y, boundsMax.y);
        target.y = Mathf.Clamp(target.y, minHeight, maxHeight);

        transform.position = smoothing > 0f
            ? Vector3.SmoothDamp(transform.position, target, ref velocity, smoothing)
            : target;
    }

    // 높이 있을수록 화면에 담기는 범위가 넓으니, 같은 조작에도 더 많이 움직여야 속도가 일정하게 느껴진다.
    float HeightScale() => Mathf.Clamp(transform.position.y / minHeight, 1f, 4f);

    Vector3 PlanarMove()
    {
        Vector2 input = KeyboardInput() + EdgeInput();
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        // 카메라가 기울어져 있어도 이동은 수평면 기준이라, forward의 y 성분을 버린 방향을 쓴다.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    static Vector2 KeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        return input;
    }

    Vector2 EdgeInput()
    {
        if (!edgeScrollEnabled || Mouse.current == null) return Vector2.zero;

        Vector2 position = Mouse.current.position.ReadValue();

        // 커서가 창 밖으로 나가면 좌표가 화면 범위를 벗어난다. 그대로 두면 다른 창을 보는 동안
        // 카메라가 계속 흘러가므로, 범위 밖이면 밀기를 멈춘다.
        if (position.x < 0f || position.y < 0f || position.x > Screen.width || position.y > Screen.height)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (position.x <= edgeThickness) input.x -= 1f;
        if (position.x >= Screen.width - edgeThickness) input.x += 1f;
        if (position.y <= edgeThickness) input.y -= 1f;
        if (position.y >= Screen.height - edgeThickness) input.y += 1f;
        return input;
    }

    static float ZoomDelta()
    {
        if (Mouse.current == null) return 0f;
        // 휠 한 칸이 플랫폼마다 크기가 달라, 방향만 취하고 속도는 zoomSpeed로 정한다.
        return -Mathf.Sign(Mouse.current.scroll.ReadValue().y) * Mathf.Min(Mathf.Abs(Mouse.current.scroll.ReadValue().y), 1f);
    }
}
