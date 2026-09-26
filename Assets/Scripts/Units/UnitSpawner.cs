using UnityEngine;
using UnityEngine.AI;

public class UnitSpawner : MonoBehaviour
{
    const string SeaAreaName = "Sea";

    // TODO(멀티): 이 메서드 내부를 서버 권위 호출로 교체하면 됨 — MULTIPLAYER_MIGRATION.md "전환 순서" 4번 참고.
    public GameObject Spawn(UnitData data, Vector3 position, int ownerId)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning("UnitSpawner: UnitData 또는 prefab이 비어있어 소환할 수 없습니다.");
            return null;
        }

        GameObject instance = Instantiate(data.prefab, position, Quaternion.identity);

        if (!instance.TryGetComponent(out OwnedByPlayer owner))
            owner = instance.AddComponent<OwnedByPlayer>();
        owner.SetOwner(ownerId);

        if (!instance.TryGetComponent(out UnitIdentity identity))
            identity = instance.AddComponent<UnitIdentity>();
        identity.SetData(data);

        if (instance.TryGetComponent(out UnitAttacker attacker))
            attacker.ApplyStats(data.attackPower, data.attackRange, data.attackSpeed);

        if (instance.TryGetComponent(out NavMeshAgent agent))
        {
            agent.speed = data.moveSpeed;
            // 2026-09-26 베타 피드백 「빙판 미끄러지듯 이동」: 가속·회전이 유니티 기본값(8 · 120°/s)인데
            // 속도는 100 안팎이라, 최고 속도까지 10초 넘게 걸리고 멈출 때도 한참 미끄러졌다.
            // 워크3처럼 거의 즉시 서고 즉시 돈다 — 0.05초 만에 최고 속도, 초당 1080° 회전.
            agent.acceleration = Mathf.Max(agent.acceleration, data.moveSpeed * 20f);
            agent.angularSpeed = Mathf.Max(agent.angularSpeed, 1080f);
            agent.autoBraking = true;
            agent.areaMask = ComputeAreaMask(data.movementAbility);
            // 아군끼리 완전히 겹치면 몇 마리인지 안 보이고, 제대로 밀어내면 뭉치질 못한다.
            // 반경을 작게(0.28) 두고 회피는 가장 싼 단계만 켜서 '살짝 비켜주는' 정도로 맞춘다.
            // 회피 비용은 주변 에이전트 수에 비례하므로 한 레인에 수십 마리가 모이는 이 게임에서
            // 높은 품질을 쓰면 그 자체가 부담이 된다.
            //
            // 🔴 2026-09-24: **여기가 유닛 회피를 정하는 유일한 자리다.**
            //    `UnitMover.Awake`도 같은 값을 `None`으로 넣지만(사장님 「겹치게」 지시),
            //    `Instantiate`가 Awake를 먼저 돌리고 이 줄이 나중이라 **이 줄이 이긴다.**
            //    실측(09-24): 유닛 회피 5/5 켜짐 · 위습 0/5(위습은 여기를 안 거친다).
            //    고칠 일이 생기면 **UnitMover가 아니라 여기**를 고쳐야 한다.
            //
            //    ⚠️ 그런데 회피가 켜져 있어도 유닛은 서로 포갠다 — 실측 10/10 짝이 몸이 겹쳤다.
            //    **회피 원 0.28이 몸(≈14)의 2%**라서 서로를 거의 못 느끼기 때문이다.
            //    즉 지금의 겹침은 의도가 아니라 **두 숫자가 어긋나서 생긴 결과**다.
            //    「이동 명령 뒤에도 겹치는 게 맞나」는 사장님 답 대기 — 답이 오면 이 두 줄만 바꾸면 된다.
            //
            // ⚠️ 이 `radius`를 키울 때 **다시 구울 필요 없다.** 이름이 같은 두 숫자가 서로 다른 일을 한다:
            //      굽기   `NavMeshBuildSettings.agentRadius`(0.5) — NavMesh가 벽에서 물러나는 폭 = **통로 폭**.
            //                                                       키우면 다시 구워야 하고 걸을 수 있는 면이 줄어든다.
            //      런타임 `NavMeshAgent.radius`(여기)            — 회피 계산의 제 몸 크기. **통로를 안 좁힌다.**
            //                                                       키운 유닛은 좁은 데를 **벽에 파묻히며 지나간다.**
            //    실측(09-24): 앞치마의 6%가 통로 반폭 2 미만(우리 칸막이)이다. 그 6%는 **굽기**를 키울 때
            //    막히는 것이지 이 값과는 무관하다. 대신 이 값을 키우면 좁은 데서 유닛끼리 밀린다 — 그건 재 봐야 안다.
            // ✅ 2026-09-25 사장님 「아군 유닛들은 겹치게 해줘도 될듯」 — 답이 와서 회피를 끈다.
            //    이제 겹침은 우연(회피 원이 몸의 2%)이 아니라 의도다. UnitMover.Awake와 같은 값이다.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.radius = 0.28f;

            // areaMask를 정한 뒤에 올려야 한다 — 지상 유닛을 바다 위에 붙여놓으면 안 된다.
            // 좌표만 주고 놓으면 NavMesh에서 살짝 벗어났을 때 에이전트가 안 붙고,
            // 그 유닛은 선택은 되는데 이동 명령이 조용히 무시된다.
            NavPlacement.Place(agent, position);
        }

        // 인벤토리는 UnitData 목록이 아니라 필드 인스턴스의 등록부다(UnitInventory 참고).
        // 플레이어 유닛을 만드는 곳이 여기뿐이라, 여기가 유일한 등록 지점이다.
        UnitInventory inventory = PlayerContext.Get(ownerId)?.UnitInventory;
        if (inventory == null)
        {
            // 조용히 넘어가면 필드엔 있는데 인벤토리엔 없는 유닛이 생긴다 —
            // 바로 그 어긋남을 없애려고 등록부로 바꾼 것이라, 배선이 빠졌으면 드러나야 한다.
            Debug.LogWarning($"UnitSpawner: 플레이어 {ownerId}의 UnitInventory를 찾지 못해 {data.unitName}을(를) 등록하지 못했습니다.", this);
        }
        identity.RegisterTo(inventory);

        // 항법 "연합세력" — 원작 Trig_UnitJohabCounter_Actions 재현(RewardDistributor.
        // GrantUnionWispIfEligible 주석 참고). 여기가 유일한 플레이어 유닛 생성 지점이라
        // 조합·가챠 구분 없이 원작과 같은 범위를 덮는다.
        RewardDistributor.Instance?.GrantUnionWispIfEligible(data, ownerId);

        // 같은 원작 트리거의 전설 나미 분기(Func020) — 보물찾기 반경·보상 개수·Gold_Plus.
        TreasureHunt.Instance?.OnUnitSpawned(data, ownerId);

        return instance;
    }

    // 조합 결과를 어디에 내보낼지 고를 때 CombineSystem도 같은 마스크로 NavMesh를 검사해야 한다 —
    // 지상 유닛의 자리를 바다 위에서 찾으면 안 된다.
    public static int ComputeAreaMask(MovementAbility ability)
    {
        int seaArea = NavMesh.GetAreaFromName(SeaAreaName);
        if (seaArea < 0)
        {
            Debug.LogWarning($"UnitSpawner: NavMesh Area \"{SeaAreaName}\"를 찾을 수 없어 전체 영역을 허용합니다. Navigation 창에서 Area를 추가해주세요.");
            return NavMesh.AllAreas;
        }

        bool canCrossSea = (ability & (MovementAbility.Flying | MovementAbility.WaterWalk)) != 0;
        if (canCrossSea) return NavMesh.AllAreas;

        return NavMesh.AllAreas & ~(1 << seaArea);
    }
}
