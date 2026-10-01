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

    static bool LaneHasBoss(int laneIndex)
    {
        foreach (EnemyDummy enemy in EnemyDummy.Active)
            if (enemy != null && enemy.LaneIndex == laneIndex && enemy.IsBoss) return true;
        return false;
    }

    // TODO(멀티): UnitPortal과 같은 이유로 서버 권위로 옮겨야 한다.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out OwnedByPlayer owner)) return; // 적 유닛에는 이게 없다
        if (!other.TryGetComponent(out UnitCombat combat)) return;

        // 원작 Trig_story_move1re1(j:6158-6168): 그 플레이어 레인(p?_life_zone)에 보스급 적(UNIT_TYPE_ANCIENT, 플레이어 0의 적)이 있으면
        // 「보스/미션중에는 스토리에 진입이 불가능합니다」(주황, 1초)를 띄우고 보내지 않는다.
        // ⚠️ 우리 「미션」(해적단 퇴치 미니보스 등 EnemyData.rewardsKillerOnly)은 레인 소속이 없어(LaneIndex −1) 여기선 못 센다 — 레인 보스만.
        if (LaneHasBoss(owner.OwnerId))
        {
            PlayerNotification.Show(owner.OwnerId, "<color=#FF8200>보스/미션중에는 스토리에 진입이 불가능합니다</color>", 1f);
            return;
        }

        // SnapTo는 NavMesh에 못 올리면 아무것도 안 바꾸고 조용히 false만 돌려준다
        // (UnitCombat.SnapTo 주석 참고) — StoryReturnPortal과 같은 이유로 플레이어에게
        // 알린다(PM 지시, 2026-09-05, 버그 #7).
        if (!combat.SnapTo(destination))
        {
            PlayerNotification.Show(owner.OwnerId, "스토리존 근처에 설 자리가 없습니다.");
        }
    }
}
