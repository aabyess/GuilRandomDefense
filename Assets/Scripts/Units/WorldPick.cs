using UnityEngine;

/// <summary>
/// 화면의 한 점이 월드의 어디를 가리키는지 찾는다. 선택·이동·스킬 대상 지정이 전부 이걸 쓴다.
///
/// 예전엔 호출부마다 <c>Physics.Raycast(ray, out hit, 200f)</c>로 거리를 박아뒀는데,
/// 카메라가 기울어져 있어서 <b>화면 위쪽을 찍을수록 광선이 길어진다</b> — 카메라 높이가 144일 때
/// 화면 위쪽 끝은 340이 넘는다. 레인을 키워 카메라가 높아지자 화면 위쪽 절반이 통째로
/// 안 잡혔고, 유닛이 선택도 이동도 안 되는 것처럼 보였다.
///
/// 거리를 재지 않는다. 맵 밖에는 콜라이더가 없으므로 끝까지 쏴도 잡을 것이 없다.
/// </summary>
public static class WorldPick
{
    public static bool TryHit(Camera cam, Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;

        return Physics.Raycast(cam.ScreenPointToRay(screenPosition), out hit, Mathf.Infinity);
    }
}
