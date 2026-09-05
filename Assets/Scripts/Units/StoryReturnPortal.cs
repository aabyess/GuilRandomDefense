using UnityEngine;

// 스토리존 → 레인 복귀 포탈. StoryZonePortal(레인→스토리존)의 정반대 방향과 정확히 같은
// 일을 한다 — 다른 점은 목적지가 소유자(OwnerId)별로 다르다는 것뿐이다(레인 4개, 각자
// 자기 레인 한가운데로). 배치(포탈 위치·레인 중심 좌표 4개)는 MapGenerator 몫이라 여기선
// 배열 필드만 둔다(PM 지시 2026-09-05, 사장님 요청으로 스토리존에 큰 포탈 하나).
[RequireComponent(typeof(Collider))]
public class StoryReturnPortal : MonoBehaviour
{
    // 인덱스 = OwnerId(레인 번호와 같다). MapGenerator가 4칸을 채워 넣는다.
    [SerializeField] Vector3[] destinationsByOwnerId = new Vector3[0];

    public void SetDestinations(Vector3[] destinations)
    {
        destinationsByOwnerId = destinations;
    }

    // TODO(멀티): StoryZonePortal과 같은 이유로 서버 권위로 옮겨야 한다.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out OwnedByPlayer owner)) return; // 적 유닛에는 이게 없다
        if (!other.TryGetComponent(out UnitCombat combat)) return;

        int ownerId = owner.OwnerId;
        if (destinationsByOwnerId == null || ownerId < 0 || ownerId >= destinationsByOwnerId.Length)
        {
            Debug.LogWarning($"StoryReturnPortal: 플레이어 {ownerId}의 복귀 지점이 배선되지 않았습니다.", this);
            return;
        }

        // SnapTo는 NavMesh에 못 올리면 아무것도 안 바꾸고 조용히 false만 돌려준다
        // (UnitCombat.SnapTo 주석 참고) — "포탈 탔는데 안 감"이 소리 없는 버그가 되지
        // 않도록 플레이어에게 알린다(PM 지시 2026-09-05).
        if (!combat.SnapTo(destinationsByOwnerId[ownerId]))
        {
            PlayerNotification.Show(ownerId, "복귀 지점 근처에 설 자리가 없습니다.");
        }
    }
}
