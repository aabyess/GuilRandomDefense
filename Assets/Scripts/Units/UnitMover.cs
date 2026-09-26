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

        // 겹쳐 설 수 있게 회피를 끈다(사장님 지시 2026-09-23 "흔함 유닛 뽑았을 때 겹치게").
        // 물리는 이미 안 민다 — Rigidbody가 isKinematic이라 콜라이더가 서로 밀지 않는다.
        // **CapsuleCollider는 그대로 둔다** — 클릭 선택이 그 콜라이더를 레이캐스트로 맞힌다.
        //
        // 🔴 2026-09-24 실측: **이 줄은 유닛에 안 닿는다. 위습에만 닿는다.**
        //    `UnitSpawner`가 Instantiate **뒤에** 같은 값을 Low로 덮어쓴다(UnitSpawner.cs:38).
        //    Awake는 Instantiate 그 자리에서 돌고, 스포너 코드는 그 다음 줄이라 **스포너가 이긴다.**
        //    그래서 화면의 유닛은 회피가 **켜져 있다**(실측 5/5). 위습은 스포너를 안 거쳐 꺼져 있다(0/5).
        //    두 파일 다 근거 있는 주석을 달고 **서로 반대되는 값**을 넣고 있었다.
        //
        //    ⚠️ 그런데도 유닛은 겹친다 — **회피 원(반지름 0.28)이 몸(≈14)의 2%**라서다.
        //    회피는 돌지만 서로를 거의 못 느낀다. 즉 지금의 겹침은 이 줄 덕이 아니라 **우연**이다.
        //    「이동 명령 뒤에도 겹치는 게 맞나」는 사장님 답 대기 중이라 **동작을 안 바꾼다.**
        //    답이 오면 고칠 곳은 여기가 아니라 `UnitSpawner`의 두 줄이다(그쪽이 이기므로).
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
        // 2026-09-26: 적 몸에 우클릭하면 이동이 아니라 공격(워크3와 같다). 적 콜라이더에 정확히 맞을 때만 —
        // 레인 근처 땅을 찍으려다 적으로 빨려 들지 않게 화면 여유(허용 픽셀)는 0으로 둔다.
        // A 공격 대상 고르는 중이면 우클릭은 「취소」라 여기서도 움직이지 않는다.
        SelectionManager selection = FindFirstObjectByType<SelectionManager>();
        if (selection != null && selection.IsAttackTargeting) return;
        if (combat != null)
        {
            EnemyDummy enemy = WorldPick.TryPickEnemy(cam, Mouse.current.position.ReadValue(), 0f);
            if (enemy != null)
            {
                combat.AttackTarget(enemy);
                return;
            }
        }
        else if (!GameAuthority.IsServer && selectable != null)
        {
            // MP: 멀티 클라의 겉모습엔 UnitCombat이 없다 — 적 우클릭은 호스트에 공격 요청으로 보낸다(적은 거울 번호로).
            EnemyDummy enemy = WorldPick.TryPickEnemy(cam, Mouse.current.position.ReadValue(), 0f);
            if (enemy != null)
            {
                NetCommands.RequestUnitCommand(NetUnitCommand.AttackTarget, new[] { selectable }, enemy);
                return;
            }
        }

        // 이동 명령이 조용히 실패하면 "클릭이 안 먹는다"로만 보인다. 막힌 지점을 말하게 한다.
        // 땅만 본다. 사이에 낀 아군을 클릭 대상으로 삼으면 그 몸통 위가 목적지가 된다.
        if (!WorldPick.TryHitGround(cam, Mouse.current.position.ReadValue(), out RaycastHit hit))
        {
            Debug.Log($"[이동] {name}: 커서 아래에 아무것도 없습니다(콜라이더 없음).", this);
            return;
        }

        // MP: 멀티 클라는 땅 좌표만 호스트로 보낸다 — 실제 이동은 호스트의 실물이 아래 MoveToGroundPoint로 한다.
        //     싱글·호스트(IsServer)는 이 줄을 지나쳐 지금과 똑같이 움직인다.
        if (!GameAuthority.IsServer) { NetCommands.RequestMove(this, hit.point); return; }

        MoveToGroundPoint(hit.point, hit.collider.name);
    }

    // MP: 우클릭 경로(위)와 멀티 호스트가 받은 클라 요청(NetCommands.ExecuteMove)이 같이 쓰는 이동 본체.
    //     복사본을 두지 않으려고 TryMoveToCursor에서 떼어 냈다 — 줄 내용은 그대로다.
    public void MoveToGroundPoint(Vector3 point, string clickedName = "요청")
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.Log($"[이동] {name}: NavMesh 위에 서 있지 않아 움직일 수 없습니다 " +
                      $"(위치 {transform.position}, 에이전트 켜짐 {agent.isActiveAndEnabled}).", this);
            return;
        }

        if (!NavMesh.SamplePosition(point, out NavMeshHit navHit, DestinationSampleRadius, agent.areaMask))
        {
            Debug.Log($"[이동] {name}: 클릭한 곳({clickedName} {point})에서 " +
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
