using System.Collections.Generic;
using UnityEngine;

// TODO(멀티플레이): 입출고도 서버 권위로 옮겨야 한다. 지금은 로컬에서 직접 SetActive/이동시킨다.
public class Warehouse : MonoBehaviour
{
    [SerializeField] int ownerPlayerId;

    // 0이면 무제한 (한도 미정, 나중에 확정되면 값만 채우면 됨).
    [SerializeField] int capacity;

    readonly List<GameObject> stored = new List<GameObject>();

    public int OwnerPlayerId => ownerPlayerId;
    public IReadOnlyList<GameObject> Stored => stored;

    // 유닛이 스탯·버프 등 런타임 상태를 갖고 있어, 파괴 후 데이터만 남기기보다
    // 비활성화한 채로 실제 GameObject를 들고 있는 쪽이 상태 유실이 없어 안전하다고 판단했다.
    // (개수가 아주 많아지면 메모리 부담이 생길 수 있으니, 보관 한도가 확정되면 그걸로 조절)
    public bool Store(GameObject unit)
    {
        if (unit == null || stored.Contains(unit)) return false;

        if (!unit.TryGetComponent(out OwnedByPlayer owner) || owner.OwnerId != ownerPlayerId)
        {
            Debug.LogWarning($"Warehouse: {unit.name}은(는) 이 창고(플레이어 {ownerPlayerId}) 소유가 아니라 보관할 수 없습니다.");
            return false;
        }

        if (capacity > 0 && stored.Count >= capacity)
        {
            Debug.LogWarning($"Warehouse: 보관 한도({capacity})에 도달했습니다.");
            return false;
        }

        unit.transform.SetParent(transform);
        unit.SetActive(false);
        stored.Add(unit);
        return true;
    }

    public bool Retrieve(GameObject unit, Vector3 position)
    {
        if (unit == null || !stored.Remove(unit)) return false;

        unit.transform.SetParent(null);
        unit.transform.position = position;
        unit.SetActive(true);
        return true;
    }
}
