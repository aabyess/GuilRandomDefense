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
///
/// 🔴 사람형(Humanoid) 인형만 켠다(2026-09-15). 사장님 「게임 시작하면 조합판에 이상한 물체」 —
///    해적선 인형(히든 조합식 재료, Generic)이 실행 때 자기 클립 Idle_Bob을 돌리자 돛·밧줄이 판 전체로 퍼졌다.
///    편집 모드 점검(크기·사진)은 Animator가 꺼져 있어 한 번도 못 잡았다. Generic 인형(배·짐승)은
///    맵 생성기가 세워 둔 자세 그대로 둔다 — 전시용이라 움직이지 않아도 된다.
/// </summary>
[DisallowMultipleComponent]
public class DollIdle : MonoBehaviour
{
    void Start()
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.enabled = animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
    }
}
