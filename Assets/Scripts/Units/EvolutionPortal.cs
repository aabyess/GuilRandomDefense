using UnityEngine;

/// <summary>S2 진화 포탈(원작 rect CH) — 진화 위습(WispData.isEvolution)만 먹고 S2Evolution을 시작한다. 트리거 콜라이더 필요.</summary>
[RequireComponent(typeof(Collider))]
public class EvolutionPortal : MonoBehaviour
{
    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;

    void Awake()
    {
        if (unitSpawner == null) unitSpawner = FindFirstObjectByType<UnitSpawner>();
        if (gachaTable == null)
        {
            UnitPortal any = FindFirstObjectByType<UnitPortal>();
            if (any != null) gachaTable = any.Table;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out Wisp wisp) || wisp.IsConsumed || wisp.Data == null || !wisp.Data.isEvolution) return;
        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;
        if (!S2Evolution.OnWispUsed(ownerId, wisp, gachaTable, unitSpawner)) return;
        wisp.MarkConsumed();
        Destroy(wisp.gameObject);
    }
}
