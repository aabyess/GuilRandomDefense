using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class UnitMover : MonoBehaviour
{
    // 클릭 지점에서 이만큼 안에 걸어갈 자리를 찾는다. 2는 너무 빡빡했다 —
    // NavMeshAgent 반지름 때문에 걸을 수 있는 면이 섬 물리 경계보다 이미 안쪽으로
    // 들어와 있어서, 눈으로 "섬 안"인 곳을 찍어도 실패하는 띠가 가장자리에 생긴다.
    // 너무 키우면 바다를 찍었는데 유닛이 근처 육지로 가버려 의도와 어긋나므로 8에서 멈춘다.
    //
    // 🔴 2026-09-23: 맵이 WorldScale.Value(4.167)배가 되면서 이 값이 옛 단위로 남아
    //    "클릭한 곳에서 2 안에 걸어갈 수 있는 자리가 없습니다"가 떴다(사장님 스크린샷).
    //    메시지의 "2"가 범인을 그대로 가리킨다 — 유닛 프리팹은 8인데 **위습 프리팹만 2**다.
    [SerializeField] float destinationSampleRadius = 8f;

    /// <summary>
    /// 실제로 쓰는 표본 반경. 프리팹에 직렬화된 값(유닛 8 · 위습 2 · 생성 프리팹 207개 전부 8)은
    /// **맵이 커지기 전 단위**라 여기서 맵 배율을 곱한다.
    ///
    /// 프리팹 209개의 숫자를 직접 고치지 않는 이유: (1) 위습이 유닛보다 촘촘하다는 관계가
    /// 곱셈으로 저절로 유지된다, (2) 배율을 또 바꿀 때 프리팹을 다시 전수로 손대지 않아도 된다,
    /// (3) 생성 프리팹은 도구가 다시 만들면 되돌아가 버린다.
    /// </summary>
    float DestinationSampleRadius => destinationSampleRadius * WorldScale.Value;

    NavMeshAgent agent;
    Camera cam;
    OwnedByPlayer owner;
    Selectable selectable;
    UnitCombat combat;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        cam = Camera.main;
        owner = GetComponent<OwnedByPlayer>();
        selectable = GetComponent<Selectable>();
        combat = GetComponent<UnitCombat>();

        // 유닛끼리 겹쳐 설 수 있게 한다(사장님 지시 2026-09-23 "흔함 유닛 뽑았을 때 겹치게",
        // 원작도 라운드몹 ucol=0·아군 15로 사실상 겹침 허용이고 같은 흔함 유닛은
        // Common_Loc 고정 슬롯에 그대로 포개 선다).
        //
        // 미는 힘은 둘일 수 있는데 실제로 도는 건 하나뿐이다:
        //  · 물리 — Rigidbody가 이미 isKinematic이라 콜라이더가 서로 밀지 않는다(확인함).
        //  · 길찾기 회피 — 프리팹 209개 전부 ObstacleAvoidanceType이 1(Low)이라 **이쪽이 범인**이다.
        // 그래서 회피만 끈다. **CapsuleCollider는 그대로 둔다** — 클릭 선택이 그 콜라이더를
        // 레이캐스트로 맞히기 때문이고, 레이캐스트는 충돌 회피와 무관하다.
        // 프리팹 209개를 고치지 않고 여기서 끄는 이유는 표본 반경 때와 같다(생성 프리팹은
        // 도구가 다시 만들면 되돌아간다).
        if (agent != null) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;
        if (owner != null && owner.OwnerId != LocalPlayer.LocalPlayerId) return;
        // 선택된 유닛만 움직인다. 이 검사가 없으면 우클릭 한 번에 내 유닛 전부가 같이 이동해서
        // 선택 자체가 의미를 잃는다.
        if (selectable != null && !selectable.IsSelected) return;
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            TryMoveToCursor();
        }
    }

    void TryMoveToCursor()
    {
        // 이동 명령이 조용히 실패하면 "클릭이 안 먹는다"로만 보인다. 막힌 지점을 말하게 한다.
        // 땅만 본다. 사이에 낀 아군을 클릭 대상으로 삼으면 그 몸통 위가 목적지가 된다.
        if (!WorldPick.TryHitGround(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            Debug.Log($"[이동] {name}: 커서 아래에 아무것도 없습니다(콜라이더 없음).", this);
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.Log($"[이동] {name}: NavMesh 위에 서 있지 않아 움직일 수 없습니다 " +
                      $"(위치 {transform.position}, 에이전트 켜짐 {agent.isActiveAndEnabled}).", this);
            return;
        }

        if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, DestinationSampleRadius, agent.areaMask))
        {
            Debug.Log($"[이동] {name}: 클릭한 곳({hit.collider.name} {hit.point})에서 " +
                      $"{DestinationSampleRadius} 안에 걸어갈 수 있는 자리가 없습니다.", this);
            return;
        }

        // UnitCombat이 있으면 그쪽에 명령을 넘겨서, 도착할 때까지 자동 추적이 끼어들지 않게 한다.
        if (combat != null)
            combat.IssueMoveCommand(navHit.position);
        else if (!agent.SetDestination(navHit.position))
            Debug.Log($"[이동] {name}: 목적지 {navHit.position} 로 가는 길을 못 찾았습니다.", this);
    }
}
