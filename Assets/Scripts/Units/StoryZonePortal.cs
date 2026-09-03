using UnityEngine;

// 레인 필드 순찰 경로(오른쪽 위 모서리) 위에 서서, 플레이어 유닛이 밟으면 스토리존으로
// 보낸다. UnitPortal·ResourcePortal은 위습을 소모해서 보상을 지급하는데, 이건 필드에 이미
// 있는 유닛을 그대로 옮기기만 하는 완전히 다른 동작이라 새 컴포넌트로 뗐다.
// 적은 걸러야 한다 — 흙길 위에 있어서 순찰하는 적이 반드시 지나간다. OwnedByPlayer가
// 없으면(EnemyDummy가 그렇다) 무시한다.
// 돌아오는 길은 없다(2026-09-03, 의도적 — 스토리존→레인 복귀는 별도 작업).
[RequireComponent(typeof(Collider))]
public class StoryZonePortal : MonoBehaviour
{
    [SerializeField] Vector3 destination;

    public void SetDestination(Vector3 position)
    {
        destination = position;
    }

    // TODO(멀티): UnitPortal과 같은 이유로 서버 권위로 옮겨야 한다.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out OwnedByPlayer owner)) return; // 적 유닛에는 이게 없다
        if (!other.TryGetComponent(out UnitCombat combat)) return;

        combat.SnapTo(destination);
    }
}
