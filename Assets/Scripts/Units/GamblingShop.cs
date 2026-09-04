using System.Collections.Generic;
using UnityEngine;

// 도박소: 도움소(SupportShop)와 같은 구조 — 레인 안에 서 있고, 선택하면 하단에 옵션 칸이 뜨고,
// 칸을 누르면 자원을 쓴다. 파괴 불가, 적 타겟에서 자동 제외된다 — SupportShop과 같은 이유로
// EnemyDummy.Active/DestructibleGate.Active 어디에도 등록되지 않는다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class GamblingShop : MonoBehaviour, ILaneShop
{
    // 하단 그리드는 가로 3칸 — 원작 화면처럼 윗줄(돈 도박)과 아랫줄(유닛 도박)이 줄로 갈리게,
    // 9칸 중 인덱스 6 다음 두 칸(7,8)은 항상 빈 칸으로 둔다.
    //   [0 1 2] 10엔 도박/500엔 도박/특성포인트 구매 — 사장님 확정: 초급도박은 없다
    //   [3 4 5] 하급/중급/고급 유닛 도박
    //   [6 _ _] 다른세계 유닛 도박
    [SerializeField] List<GamblingOptionData> moneyOptions = new List<GamblingOptionData>();
    [SerializeField] List<GamblingOptionData> unitOptions = new List<GamblingOptionData>();
    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;

    // 특성포인트 구매(인덱스 2) — 사장님 확정(2026-09-03): 15,000엔으로 1회만.
    // 원작은 "돈 도박 졸업 후 구매"라 이 상점의 돈 도박 줄에 자리를 잡았다(구현담당1 판단).
    // GamblingOptionData로 안 만든 이유: 그 데이터는 "확률로 얼마를 돌려받는가"를 표현하는
    // 모델이라, "100% 확정으로 골드가 아닌 걸 준다, 딱 1회"인 이 구매와 모양이 안 맞는다.
    [SerializeField] int traitPointPurchaseCost = 15000;
    const int TraitPointSlotIndex = 2;

    static readonly Color MoneyColor = new Color(1f, 0.82f, 0.25f); // 금색 — MapGenerator 코인 아이콘과 같은 색

    struct SlotCache
    {
        public string label;
        public Color color;
        public bool hasOption;
    }

    readonly SlotCache[] slotCache = new SlotCache[SlotCountValue];
    bool slotCacheBuilt;

    const int SlotCountValue = 9;

    OwnedByPlayer owner;

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
    }

    void OnEnable()
    {
        EnemyDummy.OnBossKilled += HandleBossKilled;
    }

    void OnDisable()
    {
        EnemyDummy.OnBossKilled -= HandleBossKilled;
    }

    // 어느 레인의 보스든(누구 것이든) 죽으면 그 라운드에 도달했다는 팀 전체의 진행이므로,
    // 이 상점의 주인만 해금한다 — "내 레인 보스를 잡아야만"이 아니다. 4인 플레이면 보스가
    // 레인마다 하나씩(최대 4마리) 죽어 이 핸들러가 여러 번 불릴 수 있는데, GamblingProgress.Unlock은
    // HashSet.Add라 몇 번을 불러도 무해하다.
    void HandleBossKilled(int roundNumber)
    {
        GamblingProgress progress = OwnerContext?.GamblingProgress;
        if (progress == null) return;

        UnlockMatching(moneyOptions, roundNumber, progress);
        UnlockMatching(unitOptions, roundNumber, progress);
    }

    static void UnlockMatching(List<GamblingOptionData> options, int roundNumber, GamblingProgress progress)
    {
        foreach (GamblingOptionData option in options)
        {
            if (option != null && option.requiresUnlock && option.unlockRound == roundNumber)
                progress.Unlock(option);
        }
    }

    // ---- ILaneShop ----

    public int SlotCount => SlotCountValue;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (!slotCacheBuilt) BuildSlotCache();
        if (index < 0 || index >= SlotCountValue) return LaneShopSlotView.Empty;

        if (index == TraitPointSlotIndex)
            return new LaneShopSlotView(slotCache[index].label, slotCache[index].color,
                CanPurchaseTraitPoint(), LaneShopTargetKind.None);

        SlotCache cache = slotCache[index];
        if (!cache.hasOption) return LaneShopSlotView.Empty;

        bool available = CanRoll(OptionAt(index));
        return new LaneShopSlotView(cache.label, cache.color, available, LaneShopTargetKind.None);
    }

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다(GetSlotView는 캐시된 값만 돌려준다).
    public string GetSlotTooltip(int index)
    {
        if (index == TraitPointSlotIndex) return BuildTraitPointTooltip();

        GamblingOptionData option = OptionAt(index);
        if (option == null) return null;

        return option.category == GamblingCategory.Money ? BuildMoneyTooltip(option) : BuildUnitTooltip(option);
    }

    public bool TryUse(int index, LaneShopTarget target)
    {
        if (index == TraitPointSlotIndex) return TryPurchaseTraitPoint();

        GamblingOptionData option = OptionAt(index);
        return option != null && TryRoll(option);
    }

    GamblingOptionData OptionAt(int index)
    {
        if (index >= 0 && index <= 2)
            return index < moneyOptions.Count ? moneyOptions[index] : null;

        if (index >= 3 && index <= 5)
        {
            int i = index - 3;
            return i < unitOptions.Count ? unitOptions[i] : null;
        }

        if (index == 6)
            return unitOptions.Count > 3 ? unitOptions[3] : null;

        return null; // 7, 8은 줄을 맞추기 위한 항상 빈 칸.
    }

    void BuildSlotCache()
    {
        slotCacheBuilt = true;

        for (int i = 0; i < SlotCountValue; i++)
        {
            if (i == TraitPointSlotIndex)
            {
                slotCache[i] = new SlotCache { hasOption = true, label = "특성포인트 구매", color = MoneyColor };
                continue;
            }

            GamblingOptionData option = OptionAt(i);
            slotCache[i] = option == null
                ? new SlotCache { hasOption = false }
                : new SlotCache
                {
                    hasOption = true,
                    label = option.optionName,
                    color = option.category == GamblingCategory.Money ? MoneyColor : GradeColor(option.primaryResultGrade),
                };
        }
    }

    string BuildTraitPointTooltip()
    {
        UnitUpgrades upgrades = OwnerContext?.UnitUpgrades;

        if (upgrades != null && upgrades.HasPurchasedPoint)
            return $"특성포인트 구매\n이미 구매함 (1회 한정)";

        return $"특성포인트 구매\n비용: {traitPointPurchaseCost}엔\n특성포인트 1개를 즉시 받습니다 (1회 한정)";
    }

    bool CanPurchaseTraitPoint()
    {
        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;
        if (context.UnitUpgrades.HasPurchasedPoint) return false;

        return context.GoldWallet.Gold >= traitPointPurchaseCost;
    }

    bool TryPurchaseTraitPoint()
    {
        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;

        bool bought = context.UnitUpgrades.TryPurchasePoint(context.GoldWallet, traitPointPurchaseCost);
        if (bought)
            Debug.Log($"[도박] 특성포인트 구매: {traitPointPurchaseCost}엔 → 특성포인트 1개. 보유 {context.UnitUpgrades.TraitPoints}개.");

        return bought;
    }

    string BuildMoneyTooltip(GamblingOptionData option)
    {
        string tooltip = $"{option.optionName}\n{option.description}\n"
            + $"비용: {option.cost}엔\n"
            + $"결과: {option.successGoldMin}~{option.successGoldMax}엔 (0엔이 나올 수도 있습니다)";

        string reason = MoneyUnavailableReason(option);
        if (reason != null) tooltip += $"\n⚠️ {reason}";

        return tooltip;
    }

    // 잠긴 칸도 보여야 하니(available=false), 왜 못 쓰는지 이유를 데이터에서 그대로 읽어 붙인다.
    string MoneyUnavailableReason(GamblingOptionData option)
    {
        GamblingProgress progress = OwnerContext?.GamblingProgress;

        if (option.requiresUnlock && (progress == null || !progress.IsUnlocked(option)))
            return string.IsNullOrEmpty(option.unlockHint) ? "아직 해금되지 않음" : option.unlockHint;

        if (option.maxUses > 0 && progress != null && progress.UsesSoFar(option) >= option.maxUses)
            return $"{option.maxUses}회 모두 사용함";

        return null;
    }

    string BuildUnitTooltip(GamblingOptionData option)
    {
        string resultDesc = option.useSecondaryGrade
            ? $"{option.primaryResultGrade.KoreanName()} 또는 {option.secondaryResultGrade.KoreanName()}"
            : option.primaryResultGrade.KoreanName();

        string failDesc = option.grantFailureReward
            ? $"행운의토큰 {option.failureLuckyTokens} + 목재 {option.failureWood}"
            : "없음";

        return $"{option.optionName}\n{option.description}\n"
             + $"비용: {ResourceLabel(option.costResourceType)} {option.cost}\n성공 확률: {option.successChancePercent:F0}%\n"
             + $"성공 시: {resultDesc}\n실패 시: {failDesc}";
    }

    static string ResourceLabel(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "목재";
            case ResourceType.Token: return "토큰";
            case ResourceType.LuckyToken: return "행운의토큰";
            case ResourceType.Mana: return "마나";
            default: return type.ToString();
        }
    }

    // GameHud가 유닛 카드에 쓰는 것과 같은 규칙. GameHud.cs를 참조할 수 없어(다른 세션 작업 중)
    // 값을 그대로 복제했다 — MapGenerator.GradeColor에도 이미 같은 복제가 있다.
    static Color GradeColor(UnitGrade grade)
    {
        switch (grade)
        {
            case UnitGrade.Legendary: return new Color(0.85f, 0.2f, 0.2f);
            case UnitGrade.Rare: return new Color(0.6f, 0.3f, 0.85f);
            case UnitGrade.Special: return new Color(0.9f, 0.85f, 0.2f);
            case UnitGrade.Hidden: return new Color(0.25f, 0.45f, 0.9f);
            case UnitGrade.Common:
            case UnitGrade.Uncommon: return new Color(0.3f, 0.7f, 0.35f);
            default: return new Color(0.4f, 0.4f, 0.4f);
        }
    }

    public bool CanRoll(GamblingOptionData option)
    {
        if (option == null) return false;
        if (option.category == GamblingCategory.Unit && (unitSpawner == null || gachaTable == null)) return false;

        PlayerContext context = OwnerContext;
        if (context == null) return false;

        if (option.category == GamblingCategory.Money)
        {
            GamblingProgress progress = context.GamblingProgress;
            if (progress == null) return false;
            if (option.requiresUnlock && !progress.IsUnlocked(option)) return false;
            if (option.maxUses > 0 && progress.UsesSoFar(option) >= option.maxUses) return false;

            return context.GoldWallet != null && context.GoldWallet.Gold >= option.cost;
        }

        if (context.ResourceWallet == null) return false;
        if (context.ResourceWallet.Get(option.costResourceType) < option.cost) return false;

        // 원작 유닛 도박은 골드와 자원을 같이 받는다. 둘 중 하나만 모자라도 못 돌린다.
        return option.goldCost <= 0
               || (context.GoldWallet != null && context.GoldWallet.Gold >= option.goldCost);
    }

    public bool TryRoll(GamblingOptionData option)
    {
        if (!CanRoll(option)) return false;

        PlayerContext context = OwnerContext;
        if (context == null) return false;

        return option.category == GamblingCategory.Money
            ? TryRollMoney(option, context)
            : TryRollUnit(option, context);
    }

    // 성공/실패 구분이 없다 — 걸고 나면 항상 결과 범위(0 포함) 안에서 얼마를 받는다.
    bool TryRollMoney(GamblingOptionData option, PlayerContext context)
    {
        if (context.GoldWallet == null || !context.GoldWallet.TrySpend(option.cost)) return false;

        // 성공률이 0이면 옛 에셋(성공/실패 구분 없이 항상 지급)으로 보고 성공 취급한다.
        bool success = option.successChancePercent <= 0f
                       || Random.Range(0f, 100f) < option.successChancePercent;

        int amount = success
            ? Random.Range(option.successGoldMin, option.successGoldMax + 1)
            : Random.Range(option.failureGoldMin, option.failureGoldMax + 1);
        if (amount > 0) context.GoldWallet.Add(amount);

        context.GamblingProgress?.RecordUse(option);

        // 결과를 말해주지 않으면 눌러도 아무 일도 안 일어난 것처럼 보인다 — 0엔이 나오는
        // 판이 있어서 더 그렇다. 남은 횟수까지 같이 알려준다.
        int used = context.GamblingProgress != null ? context.GamblingProgress.UsesSoFar(option) : 0;
        string left = option.maxUses > 0 ? $", 남은 횟수 {option.maxUses - used}" : "";
        Debug.Log($"[도박] {option.optionName}: {option.cost}엔 걸어 {amount}엔 " +
                  $"({(amount >= option.cost ? "이득" : "손해")}). 보유 {context.GoldWallet.Gold}엔{left}");

        return true;
    }

    bool TryRollUnit(GamblingOptionData option, PlayerContext context)
    {
        if (unitSpawner == null || gachaTable == null) return false;

        bool success = Random.Range(0f, 100f) < option.successChancePercent;

        // 당첨 시 지급할 등급을 자원 차감 전에 미리 정하고, 그 등급 pool이 비어있으면
        // 통째로 취소한다 — unitSpawner가 없거나 지급할 유닛이 없는데 자원만 나가면
        // 완전한 손실이 된다(UnitInventory가 인스턴스 등록부로 바뀌어 등록 경로가
        // UnitSpawner.Spawn 하나뿐이라 더 그렇다).
        UnitGrade resultGrade = default;
        if (success)
        {
            resultGrade = PickResultGrade(option);
            if (!HasPool(resultGrade))
            {
                Debug.LogWarning($"{name}: {option.optionName}의 {resultGrade} 풀이 비어있어 도박을 진행하지 않았습니다.");
                return false;
            }
        }

        // 골드를 먼저 뺀다. 자원을 먼저 빼고 골드가 모자라면 자원만 날아간다 —
        // CanRoll이 둘 다 봤어도 그 사이에 다른 경로로 골드가 줄 수 있다.
        if (option.goldCost > 0)
        {
            if (context.GoldWallet == null || !context.GoldWallet.TrySpend(option.goldCost))
                return false;
        }

        if (context.ResourceWallet == null || !context.ResourceWallet.TrySpend(option.costResourceType, option.cost))
        {
            // 자원 차감이 실패하면 이미 빠진 골드를 되돌린다.
            if (option.goldCost > 0 && context.GoldWallet != null)
                context.GoldWallet.Add(option.goldCost);
            return false;
        }

        if (success)
        {
            UnitData reward = gachaTable.RollFromGrade(resultGrade);
            unitSpawner.Spawn(reward, ResolveSpawnPosition(reward), owner.OwnerId);
        }
        else if (option.grantFailureReward)
        {
            context.ResourceWallet.Add(ResourceType.LuckyToken, option.failureLuckyTokens);
            context.ResourceWallet.Add(ResourceType.Wood, option.failureWood);
        }

        return true;
    }

    UnitGrade PickResultGrade(GamblingOptionData option)
    {
        if (!option.useSecondaryGrade) return option.primaryResultGrade;

        float primaryWeight = FindWeight(option.primaryResultGrade);
        float secondaryWeight = FindWeight(option.secondaryResultGrade);
        float total = primaryWeight + secondaryWeight;

        if (total <= 0f) return option.primaryResultGrade;

        return Random.Range(0f, total) < primaryWeight ? option.primaryResultGrade : option.secondaryResultGrade;
    }

    bool HasPool(UnitGrade grade)
    {
        if (gachaTable.entries == null) return false;

        foreach (GachaTable.GradeEntry entry in gachaTable.entries)
            if (entry != null && entry.grade == grade)
                return entry.pool != null && entry.pool.Count > 0;

        return false;
    }

    float FindWeight(UnitGrade grade)
    {
        if (gachaTable.entries == null) return 0f;

        foreach (GachaTable.GradeEntry entry in gachaTable.entries)
            if (entry != null && entry.grade == grade)
                return entry.weight;

        return 0f;
    }

    Vector3 ResolveSpawnPosition(UnitData reward)
    {
        LaneMarker lane = LaneMarker.Get(owner.OwnerId);
        if (lane != null) return lane.TakeSpawnPosition(reward);

        Debug.LogWarning($"{name}: 플레이어 {owner.OwnerId}의 레인을 찾지 못해 상점 자리에 소환합니다.", this);
        return transform.position;
    }
}
