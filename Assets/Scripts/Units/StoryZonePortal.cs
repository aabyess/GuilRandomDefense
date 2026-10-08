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

    // 사장님 10-08 「스토리로 보낸 유닛이 적에 너무 붙는다 — 공격 범위 최대한 끝에서 바로 때리게」: 착지 지점은 레인마다 고정(로스터 최소 사거리의 0.8 = 사거리가 짧은 유닛도 닿게)이라
    // 사거리가 긴 유닛은 이미 한참 안에서 내리고 때렸다. 이제 같은 방향(레인 귀퉁이)으로 유닛마다 자기 사거리(×0.97) 거리에 내린다 — 사거리 안에 들어오는 순간 서서 바로 친다.
    // 근접(사거리가 기본 착지 거리보다 짧은 유닛)은 기본 착지 거리에 내려 걸어 붙는다. 존 중심이 안 채워진 옛 씬은 예전 동작.
    [SerializeField] Vector3 storyCenter;
    [SerializeField] float maxLandingRadius;

    public void SetStoryZone(Vector3 center, float maxRadius)
    {
        storyCenter = center;
        maxLandingRadius = maxRadius;
    }

    Vector3 LandingFor(UnitCombat combat)
    {
        if (maxLandingRadius <= 0f) return destination;
        Vector3 flat = new Vector3(destination.x - storyCenter.x, 0f, destination.z - storyCenter.z);
        float baseDistance = flat.magnitude;
        if (baseDistance < 0.01f) return destination;
        float range = combat.TryGetComponent(out UnitAttacker attacker) ? attacker.AttackRange : 0f;
        float distance = Mathf.Clamp(range * 0.97f, baseDistance, maxLandingRadius);
        Vector3 spot = storyCenter + flat / baseDistance * distance;
        spot.y = destination.y;
        return spot;
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
        if (!combat.SnapTo(LandingFor(combat)))
        {
            PlayerNotification.Show(owner.OwnerId, "스토리존 근처에 설 자리가 없습니다.");
        }
    }
}
