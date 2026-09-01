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

    // GetAvailableRecipes()가 OnGUI(프레임당 최소 2회) 경로에서 매번 불리므로,
    // 설정 누락 경고는 매 호출마다 찍지 않고 대상별로 한 번만 남긴다.
    bool loggedInventoryMissing;
    readonly HashSet<CombineRecipe> loggedRoundManagerMissingFor = new HashSet<CombineRecipe>();
    readonly HashSet<CombineRecipe> loggedItemInventoryMissingFor = new HashSet<CombineRecipe>();

    // 조합 UI가 초당 몇 번씩 부르는 경로다. 레시피 199개마다 리스트를 새로 만들면
    // 초당 수천 건이 할당된다. 버퍼를 재사용하고 결과 리스트도 돌려 쓴다.
    readonly List<CombineRecipe> availableBuffer = new List<CombineRecipe>();
    readonly List<UnitData> planPool = new List<UnitData>();
    readonly List<UnitData> planUnits = new List<UnitData>();
    readonly List<ItemData> planItems = new List<ItemData>();

    readonly List<CombineRecipe> startsWithBuffer = new List<CombineRecipe>();

    /// <summary>
    /// 이 유닛이 <b>첫 번째 재료</b>인 레시피들. 조합식 표에서 맨 왼쪽에 서는 유닛이 기준이다.
    /// 재료가 갖춰졌는지는 보지 않는다 — 뭘 만들 수 있는지 보여주는 용도라, 지금 못 만들어도 알려줘야 한다.
    /// <b>돌려주는 리스트는 재사용된다.</b>
    /// </summary>
    public List<CombineRecipe> GetRecipesStartingWith(UnitData unit)
    {
        startsWithBuffer.Clear();
        if (unit == null || recipes == null) return startsWithBuffer;

        foreach (CombineRecipe recipe in recipes)
        {
            if (recipe == null || recipe.result == null) continue;
            if (FirstUnitIngredient(recipe) != unit) continue;

            startsWithBuffer.Add(recipe);
        }

        return startsWithBuffer;
    }

    static UnitData FirstUnitIngredient(CombineRecipe recipe)
    {
        if (recipe.ingredients == null) return null;

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.kind != IngredientKind.SpecificUnit) continue;
            return ingredient.unit;
        }

        return null;
    }

    /// <summary>재료가 갖춰져 지금 바로 만들 수 있는지.</summary>
    public bool CanCombineNow(CombineRecipe recipe)
    {
        return recipe != null && CanAfford(recipe, out _, out _);
    }

    /// <summary>
    /// 지금 만들 수 있는 레시피. <b>돌려주는 리스트는 재사용된다</b> —
    /// 다음 호출에서 내용이 바뀌므로, 보관하려면 복사해야 한다.
    /// </summary>
    public List<CombineRecipe> GetAvailableRecipes()
    {
        availableBuffer.Clear();
        if (recipes == null) return availableBuffer;

        foreach (CombineRecipe recipe in recipes)
        {
            if (recipe != null && CanAfford(recipe, out _, out _))
            {
                availableBuffer.Add(recipe);
            }
        }

        return availableBuffer;
    }

    public bool TryCombine(CombineRecipe recipe)
    {
        if (!GameAuthority.IsServer) return false;
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
            if (!loggedInventoryMissing)
            {
                loggedInventoryMissing = true;
                Debug.LogError("CombineSystem: inventory 참조가 비어있습니다 (인스펙터 미할당, PlayerContext.Local도 없음).", this);
            }
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
            if (loggedRoundManagerMissingFor.Add(recipe))
            {
                Debug.LogWarning($"CombineSystem: {recipe.commandId} 라운드 조건이 있지만 roundManager가 비어있어 조합을 허용하지 않습니다.");
            }
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
        planUnits.Clear();
        unitsToRemove = planUnits;

        if (recipe.ingredients == null) return true;

        // 인벤토리를 그대로 쓰면 재료를 빼는 과정에서 실제 인벤토리가 망가진다.
        // 복사본이 필요하되, 매번 새로 만들지 않고 버퍼를 비워서 채운다.
        List<UnitData> pool = planPool;
        pool.Clear();
        pool.AddRange(targetInventory.Units);

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
        planItems.Clear();
        itemsToRemove = planItems;

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
            if (loggedItemInventoryMissingFor.Add(recipe))
            {
                Debug.LogWarning($"CombineSystem: {recipe.commandId}에 아이템 재료가 필요하지만 itemInventory가 비어있습니다.");
            }
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
