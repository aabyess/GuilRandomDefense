using System.Collections.Generic;
using UnityEngine;

// 도박소: 도움소(SupportShop)와 같은 구조 — 레인 안에 서 있고, 선택하면 하단에 옵션 칸이 뜨고,
// 칸을 누르면 자원을 쓴다. 파괴 불가, 적 타겟에서 자동 제외된다 — SupportShop과 같은 이유로
// EnemyDummy.Active/DestructibleGate.Active 어디에도 등록되지 않는다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class GamblingShop : MonoBehaviour, ILaneShop
{
    // 하단 그리드는 가로 3칸 — 원작 화면처럼 윗줄(돈 도박 3칸)과 아랫줄(유닛 도박 4칸)이
    // 줄로 갈리게, 9칸 중 인덱스 6 다음 두 칸(7,8)은 항상 빈 칸으로 둔다.
    //   [0 1 2] 초급/중급/고급 돈 도박
    //   [3 4 5] 하급/중급/고급 유닛 도박
    //   [6 _ _] 다른세계 유닛 도박
    [SerializeField] List<GamblingOptionData> moneyOptions = new List<GamblingOptionData>();
    [SerializeField] List<GamblingOptionData> unitOptions = new List<GamblingOptionData>();
    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;

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

    // ---- ILaneShop ----

    public int SlotCount => SlotCountValue;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (!slotCacheBuilt) BuildSlotCache();
        if (index < 0 || index >= SlotCountValue) return LaneShopSlotView.Empty;

        SlotCache cache = slotCache[index];
        if (!cache.hasOption) return LaneShopSlotView.Empty;

        bool available = CanRoll(OptionAt(index));
        return new LaneShopSlotView(cache.label, cache.color, available, LaneShopTargetKind.None);
    }

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다(GetSlotView는 캐시된 값만 돌려준다).
    public string GetSlotTooltip(int index)
    {
        GamblingOptionData option = OptionAt(index);
        if (option == null) return null;

        return option.category == GamblingCategory.Money ? BuildMoneyTooltip(option) : BuildUnitTooltip(option);
    }

    public bool TryUse(int index, LaneShopTarget target)
    {
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

    string BuildMoneyTooltip(GamblingOptionData option)
    {
        return $"{option.optionName}\n{option.description}\n"
             + $"비용: 골드 {option.cost}\n성공 확률: {option.successChancePercent:F0}%\n"
             + $"성공 시: 골드 {option.successGoldMin}~{option.successGoldMax}\n"
             + $"실패 시: 골드 {option.failureGoldMin}~{option.failureGoldMax} 반환";
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

        if (option.category == GamblingCategory.Money
            && context.GamblingProgress != null && context.GamblingProgress.IsMoneyGamblingGraduated)
            return false;

        if (option.category == GamblingCategory.Money)
            return context.GoldWallet != null && context.GoldWallet.Gold >= option.cost;

        return context.ResourceWallet != null && context.ResourceWallet.Get(option.costResourceType) >= option.cost;
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

    bool TryRollMoney(GamblingOptionData option, PlayerContext context)
    {
        if (context.GoldWallet == null || !context.GoldWallet.TrySpend(option.cost)) return false;

        bool success = Random.Range(0f, 100f) < option.successChancePercent;
        int amount = success
            ? Random.Range(option.successGoldMin, option.successGoldMax + 1)
            : Random.Range(option.failureGoldMin, option.failureGoldMax + 1);

        if (amount > 0)
        {
            context.GoldWallet.Add(amount);
            if (success) context.GamblingProgress?.AddMoneyGamblingWinnings(amount);
        }

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

        if (context.ResourceWallet == null || !context.ResourceWallet.TrySpend(option.costResourceType, option.cost))
            return false;

        if (success)
        {
            UnitData reward = gachaTable.RollFromGrade(resultGrade);
            unitSpawner.Spawn(reward, ResolveSpawnPosition(), owner.OwnerId);
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

    Vector3 ResolveSpawnPosition()
    {
        LaneMarker lane = LaneMarker.Get(owner.OwnerId);
        if (lane != null) return lane.transform.position;

        Debug.LogWarning($"{name}: 플레이어 {owner.OwnerId}의 레인을 찾지 못해 상점 자리에 소환합니다.", this);
        return transform.position;
    }
}
