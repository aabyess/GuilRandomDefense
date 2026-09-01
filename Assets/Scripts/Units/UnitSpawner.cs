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
            agent.areaMask = ComputeAreaMask(data.movementAbility);
            // 아군끼리 완전히 겹치면 몇 마리인지 안 보이고, 제대로 밀어내면 뭉치질 못한다.
            // 반경을 작게(0.28) 두고 회피는 가장 싼 단계만 켜서 '살짝 비켜주는' 정도로 맞춘다.
            // 회피 비용은 주변 에이전트 수에 비례하므로 한 레인에 수십 마리가 모이는 이 게임에서
            // 높은 품질을 쓰면 그 자체가 부담이 된다.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.radius = 0.28f;
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
