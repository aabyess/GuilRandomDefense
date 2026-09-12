using System.Collections.Generic;
using UnityEngine;

// 보물찾기 — 원작 세 트리거를 한 자리에 모은 것(TREASURE_SPEC_2026-09-12.md, PM이 war3map_new.j로 재검증).
//
//   · 상자 숨기기  `Trig_Round_10ver_Actions` — Level<61 AND Level%10==0이면 보스 생성 직전에
//                  `udg_treasure_box_int`(7)번 `GetRandomLocInRect(gg_rct_treasure_chest_zone)`에
//                  `n00H`를 만든다. 보스 처치와 무관, 난이도 무관. 못 찾은 상자는 안 치운다 — 쌓인다.
//   · 탐색        `Trig_treasure_Actions` — 항해일지(H0C4)의 `AHta`를 찍은 지점 반경
//                  `udg_treasure_range_int[pid]` 안의 상자를 **전부** 찾는다(1개 제한 없음).
//   · 전설 나미    `Trig_UnitJohabCounter_Func020` — A04X(h02P 도둑고양이 나미)가 맵에 들어오면
//                  그 플레이어 한 번만(`Nami_legend_Boolean[pid]`): 반경 863 · 보상 개수 +1 ·
//                  Gold_Plus +0.60. 상자 개수 9만 전역이다.
//
// 🔴 전설 나미 보너스는 **조합한 플레이어 한 명**에게만 붙는다. A04X 툴팁은 "플레이어 전체 한번만
//    적용"이라고 쓰지만 코드는 전부 `GetOwningPlayer(GetTriggerUnit())` 인덱스다 — 전역인 건
//    `udg_treasure_box_int=9` 하나뿐. 툴팁이 아니라 코드를 따랐다.
//
// 상자는 원작에서 Locust + 빈 모델이라 **안 보인다** — 여기서도 게임 오브젝트를 만들지 않고 좌표만
// 든다. 원작 판정도 거리 하나뿐(시야·지형 조건 없음)이라 좌표 목록으로 충분하다.
//
// 원작에 있는데 안 옮긴 것:
//   · 영원 나미(`Trig_Eternal_Nami`, 반경 940 대입) — 우리에 영원 나미 영입 경로가 없다.
//   · 도움소 A0MX 레벨·A035 교체 — 원작 「협동 건물」 UI 표시일 뿐 수치 효과가 없다.
//   · 시노부(전설적인 h042) A0YP 미니맵 핑 — 우리에 미니맵이 없다.
public class TreasureHunt : MonoBehaviour
{
    public static TreasureHunt Instance { get; private set; }

    // 원작 Level<61 AND Level%10==0 — 10·20·30·40·50·60라운드.
    const int ChestRoundInterval = 10;
    const int LastChestRound = 60;

    [Header("상자 숨기기")]
    // 월드 XZ 사각형(x=월드 X, y=월드 Z). 원작 rect가 창고·조합식을 피해 그려진 것을 맵 생성기가 옮긴다.
    [SerializeField] List<Rect> chestZones = new List<Rect>();
    [SerializeField] float chestHeight = 1f;
    [SerializeField] int chestCountPerRound = 7;          // 원작 udg_treasure_box_int 초기값
    [SerializeField] int legendNamiChestCount = 9;        // 전설 나미 뒤 전역 대입값

    [Header("탐색")]
    [SerializeField] float searchRange = 30f;             // 원작 750 — 맵 생성기가 우리 축척으로 덮어쓴다
    [SerializeField] float legendNamiSearchRange = 34.5f; // 원작 863
    [SerializeField] float searchCooldown = 100f;         // AHta acdn 레벨1
    [SerializeField] float boostedSearchCooldown = 70f;   // AHta acdn 레벨2 — 툴팁의 "80초"는 원작 오기
    // 원작은 I00K 탐사도구를 **항해일지가 주우면** 그 건물의 AHta가 레벨2가 된다
    // (`Trig_item_up_Actions`, I00K 툴팁 "항해일지-탐색 쿨타임 20%감소"). 우리 아이템은 플레이어
    // 인벤토리에 들어가고 항해일지는 플레이어당 하나라, "그 플레이어가 I00K를 가졌다"와 같다.
    [SerializeField] ItemData boostedCooldownItem;

