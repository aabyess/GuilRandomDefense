using UnityEngine;

public class GachaController : MonoBehaviour
{
    [SerializeField] GachaTable gachaTable;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] int rollCost = 60;

    public UnitData TryRoll()
    {
        if (gachaTable == null || goldWallet == null) return null;

        UnitData result = gachaTable.Roll();
        if (result == null)
        {
            Debug.LogWarning("GachaController: 뽑기 결과가 없어 골드를 차감하지 않았습니다.");
            return null;
        }

        if (!goldWallet.TrySpend(rollCost))
        {
            Debug.Log("골드가 부족합니다.");
            return null;
        }

        Debug.Log($"뽑기 결과: {result.unitName} ({result.grade})");
        return result;
    }
}
