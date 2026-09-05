using System.Collections.Generic;
using UnityEngine;

// 해적단 퀘스트 상점(원작 h07A "도전과제-퇴치") — 사장님 발견으로 2026-09-05 재작업.
// 원작은 판매 포탈이 아니라 이 상점에서 goldCost를 내고 사는 순간 발동한다
// (`GetSoldUnit()` 이벤트, PM 재조사). UnitUpgradeShop·GamblingShop과 같은 관례를 그대로
// 따른다 — 레인 안에 서 있고, 파괴 불가, 적 타겟에서 자동 제외.
//
// 재고는 이 상점 인스턴스(=레인=플레이어) 각자가 독립적으로 갖는다 — 원작도 상점 건물이
// 4레인 각각 따로 서 있어 플레이어마다 별도 재고다.
[RequireComponent(typeof(Selectable), typeof(OwnedByPlayer))]
public class PirateQuestShop : MonoBehaviour, ILaneShop
{
    [SerializeField] List<PirateQuestData> quests = new List<PirateQuestData>();

    static readonly Color QuestColor = new Color(0.75f, 0.55f, 0.2f); // 해적단 테마 — 청동/가죽색

    struct SlotState
    {
        public int stock;
        public float restockTimer;

        // ILaneShop 문서대로 GetSlotView는 0.4초 주기로 불린다 — stock이 그대로면 라벨을
        // 새로 안 만든다.
        public bool cacheBuilt;
        public int cachedStock;
        public string cachedLabel;
    }

    SlotState[] slotState;

    OwnedByPlayer owner;

    PlayerContext OwnerContext => PlayerContext.Get(owner.OwnerId);

    void Awake()
    {
        owner = GetComponent<OwnedByPlayer>();
        slotState = new SlotState[quests.Count];

        for (int i = 0; i < quests.Count; i++)
        {
            PirateQuestData quest = quests[i];
            if (quest == null) continue;

            slotState[i].stock = Mathf.Clamp(quest.stockStart, 0, Mathf.Max(1, quest.stockMax));
            slotState[i].restockTimer = quest.restockSeconds;
        }
    }

    // 재고 보충은 서버 권위 상태다 — 클라이언트마다 다른 시점에 보충되면 재고 수가 어긋난다.
    void Update()
    {
        if (!GameAuthority.IsServer) return;

        for (int i = 0; i < quests.Count; i++)
        {
            PirateQuestData quest = quests[i];
            if (quest == null) continue;

            int max = Mathf.Max(1, quest.stockMax);
            if (slotState[i].stock >= max) continue;

            slotState[i].restockTimer -= Time.deltaTime;
            if (slotState[i].restockTimer > 0f) continue;

            slotState[i].stock++;
            slotState[i].restockTimer = quest.restockSeconds;
        }
    }

    // ---- ILaneShop ----

    public int SlotCount => quests.Count;

    public LaneShopSlotView GetSlotView(int index)
    {
        if (index < 0 || index >= quests.Count) return LaneShopSlotView.Empty;

        PirateQuestData quest = quests[index];
        if (quest == null) return LaneShopSlotView.Empty;

        ref SlotState state = ref slotState[index];
        if (!state.cacheBuilt || state.cachedStock != state.stock)
        {
            state.cacheBuilt = true;
            state.cachedStock = state.stock;
            state.cachedLabel = state.stock > 0
                ? $"{quest.questName}\n{quest.goldCost}엔"
                : $"{quest.questName}\n품절";
        }

        return new LaneShopSlotView(state.cachedLabel, QuestColor, CanBuy(index), LaneShopTargetKind.None);
    }

    // 호버할 때만 불린다 — 문자열 조립은 여기서만 한다.
    public string GetSlotTooltip(int index)
    {
        if (index < 0 || index >= quests.Count) return null;

        PirateQuestData quest = quests[index];
        if (quest == null) return null;

        string rangeText = quest.minRound <= 0 && quest.maxRound <= 0
            ? "라운드 제한 없음"
            : $"{(quest.minRound <= 0 ? 1 : quest.minRound)}~{(quest.maxRound <= 0 ? "끝" : quest.maxRound.ToString())}라운드";

        if (slotState[index].stock > 0)
        {
            return $"{quest.questName}\n{quest.goldCost}엔 — {rangeText}\n"
                 + $"제한시간 {quest.timerSeconds:F0}초 안에 미니보스를 처치하세요";
        }

        float remain = Mathf.Max(0f, slotState[index].restockTimer);
        return $"{quest.questName}\n품절 — {remain:F0}초 뒤 재입고";
    }

    // 원작 순서 그대로: 확인을 다 마친 뒤에만 차감한다(CanBuy 통과 = 재고·라운드·골드·중복진행
    // 전부 확인됨) — 실패 경로가 골드나 재고를 먹으면 안 된다.
    public bool TryUse(int index, LaneShopTarget target)
    {
        if (index < 0 || index >= quests.Count) return false;
        if (!CanBuy(index)) return false;

        PirateQuestData quest = quests[index];
        PlayerContext context = OwnerContext;

        if (!context.GoldWallet.TrySpend(quest.goldCost)) return false; // CanBuy 이후 상태가 바뀌었을 수 있어 다시 확인

        slotState[index].stock--;
        PirateQuestManager.Instance.StartQuest(quest, owner.OwnerId);
        return true;
    }

    bool CanBuy(int index)
    {
        PirateQuestData quest = quests[index];
        if (quest == null || slotState[index].stock <= 0) return false;
        if (quest.miniboss == null || quest.miniboss.prefab == null) return false;

        PirateQuestManager manager = PirateQuestManager.Instance;
        if (manager == null || manager.IsActive(quest, owner.OwnerId)) return false;

        int round = manager.CurrentRound;
        bool inRange = (quest.minRound <= 0 || round >= quest.minRound)
                     && (quest.maxRound <= 0 || round <= quest.maxRound);
        if (!inRange) return false;

        PlayerContext context = OwnerContext;
        return context != null && context.GoldWallet != null && context.GoldWallet.Gold >= quest.goldCost;
    }
}
