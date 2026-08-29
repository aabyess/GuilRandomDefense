using System.Collections.Generic;
using UnityEngine;

public class CombineSystem : MonoBehaviour
{
    [SerializeField] UnitInventory inventory;
    [SerializeField] ItemInventory itemInventory;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] RoundManager roundManager;
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
            if (recipe != null && CanAfford(recipe, out _, out _))
            {
                available.Add(recipe);
            }
        }

        return available;
    }

    public bool TryCombine(CombineRecipe recipe)
    {
        if (recipe == null) return false;

        if (!CanAfford(recipe, out List<UnitData> unitsToRemove, out List<ItemData> itemsToRemove))
        {
            return false;
        }

        // 전부 검사를 통과한 뒤에만 소모한다 (중간 실패로 재료만 날아가는 것 방지).
        UnitInventory targetInventory = Inventory;
        GoldWallet wallet = Wallet;
        ResourceWallet resources = Resources;

        foreach (UnitData unit in unitsToRemove)
        {
            targetInventory.Remove(unit);
        }

        if (itemInventory != null)
        {
            foreach (ItemData item in itemsToRemove)
            {
                itemInventory.Remove(item);
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

    bool CanAfford(CombineRecipe recipe, out List<UnitData> unitsToRemove, out List<ItemData> itemsToRemove)
    {
        unitsToRemove = null;
        itemsToRemove = null;

        if (!RoundConditionMet(recipe)) return false;

        UnitInventory targetInventory = Inventory;
        if (targetInventory == null)
        {
            Debug.LogError("CombineSystem: inventory 참조가 비어있습니다 (인스펙터 미할당, PlayerContext.Local도 없음).", this);
            return false;
        }

        if (!TryPlanUnits(recipe, targetInventory, out unitsToRemove)) return false;
        if (!TryPlanItems(recipe, out itemsToRemove)) return false;

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

    bool RoundConditionMet(CombineRecipe recipe)
    {
        if (recipe.minRound <= 0 && recipe.maxRound <= 0) return true;

        if (roundManager == null)
        {
            Debug.LogWarning($"CombineSystem: {recipe.commandId} 라운드 조건이 있지만 roundManager가 비어있어 조합을 허용하지 않습니다.");
            return false;
        }

        int currentRound = roundManager.CurrentRound;
        if (recipe.minRound > 0 && currentRound < recipe.minRound) return false;
        if (recipe.maxRound > 0 && currentRound > recipe.maxRound) return false;
        return true;
    }

    // 특정 유닛(SpecificUnit) 요구를 먼저 채우고, 남은 재고에서 등급 와일드카드(UnitGradeWildcard)를 채운다.
    // 이 순서를 지켜야 와일드카드가 다른 슬롯에 필요한 유닛을 가로채지 않는다.
    bool TryPlanUnits(CombineRecipe recipe, UnitInventory targetInventory, out List<UnitData> unitsToRemove)
    {
        unitsToRemove = new List<UnitData>();

        if (recipe.ingredients == null) return true;

        List<UnitData> pool = new List<UnitData>(targetInventory.Units);

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.kind != IngredientKind.SpecificUnit || ingredient.unit == null) continue;

            if (!TryTake(pool, ingredient.unit, Mathf.Max(1, ingredient.count), unitsToRemove))
            {
                return false;
            }
        }

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.kind != IngredientKind.UnitGradeWildcard) continue;

            if (!TryTakeByGrade(pool, ingredient.wildcardGrade, Mathf.Max(1, ingredient.count), unitsToRemove))
            {
                return false;
            }
        }

        return true;
    }

    bool TryPlanItems(CombineRecipe recipe, out List<ItemData> itemsToRemove)
    {
        itemsToRemove = new List<ItemData>();

        if (recipe.ingredients == null) return true;

        bool hasItemIngredient = false;
        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient != null && ingredient.kind == IngredientKind.SpecificItem)
            {
                hasItemIngredient = true;
                break;
            }
        }

        if (!hasItemIngredient) return true;

        if (itemInventory == null)
        {
            Debug.LogWarning($"CombineSystem: {recipe.commandId}에 아이템 재료가 필요하지만 itemInventory가 비어있습니다.");
            return false;
        }

        List<ItemData> pool = new List<ItemData>(itemInventory.Items);

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.kind != IngredientKind.SpecificItem || ingredient.item == null) continue;

            if (!TryTake(pool, ingredient.item, Mathf.Max(1, ingredient.count), itemsToRemove))
            {
                return false;
            }
        }

        return true;
    }

    static bool TryTake<T>(List<T> pool, T target, int count, List<T> takenOut) where T : Object
    {
        int taken = 0;
        for (int i = pool.Count - 1; i >= 0 && taken < count; i--)
        {
            if (pool[i] == target)
            {
                takenOut.Add(pool[i]);
                pool.RemoveAt(i);
                taken++;
            }
        }

        return taken >= count;
    }

    static bool TryTakeByGrade(List<UnitData> pool, UnitGrade grade, int count, List<UnitData> takenOut)
    {
        int taken = 0;
        for (int i = pool.Count - 1; i >= 0 && taken < count; i--)
        {
            if (pool[i] != null && pool[i].grade == grade)
            {
                takenOut.Add(pool[i]);
                pool.RemoveAt(i);
                taken++;
            }
        }

        return taken >= count;
    }
}
