using UnityEngine;

public class GachaController : MonoBehaviour
{
    [SerializeField] GachaTable gachaTable;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] int rollCost = 60;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] Transform spawnPoint;
    [SerializeField] UnitInventory unitInventory;

    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    UnitInventory Inventory => unitInventory != null ? unitInventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;

    public UnitData TryRoll()
    {
        GoldWallet wallet = Wallet;
        if (gachaTable == null || wallet == null) return null;

        UnitData result = gachaTable.Roll();
        if (result == null)
        {
            Debug.LogWarning("GachaController: 뽑기 결과가 없어 골드를 차감하지 않았습니다.");
            return null;
        }

        if (!wallet.TrySpend(rollCost))
        {
            Debug.Log("골드가 부족합니다.");
            return null;
        }

        Debug.Log($"뽑기 결과: {result.unitName} ({result.grade})");
        SpawnResult(result);
        return result;
    }

    void SpawnResult(UnitData result)
    {
        if (unitSpawner != null)
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            unitSpawner.Spawn(result, position, LocalPlayer.LocalPlayerId);
        }
        else
        {
            Debug.LogWarning("GachaController: unitSpawner가 비어있어 필드에 소환하지 못했습니다.");
        }

        UnitInventory inventory = Inventory;
        if (inventory != null)
            inventory.Add(result);
        else
            Debug.LogWarning("GachaController: unitInventory가 비어있어 인벤토리에 추가하지 못했습니다.");
    }
}
