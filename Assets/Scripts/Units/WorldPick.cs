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

    // RaycastAll이 담을 자리. 이동 명령마다 새로 할당하지 않으려고 재사용한다.
    static readonly RaycastHit[] hits = new RaycastHit[32];

    /// <summary>
    /// <b>땅</b>을 찾는다 — 유닛·적은 통과시킨다.
    ///
    /// 이동 명령은 "저 바닥으로 가라"는 뜻이지 "저 유닛을 클릭했다"가 아니다. 그런데 캐릭터가
    /// 키 20이라 화면에서 차지하는 면적이 크고, 위습은 여럿이 뭉쳐 있다 — 커서와 목적지 사이에
    /// 자기 유닛이 끼면 광선이 거기서 멈춰서, 그 유닛의 몸통 좌표를 목적지로 삼으려다 실패한다.
    /// 화면상 자기 부대 너머를 못 찍는 셈이라, 뭉쳐 있을수록 조작이 막힌다.
    /// </summary>
    public static bool TryHitGround(Camera cam, Vector2 screenPosition, out RaycastHit hit)
    {
        hit = default;
        if (cam == null) return false;

        int count = Physics.RaycastNonAlloc(cam.ScreenPointToRay(screenPosition), hits, Mathf.Infinity);
        if (count == 0) return false;

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (IsCharacter(hits[i].collider)) continue;
            if (found && hits[i].distance >= hit.distance) continue;

            hit = hits[i];
            found = true;
        }

        if (found) return true;

        // 온통 유닛뿐이면 제일 가까운 것이라도 준다 — 아무것도 안 주는 것보다는 낫다.
        hit = hits[0];
        for (int i = 1; i < count; i++)
            if (hits[i].distance < hit.distance) hit = hits[i];

        return true;
    }

    static bool IsCharacter(Collider collider)
    {
        if (collider == null) return true;
        return collider.GetComponent<Selectable>() != null || collider.GetComponent<EnemyDummy>() != null;
    }
}