    [Header("보상")]
    [SerializeField] WispData randomWisp;                 // e0IX 랜덤위습
    [SerializeField] WispData commonChoiceWisp;           // e018 흔함선택위습
    [SerializeField] WispData uncommonWisp;               // e017 안흔함 위습

    [Header("전설 나미(h02P)")]
    [SerializeField] UnitData legendNami;
    [SerializeField] float legendNamiGoldPlus = 0.6f;

    [Header("도전과제 트레저헌터")]
    [SerializeField] int questGoal = 9;                   // udg_treasurequest==9 — 팀 전체 누적
    [SerializeField] int questWood = 2;
    [SerializeField] int questSavePoints = 2;

    [Header("발견 연출(비워도 된다)")]
    // 원작은 발견 자리에 e0DA·e0D9(연출 유닛)를 만든다. 모델이 들어오기 전엔 비워 둔다.
    [SerializeField] GameObject foundEffectPrefab;
    [SerializeField] float foundEffectLifetime = 4f;

    readonly List<Vector2> chests = new List<Vector2>();
    readonly Dictionary<int, float> cooldownUntil = new Dictionary<int, float>();
    readonly Dictionary<int, float> legendRangePlayers = new Dictionary<int, float>();
    readonly Dictionary<int, int> rewardCount = new Dictionary<int, int>();
    readonly HashSet<int> legendNamiPlayers = new HashSet<int>();
    int currentChestCount;

    // I002 「찾은 보물 개수」 — 원작은 발견마다 4명 전원의 I002 충전량을 +1한다. 팀 공용 값 하나와 같다.
    public int TeamFoundCount { get; private set; }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        currentChestCount = chestCountPerRound;

