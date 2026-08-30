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
            // 아군끼리는 서로 통과한다(원작과 같음). 회피를 켜두면 유닛이 많아질수록
            // 서로 밀어내느라 목적지에 못 가고, 회피 계산 자체도 유닛 수의 제곱으로 늘어난다.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
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
