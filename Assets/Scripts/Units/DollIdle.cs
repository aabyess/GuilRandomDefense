using UnityEngine;

/// <summary>
/// 조합표·부스에 서 있는 전시용 인형의 Animator를 **실행 때만** 켠다.
///
/// 왜 — 인형은 맵 생성기가 편집 모드에서 Idle 자세를 한 번 뼈에 써서 세워 둔다.
/// 그런데 Animator가 켜진 채 저장되면 씬 로드·프리팹 갱신 때 다시 바인드하면서
/// 그 자세를 기본 자세(T자)로 되돌릴 수 있다. 그래서 편집 모드에선 꺼 두고,
/// 게임이 시작될 때 여기서 켠다. 컨트롤러 기본 상태가 Idle이라 켜기만 하면 선다.
///
/// 게임 로직은 전혀 없다. 인형에서 다른 스크립트는 전부 걷어내고 이것만 남긴다.
/// </summary>
[DisallowMultipleComponent]
public class DollIdle : MonoBehaviour
{
    void Start()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.enabled = true;
    }
}