        if (chestZones.Count == 0)
            Debug.LogWarning($"{name}: 보물상자를 숨길 구역이 비어 있어 상자가 안 생깁니다. 맵 생성을 한 번 돌려주세요.", this);
        if (randomWisp == null || commonChoiceWisp == null || uncommonWisp == null)
            Debug.LogWarning($"{name}: 보물 보상 위습 3종 중 빈 칸이 있습니다 — 그 보상이 나오면 위습 없이 끝납니다.", this);
    }

    // ---- 상자 숨기기 ----

    // RoundManager.StartRound가 매 라운드 부른다. 조건 판정은 여기서만 한다.
    public void OnRoundStarted(int roundNumber)
    {
        if (!GameAuthority.IsServer) return;
        if (roundNumber <= 0 || roundNumber > LastChestRound || roundNumber % ChestRoundInterval != 0) return;
        if (chestZones.Count == 0) return;

        for (int i = 0; i < currentChestCount; i++)
            chests.Add(RandomPointInZones());
    }

    // 원작 rect 하나 안에서 균등 무작위. 우리 구역은 여러 사각형이라 넓이 비례로 골라 균등을 지킨다.
    Vector2 RandomPointInZones()
    {
        float total = 0f;
        foreach (Rect zone in chestZones) total += zone.width * zone.height;

        float pick = Random.value * total;
        foreach (Rect zone in chestZones)
        {
            float area = zone.width * zone.height;
            if (pick <= area) return RandomPointIn(zone);
            pick -= area;
        }

        return RandomPointIn(chestZones[chestZones.Count - 1]);
    }

    static Vector2 RandomPointIn(Rect zone) =>
        new Vector2(Random.Range(zone.xMin, zone.xMax), Random.Range(zone.yMin, zone.yMax));

    // ---- 전설 나미 ----

    // UnitSpawner.Spawn(플레이어 유닛이 생기는 유일한 자리)이 부른다 — 원작 트리거도 획득 경로를 안 가린다.
    public void OnUnitSpawned(UnitData data, int ownerId)
    {
        if (data == null || legendNami == null || data != legendNami) return;
        if (!legendNamiPlayers.Add(ownerId)) return;

        currentChestCount = legendNamiChestCount;
        legendRangePlayers[ownerId] = legendNamiSearchRange;
        rewardCount[ownerId] = RewardCountFor(ownerId) + 1;
        PlayerContext.Get(ownerId)?.GoldWallet?.AddGoldPlus(legendNamiGoldPlus);
    }

    // ---- 탐색 ----

    public float RangeFor(int playerId) =>
        legendRangePlayers.TryGetValue(playerId, out float range) ? range : searchRange;

    // 원작 udg_TreasureChest[pid] — 기본 1, 전설 나미 뒤 2.
    public int RewardCountFor(int playerId) =>
        rewardCount.TryGetValue(playerId, out int count) ? count : 1;

    public float CooldownFor(PlayerContext context)
    {
        if (boostedCooldownItem != null && context != null && context.ItemInventory != null)
        {
            foreach (ItemData item in context.ItemInventory.Items)
                if (item == boostedCooldownItem) return boostedSearchCooldown;
        }
        return searchCooldown;
    }

    public float CooldownRemaining(int playerId) =>
        cooldownUntil.TryGetValue(playerId, out float until) ? Mathf.Max(0f, until - Time.time) : 0f;

    // 원작처럼 상자가 없어도 시전은 된다(쿨타임이 돈다) — 아직 안 숨겨졌는지는 플레이어가 알 수 없는 게 원작이다.
    public bool TrySearch(int playerId, Vector3 point, out string failReason)
    {
        failReason = null;

        PlayerContext context = PlayerContext.Get(playerId);
        if (context == null) return false;

        float remaining = CooldownRemaining(playerId);
        if (remaining > 0f)
        {
            failReason = $"탐색 재사용 대기 중입니다. ({Mathf.CeilToInt(remaining)}초)";
            return false;
        }

        cooldownUntil[playerId] = Time.time + CooldownFor(context);

        float range = RangeFor(playerId);
        Vector2 center = new Vector2(point.x, point.z);
        int found = 0;
        for (int i = chests.Count - 1; i >= 0; i--)
        {
            Vector2 chest = chests[i];
            if ((chest - center).sqrMagnitude > range * range) continue;

            chests.RemoveAt(i);
            OpenChest(context, chest);
            found++;
        }

        // 원작은 AHta가 3초간 그 자리를 밝혀 보여 준다(adur 3) — 우리엔 그 연출이 없어 빈손이면 문구로 알린다.
        if (found == 0) PlayerNotification.Show(playerId, "이 근처에는 보물상자가 없습니다.");
        return true;
    }

    // Trig_treasure_Func007A 순서 그대로: 누적 +1 → 보상 → 9번째면 도전과제.
    void OpenChest(PlayerContext context, Vector2 chest)
    {
        TeamFoundCount++;

        int count = RewardCountFor(context.PlayerId);
        int roll = Random.Range(1, 4);   // GetRandomInt(1,3)
        WispData wisp = roll == 1 ? randomWisp : roll == 2 ? commonChoiceWisp : uncommonWisp;
        string label = roll == 1 ? "랜덤위습" : roll == 2 ? "흔함선택위습" : "안흔함 위습";

        if (wisp != null && RewardDistributor.Instance != null)
            RewardDistributor.Instance.GrantWisps(context, new List<WispReward> { new WispReward { wisp = wisp, count = count } });

        PlayerNotification.Show(context.PlayerId, $"보물상자를 발견하였습니다! {label} {count}기 획득!");
        SpawnFoundEffect(chest);

        if (TeamFoundCount == questGoal) CompleteTreasureHunterQuest();
    }

    void CompleteTreasureHunterQuest()
    {
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            context.ResourceWallet?.Add(ResourceType.Wood, questWood);
            context.PersistentSave?.AddSessionPoints(questSavePoints);
            PlayerNotification.Show(context.PlayerId,
                $"도전과제-트레저헌터를 완수하여 모든 플레이어가 목재 {questWood}개를 획득합니다!\n" +
                $"◎세이브_플레이포인트 {questSavePoints} 획득!", 6f);
        }
    }

    void SpawnFoundEffect(Vector2 chest)
    {
        if (foundEffectPrefab == null) return;

        GameObject effect = Instantiate(foundEffectPrefab, new Vector3(chest.x, chestHeight, chest.y),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        Destroy(effect, foundEffectLifetime);
    }
}
