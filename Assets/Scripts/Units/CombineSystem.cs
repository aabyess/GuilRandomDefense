using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CombineSystem : MonoBehaviour
{
    // 재료가 흩어져 있으면 평균 지점이 실제로 밟을 수 있는 땅에서 조금 벗어난다.
    // 우클릭 이동(UnitMover)보다 넉넉히 잡아, 조금 빗나간 정도는 붙여서 쓴다.
    // 맵이 WorldScale.Value배가 됐으므로 이 거리도 같이 커진다(2026-09-23) —
    // 재료가 흩어진 거리 자체가 맵 배율만큼 늘어나기 때문이다.
    const float ResultSampleRadius = 4f * WorldScale.Value;

    [SerializeField] UnitInventory inventory;
    [SerializeField] ItemInventory itemInventory;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] RoundManager roundManager;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] List<CombineRecipe> recipes;

    // MP: 씬은 이 조합기 하나에 플레이어 0의 인벤토리·지갑을 직접 물려 둔다(싱글엔 그게 맞다). 멀티에선
    //     「누가 조합하나」가 바뀐다 — 호스트가 클라 요청을 수행하는 동안은 그 슬롯(ActingPlayerOverride),
    //     그 밖엔 이 PC의 로컬 플레이어(클라의 흐림 계산 포함). 직렬화 배선보다 앞선다. 싱글(MatchConfig 꺼짐)은 무동작.
    public static int ActingPlayerOverride = -1;
    static PlayerContext MultiplayerActingContext => ActingPlayerOverride >= 0
        ? PlayerContext.Get(ActingPlayerOverride)
        : MatchConfig.Active ? PlayerContext.Local : null;

    UnitInventory Inventory => MultiplayerActingContext != null ? MultiplayerActingContext.UnitInventory // MP
        : inventory != null ? inventory : PlayerContext.Local != null ? PlayerContext.Local.UnitInventory : null;
    GoldWallet Wallet => MultiplayerActingContext != null ? MultiplayerActingContext.GoldWallet // MP
        : goldWallet != null ? goldWallet : PlayerContext.Local != null ? PlayerContext.Local.GoldWallet : null;
    ResourceWallet Resources => MultiplayerActingContext != null ? MultiplayerActingContext.ResourceWallet // MP
        : resourceWallet != null ? resourceWallet : PlayerContext.Local != null ? PlayerContext.Local.ResourceWallet : null;

    /// <summary>MP: 조합식을 네트워크로 가리킬 번호(이 조합기의 recipes 목록 순서 — 호스트·클라 같은 씬).</summary>
    /// <summary>조합식 전체(읽기 전용) — 조합 검색 서랍이 훑는다.</summary>
    public IReadOnlyList<CombineRecipe> Recipes => recipes;

    public int IndexOfRecipe(CombineRecipe recipe) => recipes != null ? recipes.IndexOf(recipe) : -1;
    public CombineRecipe RecipeAt(int index) => recipes != null && index >= 0 && index < recipes.Count ? recipes[index] : null;

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

    // 🔴 2026-09-09 — 위 Inventory/Wallet/Resources와 달리 아이템 인벤토리만 직렬화 필드를
    //    **그대로** 쓰고 있었다. 그런데 씬의 ItemInventory는 하나뿐(플레이어 0 슬롯)이라,
    //    어느 플레이어가 조합하든 재료를 플레이어 0의 인벤토리에서 뺐다. 형제인
    //    ItemGambleState가 4개인 것과 어긋난다 — 원작은 습득·보관·해금이 전부 pid로 갈린다.
    //    OwnerContext를 거치게 해서 위 셋과 같은 결로 맞춘다(인스펙터 값이 있으면 그게 우선 —
    //    다른 플레이어 것에 일부러 물려 놓은 배선을 덮지 않는다).
    ItemInventory OwnerItems => MultiplayerActingContext != null ? MultiplayerActingContext.ItemInventory // MP
        : itemInventory != null
        ? itemInventory
        : OwnerContext != null ? OwnerContext.ItemInventory : null;

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
            if (IsChatOnly(recipe)) continue;   // 10-06 히든·불멸·초월은 조합 버튼에 안 뜬다(채팅 코드로만)
            if (FirstUnitIngredient(recipe) != unit) continue;

            startsWithBuffer.Add(recipe);
        }

        return startsWithBuffer;
    }

    /// <summary>10-06(친구 피드백): 결과가 히든·불멸·초월인 식은 유닛의 [조합] 버튼으로 못 한다 — 원작처럼 채팅 코드로만(TryCombineByChat).
    /// 버튼 목록(GetRecipesStartingWith)과 멀티 요청 검증(NetCommands.ExecuteCombine이 같은 목록을 본다)에서 빠진다.
    /// 재료·비용·결과 처리는 TryCombine 그대로다(밸런스 도구 autoloop은 GetAvailableRecipes→TryCombine이라 이 등급도 계속 조합한다).</summary>
    public static bool IsChatOnly(CombineRecipe recipe)
    {
        if (recipe == null || recipe.result == null) return false;
        UnitGrade grade = recipe.result.grade;
        return grade == UnitGrade.Hidden || grade == UnitGrade.Immortal || grade == UnitGrade.Transcendent;
    }

    static string NormalizePhrase(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.Replace(" ", "").Replace("\t", "").ToLowerInvariant();

    // 이 식을 부르는 채팅 문구 전부(정규화됨): commandId의 「/」 양쪽(예 「페로나조합 / perona」·「Juuhyuk tr」) ·
    // 에셋 이름의 친구 이름+「조합」(예 히든_최윤서 → 「최윤서 조합」) · 식의 chatPhrase(초월 수식어).
    static IEnumerable<string> ChatPhrases(CombineRecipe recipe)
    {
        if (!string.IsNullOrEmpty(recipe.commandId))
            foreach (string part in recipe.commandId.Split('/'))
            {
                string phrase = NormalizePhrase(part);
                if (phrase.Length > 0) yield return phrase;
            }

        string[] nameParts = recipe.name.Split('_');
        if (nameParts.Length >= 2 && nameParts[1].Length > 0) yield return NormalizePhrase(nameParts[1] + "조합");

        if (!string.IsNullOrEmpty(recipe.chatPhrase)) yield return NormalizePhrase(recipe.chatPhrase);
    }

    /// <summary>채팅 한 줄이 히든·불멸·초월 조합 코드면 그 플레이어 기준으로 조합한다. 코드가 아니면 null(다음 판정기로 넘김),
    /// 맞으면 성공·실패 문구. 호스트(싱글)에서만 부른다(PlayerChat.HandleOnAuthority → GameChatBox.TryExecuteCode).
    /// 결과는 시전 유닛이 없어 레인 가운데에 나온다.</summary>
    public string TryCombineByChat(int playerId, string text)
    {
        if (recipes == null) return null;
        string typed = NormalizePhrase(text);
        if (typed.Length == 0) return null;

        CombineRecipe match = null;
        foreach (CombineRecipe recipe in recipes)
        {
            if (!IsChatOnly(recipe)) continue;
            foreach (string phrase in ChatPhrases(recipe))
                if (phrase == typed) { match = recipe; break; }
            if (match != null) break;
        }
        if (match == null) return null;

        string label = match.result.unitName;
        int previous = ActingPlayerOverride;
        ActingPlayerOverride = playerId;
        try
        {
            if (!CanCombineNow(match))
            {
                List<string> shortage = DescribeShortage(match);
                return shortage.Count > 0 ? shortage[0] : $"{label}: 지금은 조합할 수 없습니다.";
            }
            return TryCombine(match) ? $"{label} 조합 성공!" : $"{label}: 지금은 조합할 수 없습니다.";
        }
        finally { ActingPlayerOverride = previous; }
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

    // casterPosition: 조합 버튼을 누른 유닛(선택된 유닛)의 자리. 원작 [조합]은 유닛의 능력이라 결과가
    // 그 시전 유닛 자리에 나온다(war3map.j L15134). 없으면(디버그 F2 등) 레인 가운데.
    public bool TryCombine(CombineRecipe recipe, Vector3? casterPosition = null)
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
        Vector3 resultPosition = ResolveResultPosition(casterPosition, recipe.result, ownerId);

        // 재료는 인벤토리에서 빼는 것으로 끝나지 않는다. 필드에 서 있는 그 개체를 없애야
        // 인벤토리와 필드가 어긋나지 않는다 — 예전엔 이걸 안 해서 조합할수록 필드에 재료가 쌓였다.
        foreach (UnitIdentity material in unitsToRemove)
        {
            if (material == null) continue;
            material.Consume();
        }

        ItemInventory ownerItems = OwnerItems;
        if (ownerItems != null)
        {
            foreach (ItemData item in itemsToRemove)
            {
                ownerItems.Remove(item);
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

        // 결과도 필드에 나와야 한다. Spawn이 인벤토리 등록까지 하므로 따로 Add하지 않는다.
        spawner.Spawn(recipe.result, resultPosition, ownerId);
        if (IsTransformRecipe(recipe)) OwnerContext?.TryConsumeTransformUse();   // 원작: 변화 성공 때 토큰 1기 제거

        // 도움소 「능력치 증가」(H0B7) 선행 조건(Rhfl) — 초월함 조합을 완료한 순간 켠다.
        // 원작은 이 순간부터 계속 조합해도 다시 안 꺼진다(한 번만 넘으면 되는 문턱)이라
        // 되돌리는 코드는 없다.
        if (recipe.result.grade == UnitGrade.Transcendent)
            OwnerContext?.MarkTranscendentCombineCompleted();

        // 2026-09-06 — Hidden_Aokiji(히든_성탄.asset, +2)류 Damage_level_Fixed 누적.
        // 항법 "패왕의길"·ChatUnlockManager/HiddenCombineManager와 같은 카운터
        // (DamageLevelFixedState)에 더해 합산되게 한다. 이중 계상 경고는
        // CombineRecipe.damageLevelFixedBonus 주석 참고.
        OwnerContext?.DamageLevelFixedState?.Add(recipe.damageLevelFixedBonus);

        return true;
    }

    int ResolveOwnerId()
    {
        PlayerContext context = OwnerContext;
        return context != null ? context.PlayerId : LocalPlayer.LocalPlayerId;
    }

    // ⚠️ 2026-09-26 사장님 지시로 아래 09-25 규칙은 **바뀌었다** — 결과는 레인 가운데 고리 자리(ResolveResultPosition 바로 위 🔴 주석).
    // (09-25 기록) ✅ 2026-09-25 원작화(사장님 「1번이랑 3번 원작대로」): 결과는 **[조합]을 누른 유닛 자리**에 나온다.
    //    원작 [조합]은 유닛 능력이고 결과를 시전 유닛 위치에 만든다(war3map.j L15134). 조합한 자리에서
    //    바로 싸우니, 레인 가운데에 생겨 매번 옮겨야 하던 문제(구현담당1 i1_07: 가운데 방치 → R3 패배)가 없다.
    //    누른 유닛 정보가 없을 때(디버그 F2)만 아래 옛 규칙(레인 가운데)으로 간다.
    //
    // (옛 규칙) 조합 결과는 **그 플레이어 레인의 한가운데**에 나온다 (사장님 지시 2026-09-24:
    //    「흔함 조합해서 나오는것들은 각 레인의 맵 가운데에 배치하게해야하지」).
    //
    //    그 전에는 **재료가 서 있던 자리의 평균**이었다. 그러면 조합할 때마다 결과가 우리 근처나
    //    레인 구석에 흩어져 나와서, 새로 만든 유닛을 찾아 옮기는 일이 매번 생겼다.
    //    가운데에서 나오면 어디서 조합했든 나오는 자리가 늘 같다.
    //
    //    LaneCenter는 레인 섬 오브젝트 자신의 위치라 곧 기하학적 한가운데다(LaneMarker 주석).
    //    옛 평균 방식이 신경 쓰던 「창고 개체는 바다 건너에 있어 같이 평균 내면 바다가 나온다」는
    //    문제도 같이 사라진다 — 재료 위치를 아예 안 본다.
    // 🔴 2026-09-26 사장님 「조합하거나 흔함 제외 뽑기로 나온 유닛들은 레인 가운데에 배치」 — 09-25의 「[조합]을 누른 자리」를 버린다.
    //    결과는 새로 뽑은 유닛과 같은 길(LaneMarker.TakeSpawnPosition): 흔함 아니면 레인 가운데 고리 자리. casterPosition은 이제 안 본다
    //    (호출부 GameHud·멀티 RPC의 서명을 안 바꾸려고 인자는 남겼다).
    // 🔴 2026-10-04 친구 피드백(노무현) 「조합 버튼 누른 유닛이 변하든가 그 근처에 조합된 유닛이 나오게」 — 09-25 원작화(시전 유닛 자리, war3map.j L15134)를 다시 켠다.
    //    흔함 등급 결과는 예전처럼 흔함 칸(LaneMarker.TakeSpawnPosition — 「흔함은 칸 안」 규칙)으로 간다. 그 밖의 결과만 누른 유닛 곁에 나온다.
    //    사장님 09-26 「조합 결과는 레인 가운데」와 반대라 PM·사장님 확인 뒤 켜 둔다 — 되돌리려면 false 한 줄.
    const bool ResultAtCasterUnit = true;
    const float ResultAtCasterJitter = 6f;   // 같은 자리에 포개 서면 클릭하기 어렵다 — 시전 유닛 바로 곁 한 몸쯤 옆(NavMesh로 다시 보정)

    Vector3 ResolveResultPosition(Vector3? casterPosition, UnitData result, int ownerId)
    {
        LaneMarker lane = LaneMarker.Get(ownerId);
        if (lane == null) return transform.position;

        if (ResultAtCasterUnit && casterPosition.HasValue && result != null && result.grade != UnitGrade.Common)
        {
            int casterMask = UnitSpawner.ComputeAreaMask(result.movementAbility);
            Vector3 near = casterPosition.Value + new Vector3(ResultAtCasterJitter, 0f, 0f);
            if (NavMesh.SamplePosition(near, out NavMeshHit casterHit, ResultSampleRadius, casterMask)) return casterHit.position;
            // 시전 유닛 곁이 NavMesh 밖이면(스토리존 건물 위 등) 레인 가운데로 — 아래 옛 규칙
        }

        // 가운데가 NavMesh 밖일 수 있다(그 자리에 건물이 서 있는 등).
        // NavMesh 밖에 스폰된 NavMeshAgent는 경로를 못 잡고 그 자리에 굳는다 —
        // UnitMover.TryMoveToCursor가 이동 목적지에 같은 검사를 한다.
        // 지상 유닛 자리를 바다에서 찾지 않도록 그 유닛이 실제로 쓸 areaMask로 본다.
        int areaMask = UnitSpawner.ComputeAreaMask(result.movementAbility);
        Vector3 slot = lane.TakeSpawnPosition(result);
        return NavMesh.SamplePosition(slot, out NavMeshHit hit, ResultSampleRadius, areaMask) ? hit.position : slot;
    }

    /// <summary>
    /// 왜 못 만드는지 — 원작 Fusion3___Action(war3map_new.j:3441~3449) 문구 그대로. 모자란 재료마다 「재료 부족 : 이름 N개」,
    /// 재료가 다 있으면 「골드가 부족 합니다!: N」 또는 「목재가 부족합니다!:N」(원작도 첫 모자람에서 멈춘다). 빈 목록이면 재료·돈은 된다
    /// (원딜·라운드 조건 등 다른 이유). 읽기만 한다(알림 묶음 5/13, GAP 65).
    /// </summary>
    public List<string> DescribeShortage(CombineRecipe recipe)
    {
        var lines = new List<string>();
        if (recipe == null) return lines;
        // 재료 부족이 아닌 「잠김」은 따로(구현담당1 제안) — 원작엔 문구가 없다(버튼이 사라짐). 문구는 우리 것.
        if (IsBroken(recipe)) { lines.Add("이 조합식은 지금 쓸 수 없습니다(잠김)."); return lines; }
        if (recipe.result != null && NavigationState.IsOneDealGrade(recipe.result.grade) && OwnerContext?.NavigationState != null &&
            OwnerContext.NavigationState.OneDealLocked)
        {
            lines.Add("원딜 잠김 — 패왕의길로 이미 한 기를 얻어 제한됨·초월·불멸·영원은 더 만들 수 없습니다.");
            return lines;
        }
        // 원작 Trig_change 실패 문구 — 목재나 회수가 모자라면 같은 한 줄(본인에게만). 회수가 0이면 재료가 다 있어도 이 줄.
        if (IsTransformRecipe(recipe) && OwnerContext != null && OwnerContext.TransformUsesLeft <= 0)
        {
            lines.Add("목재나 변화가능 횟수가 부족합니다!");
            return lines;
        }
        UnitInventory inventory = Inventory;
        if (recipe.ingredients != null && inventory != null)
        {
            foreach (RecipeIngredient ingredient in recipe.ingredients)
            {
                if (ingredient == null) continue;
                int need = Mathf.Max(1, ingredient.count);
                int have = 0;
                foreach (UnitIdentity member in inventory.Members)
                {
                    if (member == null || member.Data == null) continue;
                    if (ingredient.kind == IngredientKind.SpecificUnit ? member.Data == ingredient.unit
                        : ingredient.kind == IngredientKind.UnitGradeWildcard && member.Data.grade == ingredient.wildcardGrade) have++;
                }
                if (have >= need) continue;
                string name = ingredient.kind == IngredientKind.SpecificUnit
                    ? (ingredient.unit != null ? ingredient.unit.unitName : "?")
                    : $"{ingredient.wildcardGrade.KoreanName()} 등급";
                lines.Add($"재료 부족 : {name} {need - have}개");
            }
        }
        if (lines.Count > 0) return lines;

        GoldWallet wallet = Wallet;
        if (recipe.goldCost > 0 && wallet != null && wallet.Gold < recipe.goldCost)
        {
            lines.Add($"골드가 부족 합니다!: {recipe.goldCost - wallet.Gold}");
            return lines;
        }
        ResourceWallet resources = Resources;
        if (recipe.resourceCosts != null && resources != null)
            foreach (RecipeResourceCost cost in recipe.resourceCosts)
                if (cost.type == ResourceType.Wood && resources.Get(cost.type) < cost.amount)
                {
                    lines.Add(IsTransformRecipe(recipe) ? "목재나 변화가능 횟수가 부족합니다!" : $"목재가 부족합니다!:{cost.amount - resources.Get(cost.type)}");
                    return lines;
                }
        return lines;
    }

    static bool IsTransformRecipe(CombineRecipe recipe) => recipe != null && recipe.result != null && recipe.result.grade == UnitGrade.Transformed;

    bool CanAfford(CombineRecipe recipe, bool pickForExecution,
                   out List<UnitIdentity> unitsToRemove, out List<ItemData> itemsToRemove)
    {
        unitsToRemove = null;
        itemsToRemove = null;

        if (IsBroken(recipe)) return false;
        // 원딜(GAP 6, NavigationState.OneDealLocked) — 잠긴 플레이어는 네 등급(제한됨·초월·불멸·영원) 결과 식을 못 만든다.
        if (recipe.result != null && NavigationState.IsOneDealGrade(recipe.result.grade) && OwnerContext?.NavigationState != null &&
            OwnerContext.NavigationState.OneDealLocked) return false;
        // 변화(A0KJ) 회수(원작 Trig_change) — 플레이어당 2회.
        if (IsTransformRecipe(recipe) && OwnerContext != null && OwnerContext.TransformUsesLeft <= 0) return false;
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

        if (!SaveCountConditionMet(recipe)) return false;

        return true;
    }

    // ── 깨진 조합식 막기(2026-09-26, PM 지시) ──
    //    09-02 aac69e98에서 네 식(초월 강재규·김건·최상호 AP, 제한 최영민)의 YAML 줄바꿈이 빠져 재료가 「unit null 하나」로 읽혔다.
    //    TryPlanUnits는 null 재료를 건너뛰어서 CanAfford가 늘 참 → **재료 없이 무한 조합**이 됐다(구현담당1 combineall 판: 초월 360기).
    //    데이터가 또 깨져도 공짜 조합이 되지 않게, 재료가 비었거나 참조가 빈 식은 **아예 못 쓰게** 한다(한 번 LogError).
    //    판 시작 때(Start) 전체를 한 번 훑어 깨진 식 이름을 경고로 남긴다.
    HashSet<CombineRecipe> brokenRecipes;
    readonly HashSet<CombineRecipe> loggedBrokenFor = new HashSet<CombineRecipe>();

    static string BrokenReason(CombineRecipe recipe)
    {
        if (recipe.ingredients == null || recipe.ingredients.Count == 0) return "재료 목록이 비었다";
        for (int i = 0; i < recipe.ingredients.Count; i++)
        {
            RecipeIngredient ing = recipe.ingredients[i];
            if (ing == null) return $"{i + 1}번째 재료가 null";
            if (ing.kind == IngredientKind.SpecificUnit && ing.unit == null) return $"{i + 1}번째 재료(유닛) 참조가 비었다";
            if (ing.kind == IngredientKind.SpecificItem && ing.item == null) return $"{i + 1}번째 재료(아이템) 참조가 비었다";
        }
        return null;
    }

    HashSet<CombineRecipe> BrokenRecipes()
    {
        if (brokenRecipes != null) return brokenRecipes;
        brokenRecipes = new HashSet<CombineRecipe>();
        var names = new List<string>();
        if (recipes != null)
            foreach (CombineRecipe recipe in recipes)
            {
                if (recipe == null) continue;
                string why = BrokenReason(recipe);
                if (why == null) continue;
                brokenRecipes.Add(recipe);
                names.Add($"{recipe.name}({why})");
            }
        if (names.Count > 0)
            Debug.LogWarning($"CombineSystem: 깨진 조합식 {names.Count}개는 쓸 수 없게 막았습니다 — {string.Join(", ", names)}. " +
                             "에셋 YAML(특히 「ingredients:」 줄)을 확인하세요.", this);
        return brokenRecipes;
    }

    bool IsBroken(CombineRecipe recipe)
    {
        if (!BrokenRecipes().Contains(recipe)) return false;
        if (loggedBrokenFor.Add(recipe))
            Debug.LogError($"CombineSystem: {recipe.name}({recipe.commandId})은 재료가 깨져 조합을 막았습니다 — {BrokenReason(recipe)}.", this);
        return true;
    }

    void Start() => BrokenRecipes();

    // 영원 등급 전용(CombineRecipe.requiredSaveCount 참고). PersistentSave가 없으면(씬 배선
    // 누락 등) 잠가둔다 — 조건을 못 재는 상태에서 통과시키면 조건 자체가 없는 것과 같아진다.
    bool SaveCountConditionMet(CombineRecipe recipe)
    {
        if (recipe.requiredSaveCount <= 0) return true;

        PersistentSave save = OwnerContext?.PersistentSave;
        return save != null && save.Data.cumulativeClearCount >= recipe.requiredSaveCount;
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

        ItemInventory ownerItems = OwnerItems;
        if (ownerItems == null)
        {
            if (loggedItemInventoryMissingFor.Add(recipe))
            {
                Debug.LogWarning($"CombineSystem: {recipe.commandId}에 아이템 재료가 필요하지만 itemInventory가 비어있습니다.");
            }
            return false;
        }

        List<ItemData> pool = new List<ItemData>(ownerItems.Items);

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
            // 시스템 유닛(해적단 퀘스트 토큰 등)은 grade가 흔함이어도 진짜 등급 재료가
            // 아니다 — 이름을 안 가리는 와일드카드 재료라서 걸러두지 않으면 인벤토리에
            // 섞여 있는 토큰이 조합 재료로 조용히 사라진다(WIRING_AUDIT.md §⑧).
            if (pool[i] != null && pool[i].Data != null && pool[i].Data.grade == grade
                && !pool[i].Data.isSystemUnit)
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
