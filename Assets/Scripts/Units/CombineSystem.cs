using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CombineSystem : MonoBehaviour
{
    // 재료가 흩어져 있으면 평균 지점이 실제로 밟을 수 있는 땅에서 조금 벗어난다.
    // 우클릭 이동(UnitMover)의 2f보다 넉넉히 잡아, 조금 빗나간 정도는 붙여서 쓴다.
    const float ResultSampleRadius = 4f;

    [SerializeField] UnitInventory inventory;
    [SerializeField] ItemInventory itemInventory;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] RoundManager roundManager;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] List<CombineRecipe> recipes;

    UnitInventory Inventory => inventory != null ? inventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    GoldWallet Wallet => goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    ResourceWallet Resources => resourceWallet != null ? resourceWallet : PlayerContext.Local != null ? PlayerContext.Local.ResourceWallet : null;

    // 조합 결과를 필드에 내보내야 해서 스포너가 필요하다. 씬 배선을 늘리지 않으려고 지연 조회로 잡는다.
    // Awake에서만 잡으면 그 시점에 스포너가 아직 없을 때 영영 null로 남는다.
    // 한 번 잡으면 끝이라 "Update에서 전체 탐색 금지"에는 걸리지 않는다(SupportShop.RoundManagerRef와 같은 형태).
    UnitSpawner Spawner => unitSpawner != null ? unitSpawner : unitSpawner = FindFirstObjectByType<UnitSpawner>();

    // 인벤토리를 인스펙터로 다른 플레이어 것에 물려 놨을 수 있다. 창고와 소환 주인을 전부
    // "그 인벤토리의 주인"으로 맞춰야, 재료는 A에서 빼고 결과는 B에게 주는 일이 안 생긴다.
    // 슬롯이 넷뿐이고 실제로 조합할 때만 도는 경로다.
    PlayerContext OwnerContext
    {
        get
        {
            UnitInventory target = Inventory;
            if (target != null)
            {
                foreach (PlayerContext context in PlayerContext.All)
                    if (context != null && context.UnitInventory == target) return context;
            }

            return PlayerContext.Local;
        }
    }

    // 창고에는 "이 개체를 내가 들고 있나"만 물어본다. 없어도(null이어도) 조합은 그냥 돌아간다.
    Warehouse OwnerWarehouse
    {
        get
        {
            PlayerContext context = OwnerContext;
            return context != null ? context.Warehouse : null;
        }
    }

    // GetAvailableRecipes()가 OnGUI(프레임당 최소 2회) 경로에서 매번 불리므로,
    // 설정 누락 경고는 매 호출마다 찍지 않고 대상별로 한 번만 남긴다.
    bool loggedInventoryMissing;
    readonly HashSet<CombineRecipe> loggedRoundManagerMissingFor = new HashSet<CombineRecipe>();
    readonly HashSet<CombineRecipe> loggedItemInventoryMissingFor = new HashSet<CombineRecipe>();

    // 조합 UI가 초당 몇 번씩 부르는 경로다. 레시피 199개마다 리스트를 새로 만들면
    // 초당 수천 건이 할당된다. 버퍼를 재사용하고 결과 리스트도 돌려 쓴다.
    readonly List<CombineRecipe> availableBuffer = new List<CombineRecipe>();
    readonly List<UnitIdentity> planPool = new List<UnitIdentity>();
    readonly List<UnitIdentity> planUnits = new List<UnitIdentity>();
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
        return recipe != null && CanAfford(recipe, pickForExecution: false, out _, out _);
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
            if (recipe != null && CanAfford(recipe, pickForExecution: false, out _, out _))
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

        if (!CanAfford(recipe, pickForExecution: true, out List<UnitIdentity> unitsToRemove, out List<ItemData> itemsToRemove))
        {
            return false;
        }

        // 결과를 필드에 못 내보낼 상황이면 재료를 건드리지 않는다 —
        // 소모부터 하면 재료만 사라지고 아무것도 안 남는다.
        UnitSpawner spawner = Spawner;
        if (spawner == null)
        {
            Debug.LogWarning($"CombineSystem: UnitSpawner를 찾지 못해 {recipe.commandId} 조합을 취소했습니다 (재료는 그대로입니다).", this);
            return false;
        }

        if (recipe.result == null || recipe.result.prefab == null)
        {
            Debug.LogWarning($"CombineSystem: {recipe.commandId}의 결과 유닛에 prefab이 없어 조합을 취소했습니다 (재료는 그대로입니다).", this);
            return false;
        }

        // 전부 검사를 통과한 뒤에만 소모한다 (중간 실패로 재료만 날아가는 것 방지).
        UnitInventory targetInventory = Inventory;
        GoldWallet wallet = Wallet;
        ResourceWallet resources = Resources;

        int ownerId = ResolveOwnerId();
        // 재료를 없애기 전에 읽어야 한다 — 자리를 재료들이 서 있던 곳에서 정하기 때문이다.
        Vector3 resultPosition = ResolveResultPosition(unitsToRemove, recipe.result, ownerId);

        // 재료는 인벤토리에서 빼는 것으로 끝나지 않는다. 필드에 서 있는 그 개체를 없애야
        // 인벤토리와 필드가 어긋나지 않는다 — 예전엔 이걸 안 해서 조합할수록 필드에 재료가 쌓였다.
        foreach (UnitIdentity material in unitsToRemove)
        {
            if (material == null) continue;
            material.Consume();
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

        // 결과도 필드에 나와야 한다. Spawn이 인벤토리 등록까지 하므로 따로 Add하지 않는다.
        spawner.Spawn(recipe.result, resultPosition, ownerId);
        return true;
    }

    int ResolveOwnerId()
    {
        PlayerContext context = OwnerContext;
        return context != null ? context.PlayerId : LocalPlayer.LocalPlayerId;
    }

    // 결과는 재료가 서 있던 자리에서 나온다. 단 창고 개체는 바다 건너 창고 섬에 있어서,
    // 창고와 필드 개체를 같이 평균 내면 그 사이 바다 한가운데가 나온다 — 필드에 있던 것만 센다.
    Vector3 ResolveResultPosition(List<UnitIdentity> materials, UnitData result, int ownerId)
    {
        LaneMarker lane = LaneMarker.Get(ownerId);
        Vector3 fallback = lane != null ? lane.SpawnPosition : transform.position;

        Warehouse warehouse = OwnerWarehouse;
        Vector3 sum = Vector3.zero;
        int counted = 0;

        foreach (UnitIdentity material in materials)
        {
            if (material == null) continue;
            if (warehouse != null && warehouse.Contains(material.gameObject)) continue;

            sum += material.transform.position;
            counted++;
        }

        if (counted == 0) return fallback;

        // 평균이 바다나 벽 안으로 떨어질 수 있다(레인 유닛과 창고 유닛의 중간 등).
        // NavMesh 밖에 스폰된 NavMeshAgent는 경로를 못 잡고 그 자리에 굳는다 —
        // UnitMover.TryMoveToCursor가 이동 목적지에 같은 검사를 한다.
        // 지상 유닛 자리를 바다에서 찾지 않도록 그 유닛이 실제로 쓸 areaMask로 본다.
        int areaMask = UnitSpawner.ComputeAreaMask(result.movementAbility);
        return NavMesh.SamplePosition(sum / counted, out NavMeshHit hit, ResultSampleRadius, areaMask)
            ? hit.position
            : fallback;
    }

    bool CanAfford(CombineRecipe recipe, bool pickForExecution,
                   out List<UnitIdentity> unitsToRemove, out List<ItemData> itemsToRemove)
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

        if (!TryPlanUnits(recipe, targetInventory, pickForExecution, out unitsToRemove)) return false;
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
    bool TryPlanUnits(CombineRecipe recipe, UnitInventory targetInventory, bool pickForExecution,
                      out List<UnitIdentity> unitsToRemove)
    {
        planUnits.Clear();
        unitsToRemove = planUnits;

        if (recipe.ingredients == null) return true;

        List<UnitIdentity> pool = planPool;
        FillPool(pool, targetInventory, pickForExecution);

        foreach (RecipeIngredient ingredient in recipe.ingredients)
        {
            if (ingredient == null || ingredient.kind != IngredientKind.SpecificUnit || ingredient.unit == null) continue;

            if (!TryTakeUnit(pool, ingredient.unit, Mathf.Max(1, ingredient.count), unitsToRemove))
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

    // 인벤토리를 그대로 쓰면 재료를 빼는 과정에서 실제 인벤토리가 망가진다.
    // 복사본이 필요하되, 매번 새로 만들지 않고 버퍼를 비워서 채운다.
    //
    // 창고 우선 정렬은 실제로 소모할 때만 한다. 조합 가능 여부만 보는 경로(GetAvailableRecipes)는
    // 조합식 199개를 매 OnGUI마다 도는 자리라, 거기서 창고까지 뒤지면 그 자체가 부담이 된다.
    // 어느 개체를 쓰든 "만들 수 있나"의 답은 같으므로 그 경로에선 정렬이 필요 없다.
    void FillPool(List<UnitIdentity> pool, UnitInventory targetInventory, bool warehouseFirst)
    {
        pool.Clear();

        IReadOnlyList<UnitIdentity> members = targetInventory.Members;

        if (!warehouseFirst)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null) pool.Add(members[i]);

            return;
        }

        // 창고에 있는 개체부터 소모한다. 레인에서 싸우는 중인 유닛을 조합이 말없이 지우면
        // 방어가 갑자기 뚫린다 — 창고에 치워둔 개체가 "쓰려고 모아둔 것"에 가깝다.
        // 앞에 담은 것부터 집어가므로(TryTakeUnit이 앞에서부터 훑는다) 창고 것을 먼저 넣는다.
        Warehouse warehouse = OwnerWarehouse;

        for (int i = 0; i < members.Count; i++)
            if (members[i] != null && warehouse != null && warehouse.Contains(members[i].gameObject))
                pool.Add(members[i]);

        for (int i = 0; i < members.Count; i++)
            if (members[i] != null && (warehouse == null || !warehouse.Contains(members[i].gameObject)))
                pool.Add(members[i]);
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

    // 유닛은 개체(UnitIdentity)로 집는다 — 어느 개체를 없앨지가 정해져야 필드까지 정리할 수 있다.
    // pool 앞쪽이 우선순위가 높다(FillPool이 창고 개체를 앞에 넣는다).
    static bool TryTakeUnit(List<UnitIdentity> pool, UnitData target, int count, List<UnitIdentity> takenOut)
    {
        int taken = 0;
        for (int i = 0; i < pool.Count && taken < count; )
        {
            if (pool[i] != null && pool[i].Data == target)
            {
                takenOut.Add(pool[i]);
                pool.RemoveAt(i);
                taken++;
            }
            else
            {
                i++;
            }
        }

        return taken >= count;
    }

    static bool TryTakeByGrade(List<UnitIdentity> pool, UnitGrade grade, int count, List<UnitIdentity> takenOut)
    {
        int taken = 0;
        for (int i = 0; i < pool.Count && taken < count; )
        {
            if (pool[i] != null && pool[i].Data != null && pool[i].Data.grade == grade)
            {
                takenOut.Add(pool[i]);
                pool.RemoveAt(i);
                taken++;
            }
            else
            {
                i++;
            }
        }

        return taken >= count;
    }
}
