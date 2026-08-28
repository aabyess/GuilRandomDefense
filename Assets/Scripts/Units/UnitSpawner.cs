using UnityEngine;
using UnityEngine.AI;

public class UnitSpawner : MonoBehaviour
{
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

        if (instance.TryGetComponent(out UnitAttacker attacker))
            attacker.ApplyStats(data.attackPower, data.attackRange, data.attackSpeed);

        if (instance.TryGetComponent(out NavMeshAgent agent))
            agent.speed = data.moveSpeed;

        return instance;
    }
}
