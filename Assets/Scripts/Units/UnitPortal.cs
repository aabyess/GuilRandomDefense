using UnityEngine;

// 포탈에 자기 등급의 위습이 들어오면 유닛을 지급하고 위습을 소모한다.
// 포탈 오브젝트에 Collider(isTrigger = true)가 필요하다.
[RequireComponent(typeof(Collider))]
public class UnitPortal : MonoBehaviour
{
    [SerializeField] UnitGrade acceptedGrade;
    [SerializeField] UnitData specificUnit; // 선택형 포탈(흔함 등)이면 지정, 비어있으면 등급 내 랜덤 지급
    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] Transform spawnPoint;

    // TODO(멀티): 포탈 진입 판정·지급은 서버 권위로 이동해야 함 — 지금은 클라이언트가 직접 처리.
    void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Wisp wisp)) return;
        if (wisp.Data == null || wisp.Data.targetGrade != acceptedGrade) return;

        UnitData reward = specificUnit != null
            ? specificUnit
            : gachaTable != null ? gachaTable.RollFromGrade(acceptedGrade) : null;

        if (reward == null)
        {
            Debug.LogWarning($"UnitPortal: {acceptedGrade} 등급에서 지급할 유닛을 찾지 못해 위습을 소모하지 않았습니다.");
            return;
        }

        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        Destroy(wisp.gameObject);

        PlayerContext.Get(ownerId)?.UnitInventory?.Add(reward);

        if (unitSpawner != null)
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            unitSpawner.Spawn(reward, position, ownerId);
        }
        else
        {
            Debug.LogWarning("UnitPortal: unitSpawner가 비어있어 필드에 소환하지 못했습니다.");
        }
    }
}
