using UnityEngine;

// 항해일지(원작 H0C4) — 아이템 도박용 「메타몽」(H0BS)을 파는 레인 상점.
//
// 왜 필요했나 — 2026-09-09 아이템 40종 전수 감사(ITEM_SYSTEM_AUDIT_2026-09-09.md)에서
// 아이템 시스템이 통째로 안 도는 이유가 여기였다. 메타몽 유닛 자산도 있고
// `sellTriggersItemGamblePool` 배선도 정확한데, **그 유닛을 주는 경로가 프로젝트 전체에
// 0건**이었다(가챠·조합·창고·보상 어디에도 없음). 문은 멀쩡한데 열쇠가 없었다.
//
// 원작 구조(리서치담당, JASS 원문 확인):
//   · `CreateUnitsForPlayer0`~`Player3`이 판 시작 시 `CreateUnit(Player(N),'H0C4',...)`으로
//     4명 각자 기지에 하나씩 배치한다 — 사는 게 아니라 처음부터 있는 건물이다.
//   · `Trig_BaseORD_Actions`가 그 4개를 `udg_Tech_Tree[pid]`(unit array)에 연결한다.
//   · 파는 물건은 H0BS 하나뿐이다(`udg_Tech_Tree` 대상 재고 추가 호출 전수 확인).
//     ⚠️ H0AQ·H0AO는 `udg_Q_Buliding_mob`라는 **다른 건물**의 재고다 — 혼동 주의.
//   · 값은 `w3u` 원문 그대로 `ugol=5000`(골드) · `ulum=3`(목재).
//
// 재고는 여기 두지 않는다 — 이미 `ItemGambleState`(플레이어별 컴포넌트)에 있고
// `RoundManager.GrantItemGambleStock`이 6·9라운드에 채운다. 원작도 같은 자리다:
//   시작 `AddUnitToStockBJ('H0BS', Tech_Tree[pid], 0, 0)`  → 재고 0·상한 0(구매 불가)
//   `Trig_Story_reward6_Actions` → Item_Int+1 뒤 상한 2로 재설정
//   `Trig_Story_reward9_Actions` → 다시 +1
// 시간마다 차오르는 리젠은 원작에 아예 없다(H0C4에 `urst` 없음) — 그래서 여기도
// PirateQuestShop과 달리 Update에서 재고를 안 채운다.
//
// 🔴 재고를 **살 때가 아니라 팔 때** 깎는다는 점이 원작과 다르다. 워크래프트 상점은
//    사는 순간 재고가 줄지만, 우리는 `ItemGambleState.TryGamble`이 판매 시점에 1을
//    소모하도록 이미 짜여 있다(그쪽이 도박 성패와 무관하게 먼저 차감하는 원작 순서를
//    지킨다). 사고 파는 게 1:1이라 **총 도박 횟수는 원작과 같다.**
//    다만 그대로 두면 재고 1로 메타몽을 여러 마리 사서 돈만 버릴 수 있어,
//    「이미 들고 있는 메타몽 수」를 재고에서 빼고 판다(AvailableStock).
//
// 두 번째 칸은 「탐색」(원작 AHta) — 보물찾기. 원작에서 AHta를 가진 건 항해일지 H0C4와 그 분기
// 건물들(H0C0·H0C1·H0C2·H0BZ·H0BQ)이고, I00K 툴팁도 "항해일지-탐색 쿨타임"이라 **모든 플레이어가
// 자기 항해일지로** 찾는다. 규칙·상태는 전부 TreasureHunt에 있고 여기선 버튼만 단다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class VoyageLogShop : MonoBehaviour, ILaneShop
{
    const int GambleSlot = 0;
    const int SearchSlot = 1;

    [SerializeField] UnitData gambleUnit;
    [SerializeField] int goldCost = 5000;
    [SerializeField] int woodCost = 3;
    [SerializeField] UnitSpawner unitSpawner;

    static readonly Color LogColor = new Color(0.35f, 0.5f, 0.72f);   // 항해 테마 — 해도(海圖)의 남색

    OwnedByPlayer owner;

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    // GetSlotView가 0.4초마다 불린다(ILaneShop 참고) — 재고가 그대로면 라벨을 새로 안 만든다.
    bool cacheBuilt;
    int cachedStock;
    string cachedLabel;

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();

        if (gambleUnit == null)
        {
            Debug.LogWarning($"{name}: 파는 유닛(메타몽 h0BS)이 안 꽂혀 있어 아이템 도박을 열 수 없습니다. " +
                             "맵 생성(Tools/맵/원랜디 맵 생성)을 한 번 돌려주세요.", this);
        }
        else if (gambleUnit.sellTriggersItemGamblePool == null)
        {
            // 이 유닛을 파는 것이 이 상점의 존재 이유다 — 그 배선이 비어 있으면
            // 사더라도 팔 때 아무 일도 안 난다. 조용히 두면 또 "왜 안 도나"가 된다.
            Debug.LogWarning($"{name}: {gambleUnit.unitName}에 sellTriggersItemGamblePool이 비어 있어 " +
                             "판매해도 아이템 도박이 돌지 않습니다.", this);
        }
    }

    /// <summary>
    /// 지금 살 수 있는 수 — 남은 도박 횟수에서 이미 들고 있는 메타몽을 뺀다.
    /// 재고 소모가 판매 시점이라(위 주석) 이걸 안 빼면 재고 1로 여러 마리를 사서
    /// 돈만 버리게 된다.
    /// </summary>
    int AvailableStock(PlayerContext context)
    {
        if (context == null || context.ItemGambleState == null) return 0;

        int held = 0;
        UnitInventory inventory = context.UnitInventory;
        if (inventory != null && gambleUnit != null)
        {
            foreach (UnitData unit in inventory.Units)
                if (unit == gambleUnit) held++;
        }

        return Mathf.Max(0, context.ItemGambleState.Stock - held);
    }

    // ---- ILaneShop ----

    public int SlotCount => 2;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (index == SearchSlot) return GetSearchSlotView();
        if (index != GambleSlot || gambleUnit == null) return LaneShopSlotView.Empty;

        int stock = AvailableStock(OwnerContext);
        if (!cacheBuilt || cachedStock != stock)
        {
            cacheBuilt = true;
            cachedStock = stock;
            cachedLabel = stock > 0
                ? $"아이템 도박\n{goldCost}엔 + 목재{woodCost}"
                : "아이템 도박\n횟수 없음";
        }

        return new LaneShopSlotView(cachedLabel, LogColor, CanBuy(), LaneShopTargetKind.None);
    }

    public string GetSlotTooltip(int index)
    {
        if (index == SearchSlot) return GetSearchTooltip();
        if (index != GambleSlot || gambleUnit == null) return null;

        PlayerContext context = OwnerContext;
        int stock = AvailableStock(context);

        if (stock <= 0)
        {
            // "왜 없는지"를 알려준다 — 6라운드 전까지는 아예 안 열린다는 게 원작 규칙이라,
            // 그걸 모르면 고장으로 보인다.
            return $"{gambleUnit.unitName}\n남은 도박 횟수가 없습니다.\n"
                 + "도박 횟수는 6라운드·9라운드 스토리를 깨면 1회씩 늘어납니다.";
        }

        return $"{gambleUnit.unitName}\n{goldCost}엔 + 목재{woodCost} — 남은 횟수 {stock}\n"
             + "사서 **판매**하면 아이템을 하나 뽑습니다.";
    }

    // 확인을 다 마친 뒤에만 차감한다 — 실패 경로가 골드나 목재를 먹으면 안 된다.
    // 골드와 목재 둘 다 내는데, 한쪽만 깎이고 다른 쪽이 모자라 실패하면 그대로 손해다.
    // 그래서 **깎기 전에 둘 다 있는지 본다**(PirateQuestShop은 골드 하나뿐이라 이 문제가 없었다).
    public bool TryUse(int index, LaneShopTarget target, out string failReason)
    {
        failReason = null;

        if (index == SearchSlot)
        {
            TreasureHunt hunt = TreasureHunt.Instance;
            return hunt != null && hunt.TrySearch(owner.OwnerId, target.point, out failReason);
        }

        if (index != GambleSlot) return false;
        if (gambleUnit == null || gambleUnit.prefab == null) return false;   // 배선 오류, reason 없음
        if (unitSpawner == null) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null || context.ResourceWallet == null) return false;

        if (AvailableStock(context) <= 0)
        {
            failReason = "남은 도박 횟수가 없습니다. (6·9라운드 스토리 클리어 시 1회씩)";
            return false;
        }

        if (context.GoldWallet.Gold < goldCost)
        {
            failReason = "골드가 부족합니다!";
            return false;
        }

        if (context.ResourceWallet.Get(ResourceType.Wood) < woodCost)
        {
            failReason = "목재가 부족합니다!";
            return false;
        }

        if (!context.GoldWallet.TrySpend(goldCost)) return false;

        if (!context.ResourceWallet.TrySpend(ResourceType.Wood, woodCost))
        {
            // 위에서 확인했는데도 실패했다면 그 사이에 다른 소비가 끼어든 것이다.
            // 이미 깎은 골드를 돌려준다 — 안 그러면 아무것도 못 받고 5000엔이 사라진다.
            context.GoldWallet.Add(goldCost);
            failReason = "목재가 부족합니다!";
            return false;
        }

        unitSpawner.Spawn(gambleUnit, ResolveSpawnPosition(), owner.OwnerId);
        return true;
    }

    bool CanBuy()
    {
        if (gambleUnit == null || gambleUnit.prefab == null || unitSpawner == null) return false;

        PlayerContext context = OwnerContext;
        if (context == null || context.GoldWallet == null || context.ResourceWallet == null) return false;
        if (AvailableStock(context) <= 0) return false;

        return context.GoldWallet.Gold >= goldCost
            && context.ResourceWallet.Get(ResourceType.Wood) >= woodCost;
    }

    // ---- 탐색(보물찾기) ----

    // 쿨타임 남은 초가 바뀔 때만 라벨을 새로 만든다(GetSlotView는 0.4초마다 불린다).
    int cachedSearchSeconds = -1;
    string cachedSearchLabel;

    LaneShopSlotView GetSearchSlotView()
    {
        TreasureHunt hunt = TreasureHunt.Instance;
        if (hunt == null) return LaneShopSlotView.Empty;

        int seconds = Mathf.CeilToInt(hunt.CooldownRemaining(owner.OwnerId));
        if (seconds != cachedSearchSeconds)
        {
            cachedSearchSeconds = seconds;
            cachedSearchLabel = seconds > 0 ? $"탐색\n{seconds}초" : "탐색\n보물찾기";
        }

        return new LaneShopSlotView(cachedSearchLabel, LogColor, seconds <= 0, LaneShopTargetKind.Ground);
    }

    string GetSearchTooltip()
    {
        TreasureHunt hunt = TreasureHunt.Instance;
        if (hunt == null) return null;

        int playerId = owner.OwnerId;
        return "탐색 — 찍은 지점 주변의 숨겨진 보물상자를 모두 찾습니다.\n"
             + "상자는 10·20·30·40·50·60라운드가 시작될 때 맵 곳곳(창고 제외)에 숨겨집니다.\n"
             + $"찾으면 랜덤위습·흔함선택위습·안흔함 위습 중 하나를 {hunt.RewardCountFor(playerId)}기 받습니다.\n"
             + $"쿨타임 {hunt.CooldownFor(OwnerContext):0}초 (탐사도구 보유 시 70초)\n"
             + $"팀이 찾은 보물 {hunt.TeamFoundCount}개 — 9번째를 찾으면 전원 목재 +2";
    }

    // GamblingShop.ResolveSpawnPosition과 같은 관례 — 산 유닛은 그 플레이어 레인에 선다.
    Vector3 ResolveSpawnPosition()
    {
        LaneMarker lane = LaneMarker.Get(owner.OwnerId);
        if (lane != null) return lane.TakeSpawnPosition(gambleUnit);

        Debug.LogWarning($"{name}: 플레이어 {owner.OwnerId}의 레인을 찾지 못해 상점 자리에 소환합니다.", this);
        return transform.position;
    }
}
