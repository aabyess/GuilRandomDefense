using System.Collections.Generic;
using UnityEngine;

// 도박소: 도움소(SupportShop)와 같은 구조 — 레인 안에 서 있고, 선택하면 하단에 옵션 칸이 뜨고,
// 칸을 누르면 자원을 쓴다. 파괴 불가, 적 타겟에서 자동 제외된다 — SupportShop과 같은 이유로
// EnemyDummy.Active/DestructibleGate.Active 어디에도 등록되지 않는다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class GamblingShop : MonoBehaviour, IPagedLaneShop
{
    // 하단 그리드는 가로 3칸 — 원작 화면처럼 윗줄(돈 도박)과 아랫줄(유닛 도박)이 줄로 갈리게,
    // 9칸 중 인덱스 6 다음 두 칸(7,8)은 항상 빈 칸으로 둔다.
    //   [0 1 2] 10엔 도박/500엔 도박/특성포인트 구매 — 사장님 확정: 초급도박은 없다
    //   [3 4 5] 하급/중급/고급 유닛 도박
    //   [6 _ _] 다른세계 유닛 도박
    [SerializeField] List<GamblingOptionData> moneyOptions = new List<GamblingOptionData>();
    [SerializeField] List<GamblingOptionData> unitOptions = new List<GamblingOptionData>();
    [SerializeField] GachaTable gachaTable;
    // 해적단 퀘스트(2026-10-04 합침) — 같은 건물에 붙은 PirateQuestShop. 8번 칸 「해적단 ▶」을 누르면 칸이 퀘스트 목록으로 바뀌고
    // 같은 8번 칸이 「◀ 뒤로」가 된다. 쪽은 화면마다 따로(IPagedLaneShop) — 서버엔 절대 번호(PirateNetBase+퀘스트 번호)만 간다.
    [SerializeField] PirateQuestShop pirate;
    [SerializeField] UnitSpawner unitSpawner;

    // ⚠️ 맨 뒤에 추가(2026-09-07, "희귀함 리롤" A0VX) — GamblingOptionData.
    // grantsUniqueRerollOnGenericSuccess가 켜진 옵션의 "일반 결과" 성공 스폰에 붙일
    // 능력 수치. 비어 있으면(씬 배선 전) 해당 분기는 조용히 건너뛴다 — 회귀 없음.
    [SerializeField] UniqueRerollAbilityData uniqueRerollAbilityData;

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
    const int PageSlot = 8;              // 「해적단 ▶」 / 「◀ 뒤로」 칸(원래 항상 빈 칸)
    const int PirateNetBase = 100;       // TryUse(절대 번호) — 100 + 퀘스트 번호
    public const int TokenSlot = 7;      // 해적단 쪽 마지막 칸 = 「행운의 토큰 사용」(원작 A0BC) — 퀘스트는 0~6
    const int PirateVisibleSlots = 8;    // 해적단 쪽에서 퀘스트가 쓸 수 있는 칸 0~7(8은 뒤로)
    static readonly Color PageColor = new Color(0.75f, 0.55f, 0.2f);   // 해적단 퀘스트 칸과 같은 청동색
    bool pirateOpen;                     // 이 화면(클라)에서 해적단 쪽을 보는 중 — 서버 상태 아님

    OwnedByPlayer owner;

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    void Awake()
    {
        // 🔴 배선이 빠지면 유닛 도박 칸이 영구히 비활성인데 예전엔 문구도 로그도 없었다 —
        //    같은 파일의 다른 실패(골드 부족 등)는 전부 알리는데 이 자리만 침묵이라
        //    개발자도 원인을 못 찾았다. 시작할 때 한 번 알린다.
        if (unitSpawner == null || gachaTable == null)
            Debug.LogWarning($"GamblingShop({name}): " +
                             (unitSpawner == null ? "unitSpawner " : "") +
                             (gachaTable == null ? "gachaTable " : "") +
                             "가 배선되지 않아 유닛 도박이 동작하지 않습니다.", this);

        owner = GetComponent<OwnedByPlayer>();
    }

    void OnEnable()
    {
        EnemyDummy.OnLaneBossKilled += HandleBossKilled;
    }

    void OnDisable()
    {
        EnemyDummy.OnLaneBossKilled -= HandleBossKilled;
    }

    // 어느 레인의 보스든(누구 것이든) 죽으면 그 라운드에 도달했다는 팀 전체의 진행이므로,
    // 이 상점의 주인만 해금한다 — "내 레인 보스를 잡아야만"이 아니다. 4인 플레이면 보스가
    // 레인마다 하나씩(최대 4마리) 죽어 이 핸들러가 여러 번 불릴 수 있는데, GamblingProgress.Unlock은
    // HashSet.Add라 몇 번을 불러도 무해하다.
    // 🔴 09-27 정정: 원작 Rhse(R10)·Rhde(R20)는 **보스가 든 레인의 주인만** 해금한다(SetPlayerTechResearchedSwap(…, Player(BossRectInt)), j:13448·13459).
    //    예전엔 아무 레인 보스가 죽으면 전원이 열렸다 — 솔로는 같고 멀티에서 느린 사람이 공짜로 열렸다.
    void HandleBossKilled(int roundNumber, int laneIndex)
    {
        PlayerContext ownerContext = OwnerContext;
        if (ownerContext == null || ownerContext.PlayerId != laneIndex) return;
        GamblingProgress progress = ownerContext.GamblingProgress;
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

    // ---- IPagedLaneShop ----

    public bool TryChangePage(int visibleIndex)
    {
        if (pirate == null || visibleIndex != PageSlot) return false;
        pirateOpen = !pirateOpen;
        return true;
    }

    public int ToNetSlot(int visibleIndex) => pirateOpen && visibleIndex != PageSlot ? PirateNetBase + visibleIndex : visibleIndex;

    public void ResetPage() => pirateOpen = false;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (pirate != null)
        {
            if (index == PageSlot)
                return new LaneShopSlotView(pirateOpen ? "◀ 뒤로" : "해적단 ▶", PageColor, true, LaneShopTargetKind.None);
            if (pirateOpen)
            {
                if (index == TokenSlot && TokenOption != null) return TokenSlotView();
                return index >= 0 && index < PirateVisibleSlots && index < pirate.SlotCount ? pirate.GetSlotView(index) : LaneShopSlotView.Empty;
            }
        }
        if (!slotCacheBuilt) BuildSlotCache();
        if (index < 0 || index >= SlotCountValue) return LaneShopSlotView.Empty;

        if (index == TraitPointSlotIndex)
            return new LaneShopSlotView(slotCache[index].label, slotCache[index].color,
                CanPurchaseTraitPoint(), LaneShopTargetKind.None);

        VisibleMoney();   // 졸업하면 칸이 바뀐다 — 캐시를 무효화할 기회를 먼저 준다
        if (!slotCacheBuilt) BuildSlotCache();
        SlotCache cache = slotCache[index];
        if (!cache.hasOption) return LaneShopSlotView.Empty;

        GamblingOptionData opt = OptionAt(index);
        bool available = CanRoll(opt);
        // 충전식은 재고 0일 때만 칸을 덮는다(PM 권장 — 재고 1 이상이면 칸은 밝게). 남은 시간은 「다음 1개까지」.
        float cdRemaining = 0f, cdTotal = 0f;
        GamblingProgress stockProgress = OwnerContext?.GamblingProgress;
        if (opt != null && opt.stockMax > 0 && opt.stockRegenSeconds > 0f && stockProgress != null
            && (!opt.requiresUnlock || stockProgress.IsUnlocked(opt)) && stockProgress.Stock(opt) <= 0)
        {
            cdRemaining = stockProgress.SecondsToNextStock(opt);
            cdTotal = opt.stockRegenSeconds;
        }
        return new LaneShopSlotView(cache.label + StockSuffix(opt), cache.color, available, LaneShopTargetKind.None,
                                    0f, '\0', cdRemaining, cdTotal);
    }

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다(GetSlotView는 캐시된 값만 돌려준다).
    public string GetSlotTooltip(int index)
    {
        if (pirate != null)
        {
            if (index == PageSlot) return pirateOpen ? "도박소로 돌아갑니다" : "해적단 퀘스트 — 미니보스 퇴치 의뢰를 삽니다";
            if (pirateOpen)
            {
                if (index == TokenSlot && TokenOption != null) return TokenTooltip();
                return index >= 0 && index < PirateVisibleSlots && index < pirate.SlotCount ? pirate.GetSlotTooltip(index) : null;
            }
        }
        if (index == TraitPointSlotIndex) return BuildTraitPointTooltip();

        GamblingOptionData option = OptionAt(index);
        if (option == null) return null;

        return option.category == GamblingCategory.Money ? BuildMoneyTooltip(option) : BuildUnitTooltip(option);
    }

    public bool TryUse(int index, LaneShopTarget target, out string failReason)
    {
        // 절대 번호: 100 이상 = 해적단 퀘스트(서버·호스트·싱글 모두). 쪽 넘김 칸은 HUD가 TryChangePage로 먼저 처리해 여기 안 온다.
        if (index >= PirateNetBase)
        {
            failReason = null;
            if (index - PirateNetBase == TokenSlot && TokenOption != null) return TryTokenExchange(out failReason);
            return pirate != null && index - PirateNetBase < PirateVisibleSlots && pirate.TryUse(index - PirateNetBase, target, out failReason);
        }
        if (index == PageSlot) { failReason = null; return false; }   // 8번은 쪽 넘김 전용 — 도박 옵션이 없는 빈 칸
        if (index == TraitPointSlotIndex) return TryPurchaseTraitPoint(out failReason);

        failReason = null;
        GamblingOptionData option = OptionAt(index);
        return option != null && TryRoll(option, out failReason);
    }

    // ── 행운의 토큰 사용(원작 A0BC, 2026-10-07) ──
    GamblingOptionData TokenOption => unitOptions.Find(o => o != null && o.tokenExchange);

    LaneShopSlotView TokenSlotView()
    {
        GamblingOptionData option = TokenOption;
        int have = OwnerContext?.ResourceWallet != null ? OwnerContext.ResourceWallet.Get(option.costResourceType) : 0;
        return new LaneShopSlotView($"행운의 토큰 사용\n토큰 {have}/{option.cost}", new Color(0.55f, 0.85f, 0.5f), have >= option.cost, LaneShopTargetKind.None);
    }

    string TokenTooltip()
    {
        GamblingOptionData o = TokenOption;
        return $"{o.optionName}\n행운의 토큰 {o.cost}개를 써서 위습을 받습니다.\n{o.tokenSpecialChancePercent:F0}% {(o.tokenWispSpecial != null ? o.tokenWispSpecial.wispName : "특별함 위습")} · {100f - o.tokenSpecialChancePercent:F0}% {(o.tokenWispRare != null ? o.tokenWispRare.wispName : "희귀함 위습")}";
    }

    // 원작 Trig_Token: 토큰 3개 이상이면 80%/20%로 위습 하나, 토큰 3개 소모(j 82927~83019). 위습은 RewardDistributor.GrantWisps로(원작 StoryReward_Base3/4 자리 = 우리 위습 칸).
    bool TryTokenExchange(out string failReason)
    {
        failReason = null;
        GamblingOptionData option = TokenOption;
        PlayerContext context = OwnerContext;
        if (option == null || context == null || context.ResourceWallet == null || RewardDistributor.Instance == null) return false;
        if (context.ResourceWallet.Get(option.costResourceType) < option.cost) { failReason = "행운의토큰이 부족합니다!"; return false; }
        if (!context.ResourceWallet.TrySpend(option.costResourceType, option.cost)) { failReason = "행운의토큰이 부족합니다!"; return false; }
        bool special = Random.Range(0f, 100f) < option.tokenSpecialChancePercent;
        WispData wisp = special ? option.tokenWispSpecial : option.tokenWispRare;
        if (wisp != null)
            RewardDistributor.Instance.GrantWisps(context, new List<WispReward> { new WispReward { wisp = wisp, count = 1 } });
        PlayerNotification.Show(owner.OwnerId, "<color=#FF8200>☆★         행운의 토큰사용 !!         ★☆</color>", 10f);
        PlayerNotification.Show(owner.OwnerId, special
            ? "<color=#2040F0>☆★         특별함 위습 획득 !!         ★☆</color>"
            : "<color=#FF00FF>☆★         희귀함 위습 획득 !!         ★☆</color>", 10f);
        return true;
    }

    // 돈 도박 줄(0·1)에 보이는 옵션 — 졸업 전: 졸업 조건이 없는 것(10엔·500엔), 졸업 뒤: 졸업해서 없어지지 않는 것
    //    (원작 h062 → h08C: 돈도박 초급·고급이 빠지고 고급 유닛 생성·목재 구입이 들어온다). 2번은 특성포인트 고정.
    readonly List<GamblingOptionData> visibleMoney = new List<GamblingOptionData>();
    bool visibleGraduated = false;
    bool visibleBuilt;

    List<GamblingOptionData> VisibleMoney()
    {
        bool graduated = OwnerContext?.GamblingProgress != null && OwnerContext.GamblingProgress.Graduated;
        if (!visibleBuilt || graduated != visibleGraduated)
        {
            visibleBuilt = true;
            visibleGraduated = graduated;
            visibleMoney.Clear();
            foreach (GamblingOptionData o in moneyOptions)
                if (o != null && (graduated ? !o.retiredOnGraduation : !o.requiresGraduation)) visibleMoney.Add(o);
            slotCacheBuilt = false;   // 칸 글자·색도 다시
        }
        return visibleMoney;
    }

    GamblingOptionData OptionAt(int index)
    {
        if (index >= 0 && index <= 1)
        {
            List<GamblingOptionData> money = VisibleMoney();
            return index < money.Count ? money[index] : null;
        }

        if (index >= 3 && index <= 5)
        {
            int i = index - 3;
            return i < unitOptions.Count ? unitOptions[i] : null;
        }

        if (index == 6)
            return unitOptions.Count > 3 ? unitOptions[3] : null;

        // 조도연 좆돼지 도박(구 압살롬 도박 h069 — 사장님 10-07: 좆돼지로 통합, 8라운드 시작에 열림) — 다른세계 도박 옆 칸. unitOptions[5]는 행운의 토큰 사용(해적단 쪽 7번 칸).
        if (index == 7)
            return unitOptions.Count > 4 ? unitOptions[4] : null;

        return null; // 8은 줄을 맞추기 위한 항상 빈 칸.
    }

    // 「남은 개수/최대 · 다음 충전까지 초」 — 워크3 상점의 재고 숫자·충전 원과 같은 정보(친구 베타 「왜 안 눌리지」 방지, PM 09-26).
    string StockSuffix(GamblingOptionData option)
    {
        GamblingProgress progress = OwnerContext?.GamblingProgress;
        if (option == null || progress == null) return "";
        if (option.requiresUnlock && !progress.IsUnlocked(option)) return "\n(잠김)";   // 재고 없는 유닛 도박(다른세계: 스토리 9번 뒤 해금)도 칸에 잠김을 쓴다
        if (option.stockMax <= 0) return "";
        // 재고가 있어도 스토리 조건에 막히면 「1/1」만 보고 「왜 안 눌리지」가 된다(09-26 확인 판) — 막는 조건을 칸에 쓴다.
        if (!StoryRequirementMet(option))
            return $"\n스토리 {(StoryManager.Instance != null ? StoryManager.Instance.FinishedCount : 0)}/{option.requiresStoriesCleared}";
        int n = progress.Stock(option);
        float next = progress.SecondsToNextStock(option);
        return n >= option.stockMax ? $"\n{n}/{option.stockMax}" : $"\n{n}/{option.stockMax} · {Mathf.CeilToInt(next)}초";
    }

    bool StoryRequirementMet(GamblingOptionData option) =>
        option.requiresStoriesCleared <= 0 || (StoryManager.Instance != null && StoryManager.Instance.FinishedCount >= option.requiresStoriesCleared);

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

    public string GetUnavailableReason(int index)
    {
        if (pirate != null && pirateOpen)
            return index >= 0 && index < PirateVisibleSlots && index < pirate.SlotCount ? pirate.GetUnavailableReason(index) : null;
        if (index == TraitPointSlotIndex)
        {
            PlayerContext context = OwnerContext;
            if (context?.UnitUpgrades == null) return null;
            return context.UnitUpgrades.HasPurchasedPoint ? "이미 구매했습니다 (1회 한정)." : "골드가 부족합니다.";
        }
        return UnavailableReason(OptionAt(index));
    }

    bool CanPurchaseTraitPoint()
    {
        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;
        if (context.UnitUpgrades.HasPurchasedPoint) return false;

        return context.GoldWallet.Gold >= traitPointPurchaseCost;
    }

    bool TryPurchaseTraitPoint(out string failReason)
    {
        failReason = null;
        PlayerContext context = OwnerContext;
        if (context == null || context.UnitUpgrades == null || context.GoldWallet == null) return false;

        if (context.UnitUpgrades.HasPurchasedPoint)
        {
            failReason = "이미 구매했습니다 (1회 한정).";
            return false;
        }

        bool bought = context.UnitUpgrades.TryPurchasePoint(context.GoldWallet, traitPointPurchaseCost);
        if (bought)
        {
            Debug.Log($"[도박] 특성포인트 구매: {traitPointPurchaseCost}엔 → 특성포인트 1개. 보유 {context.UnitUpgrades.TraitPoints}개.");
            GameSound.PlayFor(context.PlayerId, GameSoundId.Coin);   // 원작 Money_trade2(h0AL 특성 포인트)
        }
        else
        {
            failReason = "골드가 부족합니다.";
        }

        return bought;
    }

    string BuildMoneyTooltip(GamblingOptionData option)
    {
        string tooltip = $"{option.optionName}\n{PlayerFacingText.Clean(option.description)}\n"
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
        string stock = StockReason(option, progress);
        if (stock != null) return stock;

        if (option.requiresUnlock && (progress == null || !progress.IsUnlocked(option)))
            return string.IsNullOrEmpty(option.unlockHint) ? "아직 해금되지 않음" : option.unlockHint;

        if (option.maxUses > 0 && progress != null && progress.UsesSoFar(option) >= option.maxUses)
            return $"{option.maxUses}회 모두 사용함";

        return null;
    }

    string StockReason(GamblingOptionData option, GamblingProgress progress)
    {
        if (!StoryRequirementMet(option)) return $"스토리를 {option.requiresStoriesCleared}개 깨야 합니다";
        if (option.stockMax <= 0 || progress == null) return null;
        if (option.requiresUnlock && !progress.IsUnlocked(option)) return null;   // 잠김 이유는 아래 해금 문구가 말한다
        if (progress.Stock(option) > 0) return null;
        return $"재고 없음 — {Mathf.CeilToInt(progress.SecondsToNextStock(option))}초 뒤 1개 충전(최대 {option.stockMax})";
    }

    string BuildUnitTooltip(GamblingOptionData option)
    {
        string resultDesc = option.useSecondaryGrade
            ? $"{option.primaryResultGrade.KoreanName()} 또는 {option.secondaryResultGrade.KoreanName()}"
            : option.primaryResultGrade.KoreanName();

        string failDesc = option.grantFailureReward
            ? $"행운의토큰 {FailureLuckyTokens(option, OwnerContext)} + 목재 {option.failureWood}"
            : "없음";

        string lockNote = option.requiresUnlock && !(OwnerContext?.GamblingProgress?.IsUnlocked(option) ?? false)
            ? $"\n<color=#FF7070>잠김 — {(string.IsNullOrEmpty(option.unlockHint) ? "아직 해금되지 않음" : option.unlockHint)}</color>"
            : "";
        return $"{option.optionName}\n{PlayerFacingText.Clean(option.description)}\n"
             + $"비용: {ResourceLabel(option.costResourceType)} {option.cost}\n성공 확률: {EffectiveSuccessChance(option, OwnerContext):F0}%{BoostNote(option, OwnerContext)}\n"
             + $"성공 시: {resultDesc}\n실패 시: {failDesc}{lockNote}";
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
            case UnitGrade.Common: return new Color(0.3f, 0.7f, 0.35f);
            case UnitGrade.Uncommon: return new Color(0.52f, 0.42f, 0.96f);   // UnitGradeExtensions.Color와 같은 값
            default: return new Color(0.4f, 0.4f, 0.4f);
        }
    }

    public bool CanRoll(GamblingOptionData option)
    {
        if (option == null) return false;
        GamblingProgress stockProgress = OwnerContext?.GamblingProgress;
        if (option.stockMax > 0 && (stockProgress == null || stockProgress.Stock(option) <= 0)) return false;
        if (!StoryRequirementMet(option)) return false;
        if (option.category == GamblingCategory.Unit && (unitSpawner == null || gachaTable == null)) return false;
        // 유닛 도박도 해금 대상이다(고급도박 R15) — 돈 도박만 이 검사를 했다.
        if (option.category == GamblingCategory.Unit && option.requiresUnlock && (stockProgress == null || !stockProgress.IsUnlocked(option))) return false;

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

    public bool TryRoll(GamblingOptionData option, out string failReason)
    {
        if (!CanRoll(option))
        {
            failReason = UnavailableReason(option);
            return false;
        }

        PlayerContext context = OwnerContext;
        if (context == null) { failReason = null; return false; }

        return option.category == GamblingCategory.Money
            ? TryRollMoney(option, context, out failReason)
            : TryRollUnit(option, context, out failReason);
    }

    // CanRoll과 같은 조건을 그대로 따라가며 "어디서 막혔는지"만 문구로 뽑는다 — 판정 로직을
    // 두 번 쓰기 싫지만(CanRoll이 이미 있다), 그쪽은 bool 하나만 돌려주게 놔뒀다(GetSlotView가
    // 0.4초마다 부르는 자리라 문자열까지 매번 만들면 낭비다) — 그래서 실패했을 때만, 클릭
    // 시점에 한 번만 여기서 다시 짚는다.
    string UnavailableReason(GamblingOptionData option)
    {
        if (option == null) return null;
        if (option.category == GamblingCategory.Unit && (unitSpawner == null || gachaTable == null))
            return null;   // 배선 오류 — 플레이어가 봐도 못 고친다.

        PlayerContext context = OwnerContext;
        if (context == null) return null;

        if (option.category == GamblingCategory.Money)
            return MoneyUnavailableReason(option);

        if (option.requiresUnlock && (context.GamblingProgress == null || !context.GamblingProgress.IsUnlocked(option)))
            return string.IsNullOrEmpty(option.unlockHint) ? "아직 해금되지 않음" : option.unlockHint;
        string stock = StockReason(option, context.GamblingProgress);
        if (stock != null) return stock;
        if (context.ResourceWallet == null) return null;
        if (context.ResourceWallet.Get(option.costResourceType) < option.cost)
        {
            string label = ResourceLabel(option.costResourceType);
            char last = label[label.Length - 1];
            bool batchim = last >= '가' && last <= '힣' && (last - '가') % 28 != 0;   // 받침 있으면 「이」(토큰이), 없으면 「가」(목재가)
            return $"{label}{(batchim ? "이" : "가")} 부족합니다!";
        }
        if (option.goldCost > 0 && (context.GoldWallet == null || context.GoldWallet.Gold < option.goldCost))
            return "골드가 부족합니다.";

        return null;
    }

    // 엄태웅 「웅교교주」 누적(+4%p×횟수, 최대 5회)을 100%가 아닌 도박에 더한다(상한 100%). 성공률 0은 「옛 에셋 = 항상 성공」이라 그대로 둔다.
    static float EffectiveSuccessChance(GamblingOptionData option, PlayerContext context)
    {
        float baseChance = option.successChancePercent;
        if (context == null || baseChance <= 0f || baseChance >= 100f) return baseChance;
        return Mathf.Min(100f, baseChance + context.GambleBoostPercent);
    }

    static string BoostNote(GamblingOptionData option, PlayerContext context)
        => context != null && context.GambleBoostCount > 0 && option.successChancePercent > 0f && option.successChancePercent < 100f
            ? $" (웅교교주 +{Mathf.Min(100f, option.successChancePercent + context.GambleBoostPercent) - option.successChancePercent:F0}%p)" : "";

    // 성공/실패 구분이 없다 — 걸고 나면 항상 결과 범위(0 포함) 안에서 얼마를 받는다.
    bool TryRollMoney(GamblingOptionData option, PlayerContext context, out string failReason)
    {
        failReason = null;
        if (context.GoldWallet == null || !context.GoldWallet.TrySpend(option.cost))
        {
            failReason = "골드가 부족합니다.";
            return false;
        }

        // 성공률이 0이면 옛 에셋(성공/실패 구분 없이 항상 지급)으로 보고 성공 취급한다.
        bool success = option.successChancePercent <= 0f
                       || Random.Range(0f, 100f) < EffectiveSuccessChance(option, context);

        context.GamblingProgress?.ConsumeStock(option);

        // 목재 구입(원작 h0AK 10,000골드 → 목재 1) — 골드 대신 자원을 준다.
        if (option.payoutResourceAmount > 0)
        {
            context.ResourceWallet?.Add(option.payoutResourceType, option.payoutResourceAmount);
            context.GamblingProgress?.RecordUse(option);
            PlayerNotification.Show(context.PlayerId, $"<color=#32CD32>{ResourceLabel(option.payoutResourceType)} {option.payoutResourceAmount} 획득!</color>");
            if (option.coinSoundOnSuccess) GameSound.PlayFor(context.PlayerId, GameSoundId.Coin);   // 원작 Money_trade
            return true;
        }

        int amount = success
            ? Random.Range(option.successGoldMin, option.successGoldMax + 1)
            : Random.Range(option.failureGoldMin, option.failureGoldMax + 1);
        if (amount > 0) context.GoldWallet.Add(amount);

        context.GamblingProgress?.RecordUse(option);

        // 졸업(원작 Trig_Money_Gemble_3): 당첨금·실패 환급을 **둘 다** 누적하고, **당첨 때만** 누적 ≥ 35,000을 본다.
        if (option.graduateAtCumulative > 0 && context.GamblingProgress != null)
        {
            context.GamblingProgress.AddPayout(option, amount);
            if (success && !context.GamblingProgress.Graduated && context.GamblingProgress.CumulativePayout(option) >= option.graduateAtCumulative)
            {
                context.GamblingProgress.Graduate();
                PlayerNotification.Show(context.PlayerId, "<color=#32CD32>돈도박 골드획득 한계에 도달하여 돈도박-고급을 졸업합니다!</color>", 10f);
                Debug.Log($"[도박] 졸업: {option.optionName} 누적 {context.GamblingProgress.CumulativePayout(option)}엔 — 도박소 칸이 바뀐다(원작 h062 → h08C).");
            }
        }

        // 결과를 말해주지 않으면 눌러도 아무 일도 안 일어난 것처럼 보인다 — 0엔이 나오는
        // 판이 있어서 더 그렇다. 남은 횟수까지 같이 알려준다.
        // ⚠️ 2026-09-05 정정: 이 주석이 스스로 문제를 지적해놓고 Debug.Log로만 고쳐뒀었다 —
        // 콘솔은 플레이어가 안 본다. PlayerNotification으로 화면에 띄운다(구현담당3 정리,
        // "조용한 실패" #13).
        int used = context.GamblingProgress != null ? context.GamblingProgress.UsesSoFar(option) : 0;
        string left = option.maxUses > 0 ? $", 남은 횟수 {option.maxUses - used}"
                    : option.stockMax > 0 && context.GamblingProgress != null ? $", 재고 {context.GamblingProgress.Stock(option)}/{option.stockMax}" : "";
        // 원작 문구: 당첨 「N원 획득 !」 · 실패 「돈도박에 실패하여 N원을 되돌려받습니다.」(Trig_Money_Gemble_3)
        string result = success ? $"<color=#52E252>{amount}원 획득 !</color>" : $"<color=#FF8200>돈도박에 실패하여 {amount}원을 되돌려받습니다.</color>";
        PlayerNotification.Show(context.PlayerId, $"{option.optionName}: {result} 보유 {context.GoldWallet.Gold}엔{left}");
        if (success ? option.coinSoundOnSuccess : option.coinSoundOnFailure)
            GameSound.PlayFor(context.PlayerId, GameSoundId.Coin);   // 원작 Money_Gemble_1_re(둘 다) · Money_Gemble_3(당첨만)

        return true;
    }

    bool TryRollUnit(GamblingOptionData option, PlayerContext context, out string failReason)
    {
        failReason = null;
        if (unitSpawner == null || gachaTable == null) return false;   // 배선 오류, reason 없음

        // 묶음 보너스(bonusPool)는 성공 판정보다 먼저, 판 전체 확률로 굴린다(데이터 주석 참고).
        UnitData poolReward = null;
        if (option.bonusPool != null && option.bonusPool.Count > 0 && option.bonusPoolChancePercent > 0f
            && Random.Range(0f, 100f) < option.bonusPoolChancePercent)
            poolReward = option.bonusPool[Random.Range(0, option.bonusPool.Count)];

        bool success = poolReward != null || Random.Range(0f, 100f) < EffectiveSuccessChance(option, context);

        // 당첨 시 지급할 등급을 자원 차감 전에 미리 정하고, 그 등급 pool이 비어있으면
        // 통째로 취소한다 — unitSpawner가 없거나 지급할 유닛이 없는데 자원만 나가면
        // 완전한 손실이 된다(UnitInventory가 인스턴스 등록부로 바뀌어 등록 경로가
        // UnitSpawner.Spawn 하나뿐이라 더 그렇다).
        UnitGrade resultGrade = default;
        // 원작 "유닛도박 초급/중급"은 성공(85%/70%) 안에서 다시 낮은 확률(2%/3.5%)로
        // 해적선 같은 특정 유닛을 먼저 노린다 — 실패하면 그제서야 등급 풀로 넘어간다
        // (0.85×0.02=1.70%, 0.70×0.035=2.45%). bonusUnit이 비어 있으면(기존 도박 옵션)
        // 이 축을 안 타 예전과 똑같이 동작한다.
        bool bonusHit = poolReward != null
                        || (success && option.bonusUnit != null && option.bonusChancePercent > 0f
                            && Random.Range(0f, 100f) < option.bonusChancePercent);
        if (success && !bonusHit)
        {
            resultGrade = PickResultGrade(option);
            if (!HasPool(resultGrade))
            {
                // 콘텐츠 결손(사장님 배정 전)이라 콘솔에도 남기고, 화면에도 알린다 — 이건
                // 감사 때 놓쳤던 자리다(원래 로그만 있었다).
                Debug.LogWarning($"{name}: {option.optionName}의 {resultGrade} 풀이 비어있어 도박을 진행하지 않았습니다.");
                failReason = $"{resultGrade.KoreanName()} 등급 재고가 없습니다.";
                return false;
            }
        }

        // 골드를 먼저 뺀다. 자원을 먼저 빼고 골드가 모자라면 자원만 날아간다 —
        // CanRoll이 둘 다 봤어도 그 사이에 다른 경로로 골드가 줄 수 있다.
        // ⚠️ 2026-09-05: 이 레이스는 실제로는 드물지만, 걸리면 예전엔 완전 침묵이었다 —
        // 되돌림까지 해놓고 플레이어에게는 아무 표시가 없었다("조용한 실패" #3).
        if (option.goldCost > 0)
        {
            if (context.GoldWallet == null || !context.GoldWallet.TrySpend(option.goldCost))
            {
                failReason = "골드가 부족합니다.";
                return false;
            }
        }

        if (context.ResourceWallet == null || !context.ResourceWallet.TrySpend(option.costResourceType, option.cost))
        {
            // 자원 차감이 실패하면 이미 빠진 골드를 되돌린다.
            if (option.goldCost > 0 && context.GoldWallet != null)
                context.GoldWallet.Add(option.goldCost);
            failReason = "자원이 부족합니다.";
            return false;
        }

        context.GamblingProgress?.ConsumeStock(option);

        if (success)
        {
            UnitData reward = poolReward != null ? poolReward : bonusHit ? option.bonusUnit : gachaTable.RollFromGrade(resultGrade);
            GameObject spawned = unitSpawner.Spawn(reward, ResolveSpawnPosition(reward), owner.OwnerId);

            // 희귀함 리롤(A0VX) — "지정된 특별 결과(bonusHit)가 아닌 일반 랜덤풀 결과"에만
            // 붙는다(UNIQUE_REROLE_AND_SELL_FAMILY.md ⑦). 이 옵션이 그 소스로 확인된 경우만
            // (grantsUniqueRerollOnGenericSuccess) 스폰된 인스턴스에 런타임으로 붙인다 —
            // UnitData 자산에 미리 박아둘 수 없다(대상이 "가챠에서 나온 그 어떤 유닛"이라
            // 특정 자산 하나로 고정이 안 됨, UniqueRerollAbility 클래스 주석 참고).
            if (!bonusHit && option.grantsUniqueRerollOnGenericSuccess)
                UniqueRerollAbility.Attach(spawned, uniqueRerollAbilityData, unitSpawner);
            AnnounceUnitGamble(option, owner.OwnerId, reward);
            GameSound.PlayFor(owner.OwnerId, GameSoundId.Gacha);   // 뽑기음(10-06) — 당첨만. 실패는 「실패 !」 알림이 말한다
        }
        else
        {
            if (option.grantFailureReward)
            {
                context.ResourceWallet.Add(ResourceType.LuckyToken, FailureLuckyTokens(option, context));
                context.ResourceWallet.Add(ResourceType.Wood, option.failureWood);
                // 원작 j:13103/13131(본인 10초): 도박광 항법 보너스가 붙었을 때만.
                if (option.scalesWithGamblerNavigation && context.NavigationState != null && context.NavigationState.Choice == NavigationChoice.Gambler)
                    PlayerNotification.Show(owner.OwnerId, "<color=#FFD700>항법효과:</color> + <color=#20B2AA>나무 1개</color> <color=#FF8200>+행운의 토큰 1개</color>를 돌려받습니다!", 10f);
            }
            AnnounceUnitGamble(option, owner.OwnerId, null);
        }

        return true;
    }

    // ⚠️ 2026-09-06 신설("항법" 5택1 연결) — 원작 다른세계 도박 실패 시 럭키토큰 공식
    // "1+Dobak_Tech_int"(항법 "도박광" 선택 시 1, 아니면 0)을 그대로 옮긴다.
    // 원작 유닛도박 결과 문구(알림 묶음 6/13, GAP 71). 하급(라벨 없음)은 본인만 — 당첨은 UnitAcquireNotice가 이미 「이름 - 등급 획득!」을 띄워 실패만.
    static void AnnounceUnitGamble(GamblingOptionData option, int ownerId, UnitData reward)
    {
        string label = reward != null ? option.announceSuccessLabel : option.announceFailLabel;
        if (string.IsNullOrEmpty(label))
        {
            if (reward == null) PlayerNotification.Show(ownerId, "<color=#FF0000>실패 !</color>", 10f);   // j:13064
            return;
        }
        string who = $"<color=#FF8200>{PlayerDisplayName.Of(ownerId)}</color>";
        string line = reward != null
            ? $"{who} <color=#FF8200>님이 {label} 도박으로</color> <color=#FF0000>{reward.unitName} 획득 !</color>"
            : $"{who} <color=#FF8200>님이 {label} 도박을</color> <color=#FF0000>실패 하셨습니다!</color>";
        foreach (PlayerContext context in PlayerContext.Occupied)
            PlayerNotification.Show(context.PlayerId, line, 10f);
    }

    // scalesWithGamblerNavigation이 꺼진 옵션(고급도박 등)은 기존 그대로
    // failureLuckyTokens만 돌려준다 — 회귀 없음.
    static int FailureLuckyTokens(GamblingOptionData option, PlayerContext context)
    {
        int bonus = (option.scalesWithGamblerNavigation
                     && context?.NavigationState != null
                     && context.NavigationState.Choice == NavigationChoice.Gambler) ? 1 : 0;
        return option.failureLuckyTokens + bonus;
    }

    UnitGrade PickResultGrade(GamblingOptionData option)
    {
        if (!option.useSecondaryGrade) return option.primaryResultGrade;
        // 정확한 확률이 적혀 있으면 그대로(원작 H0B0: 보너스 미적중 뒤 1/38로 특수함).
        if (option.secondaryChancePercent > 0f)
            return Random.Range(0f, 100f) < option.secondaryChancePercent ? option.secondaryResultGrade : option.primaryResultGrade;

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
