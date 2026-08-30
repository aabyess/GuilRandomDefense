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

        return instance;
    }

    static int ComputeAreaMask(MovementAbility ability)
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
