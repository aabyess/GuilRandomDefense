using System.Collections.Generic;
using UnityEngine;

public class CombineSystem : MonoBehaviour
{
    [SerializeField] UnitInventory inventory;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] List<CombineRecipe> recipes;

    UnitInventory Inventory => inventory != null ? inventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    ResourceWallet Resources => resourceWallet != null ? resourceWallet : PlayerContext.Local != null ? PlayerContext.Local.ResourceWallet : null;

    public List<CombineRecipe> GetAvailableRecipes()
    {
        List<CombineRecipe> available = new List<CombineRecipe>();

        if (recipes == null) return available;

        foreach (CombineRecipe recipe in recipes)
        {
            if (recipe != null && CanAfford(recipe))
            {
                available.Add(recipe);
            }
        }

        return available;
    }

    public bool TryCombine(CombineRecipe recipe)
    {
        if (recipe == null) return false;

        if (!CanAfford(recipe)) return false;

        // 전부 검사를 통과한 뒤에만 소모한다 (중간 실패로 재료만 날아가는 것 방지).
        UnitInventory targetInventory = Inventory;
        GoldWallet wallet = Wallet;
        ResourceWallet resources = Resources;

        foreach (KeyValuePair<UnitData, int> pair in AggregateIngredients(recipe))
        {
            for (int i = 0; i < pair.Value; i++)
            {
                targetInventory.Remove(pair.Key);
            }
        }

        if (recipe.goldCost > 0 && wallet != null)
        {
            wallet.TrySpend(recipe.goldCost);
        }

        if (resources != null && recipe.resourceCosts != null)
        {
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
            {
                resources.TrySpend(cost.type, cost.amount);
            }
        }

        // TODO: requiredSaveCount(영원 등급, 게임 클리어 누적 횟수) — 세이브 시스템 없어 아직 검사하지 않음.

        targetInventory.Add(recipe.result);
        return true;
    }

    bool CanAfford(CombineRecipe recipe)
    {
        UnitInventory targetInventory = Inventory;
        if (targetInventory == null)
        {
            Debug.LogError("CombineSystem: inventory 참조가 비어있습니다 (인스펙터 미할당, PlayerContext.Local도 없음).", this);
            return false;
        }

        foreach (KeyValuePair<UnitData, int> pair in AggregateIngredients(recipe))
        {
            if (CountOf(targetInventory, pair.Key) < pair.Value) return false;
        }

        if (recipe.goldCost > 0)
        {
            GoldWallet wallet = Wallet;
            if (wallet == null || wallet.Gold < recipe.goldCost) return false;
        }

        if (recipe.resourceCosts != null && recipe.resourceCosts.Count > 0)
        {
            ResourceWallet resources = Resources;
            if (resources == null) return false;

            foreach (RecipeResourceCost cost in recipe.resourceCosts)
            {
                if (resources.Get(cost.type) < cost.amount) return false;
            }
        }

        // TODO: requiredSaveCount(영원 등급) 검사는 세이브 시스템 구현 후 추가.

        return true;
    }

    Dictionary<UnitData, int> AggregateIngredients(CombineRecipe recipe)
    {
        Dictionary<UnitData, int> aggregated = new Dictionary<UnitData, int>();

        if (recipe.ingredients == null) return aggregated;

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.unit == null) continue;

            aggregated.TryGetValue(ingredient.unit, out int existing);
            aggregated[ingredient.unit] = existing + Mathf.Max(1, ingredient.count);
        }

        return aggregated;
    }

    int CountOf(UnitInventory targetInventory, UnitData unit)
    {
        int count = 0;
        foreach (UnitData u in targetInventory.Units)
        {
            if (u == unit) count++;
        }
        return count;
    }
}
