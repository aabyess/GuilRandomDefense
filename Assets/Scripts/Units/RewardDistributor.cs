using System.Collections.Generic;
using UnityEngine;

// TODO(멀티플레이): 지급 로직은 반드시 서버 권위로 옮겨야 한다.
// 지금처럼 각 클라이언트가 이 메서드를 각자 실행하면(특히 rewardsAllPlayers=true인 물범류나
// GrantStoryReward의 전체 지급) 자원·위습이 접속자 수만큼 중복 지급된다.
public class RewardDistributor : MonoBehaviour
{
    public static RewardDistributor Instance { get; private set; }

    // 게임 시작에 모든 플레이어가 받는 위습. 원작처럼 랜덤 위습 다섯 개로 시작한다 —
    // 이걸 자원 칸 북쪽 포탈에 넣으면 흔함 유닛이 하나씩 나온다(1% 상붕카).
    [SerializeField] WispData startingWisp;
    [SerializeField] int startingWispCount = 5;

    // 우물 한가운데 뭉쳐 있게 둔다. 8로 벌리면 별 모양으로 흩어져서 다섯 덩어리로 보이는데,
    // 이건 한 사람 몫의 시작 자원이라 한 무더기로 읽혀야 한다.
    // 위습끼리는 서로 통과하듯 겹치므로(회피 반지름 0.28) 이 정도면 자연스럽게 뭉친다.
    const float WispSpread = 2f;     // 한 주인의 위습들끼리 벌리는 반지름
    const float OwnerSpread = 20f;   // 주인끼리 벌리는 반지름

    void OnEnable()
    {
        Instance = this;
    }

    void Start()
    {
        GrantStartingWisps();
        GrantStartingTraitPoints();
    }

    // OnEnable이 아니라 Start인 이유: 위습은 WispCell 위치에 생기는데, 맵이 만들어지고
    // 셀들이 OnEnable로 등록을 끝낸 뒤여야 자기 칸을 찾는다.
    void GrantStartingWisps()
    {
        if (!GameAuthority.IsServer) return;
        if (startingWisp == null || startingWispCount <= 0) return;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            SpawnWisp(context, startingWisp, startingWispCount);
        }
    }

    // 특성포인트 3갈래 중 첫 번째 — 게임 시작 시 1개(사장님 확정 2026-09-03, 플레이어별).
    void GrantStartingTraitPoints()
    {
        if (!GameAuthority.IsServer) return;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            context.UnitUpgrades?.GrantStartingPoint();
        }
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 처치 골드 — 원작은 확정 지급이 아니라 25% 확률 지급이다(`war3map.j`의 `Trig_EnemyDeath2_Actions`
    // 직접 확인, 2026-09-04). "자기 라인 킬만"도 원작 그대로다 — 원작은 스폰 시점에
    // SetUnitUserData로 라인 번호를 박아두고 죽을 때 그 번호의 주인에게 준다(킬러를 안 본다).
    // 우리는 EnemyDummy.LaneIndex가 그 자리다.
    const float KillGoldChance = 0.25f;

    // 라운드 10~60에 유닛종별로 걸려 있던 보스 전용 고정 보너스. `Trig_BossReward_Actions` +
    // 조건 함수 6개를 전수 확인하고, 각 조건이 검사하는 유닛타입ID를 war3map.w3u 이름과
    // 대조해 라운드로 확정했다(R10=아론600+목1 … R60=센고쿠4000+목3). R65/70/75(신세계)는
    // 원작에도 이 보너스가 없다 — `Trig_BossReward_Func001C`가 `udg_Level<62`로 직접 게이트한다.
    static readonly Dictionary<int, (int gold, int wood)> BossRewardByRound = new Dictionary<int, (int, int)>
    {
        { 10, (600, 1) },
        { 20, (1500, 2) },
        { 30, (2500, 2) },
        { 40, (3000, 3) },
        { 50, (3000, 4) },
        { 60, (4000, 3) },
    };

    // round는 EnemyDummy.SpawnRound를 그대로 받는다 — RewardDistributor가 RoundManager를
    // 직접 찾지 않아도 되고, 죽는 순간 라운드가 막 넘어가도 "이 적이 태어난 라운드" 기준으로
    // 정확하다(GrantRoundClearWisps·OnBossKilled와 같은 이유).
    public void GrantKillReward(EnemyData data, int laneIndex, int round, int killerPlayerId = -1)
    {
        if (!GameAuthority.IsServer) return;
        if (data == null) return;

        if (data.rewardsAllPlayers)
        {
            // 빈 슬롯은 건너뛴다 — 스토리·라운드 보상이 이미 같은 규칙이다.
            foreach (PlayerContext context in PlayerContext.Occupied)
            {
                GrantTo(context, data);
            }
            return;
        }

        // 레인에 안 속한 적(크립·퀘스트 미니보스)은 레인 주인을 찾을 수 없다 —
        // LaneIndex가 −1이라 아래 PlayerContext.Get(-1)이 null이 되고 보상이 사라진다.
        // 원작도 이런 적은 처치자 기준이라 그쪽으로 보낸다(EnemyData.rewardsKillerOnly 주석 참고).
        if (data.rewardsKillerOnly)
        {
            GrantToKiller(data, killerPlayerId, round);
            return;
        }

        // 레인 소유자에게만 간다 — 누가 마지막 타격을 넣었는지는 안 본다(원작 그대로).
        PlayerContext owner = PlayerContext.Get(laneIndex);
        if (owner == null || !owner.IsOccupied) return;

        GrantKillGold(owner, round);
        GrantResources(owner, data);

        if (data.isBoss) GrantBossReward(owner, round);
    }

    // rewardsKillerOnly 전용 — 마지막 타격을 넣은 플레이어에게만.
    void GrantToKiller(EnemyData data, int killerPlayerId, int round)
    {
        PlayerContext killer = PlayerContext.Get(killerPlayerId);
        if (killer == null || !killer.IsOccupied)
        {
            // 조용히 넘기면 "왜 크립을 잡았는데 아무것도 안 나오지"가 된다.
            Debug.LogWarning($"RewardDistributor: {data.enemyName}의 처치자(플레이어 {killerPlayerId})를 " +
                             "찾지 못해 보상을 지급하지 못했습니다.", this);
            return;
        }

        if (data.goldReward > 0) killer.GoldWallet?.Add(data.goldReward);
        GrantResources(killer, data);
        if (data.isBoss) GrantBossReward(killer, round);
    }

    // rewardsAllPlayers 전용(물범류) — 확정 지급, 원작 별개 축이라 그대로 둔다.
    void GrantTo(PlayerContext context, EnemyData data)
    {
        // 비어 있는 플레이어 슬롯에도 물범 목재 등이 쌓이는 걸 막는다 (rewardsAllPlayers 전체 지급 경로).
        if (context == null || !context.IsOccupied) return;

        if (data.goldReward > 0 && context.GoldWallet != null)
        {
            context.GoldWallet.Add(data.goldReward);
        }

        GrantResources(context, data);
    }

    void GrantResources(PlayerContext context, EnemyData data)
    {
        if (data.resourceRewards == null || context.ResourceWallet == null) return;

        foreach (EnemyResourceReward reward in data.resourceRewards)
        {
            context.ResourceWallet.Add(reward.type, reward.amount);
        }
    }

    // Gold_Math(L) = 1 + 2⌊L/5⌋ + 3⌊L/6⌋ − ⌊L/10⌋ (정수 나눗셈) — war3map.j 원문 그대로.
    static int ComputeGoldMath(int round) => 1 + 2 * (round / 5) + 3 * (round / 6) - (round / 10);

    void GrantKillGold(PlayerContext context, int round)
    {
        if (context.GoldWallet == null) return;
        if (Random.value >= KillGoldChance) return;

        int goldMath = ComputeGoldMath(round);
        int goldPlus = context.GoldWallet.GoldPlus;
        context.GoldWallet.Add(goldMath * (2 + goldPlus));
    }

    void GrantBossReward(PlayerContext context, int round)
    {
        if (!BossRewardByRound.TryGetValue(round, out (int gold, int wood) reward)) return;

        context.GoldWallet?.Add(reward.gold);
        context.ResourceWallet?.Add(ResourceType.Wood, reward.wood);
    }

    // 스토리 클리어 보상: 전체 플레이어에게 골드 + 자원 + 위습 지급.
    // 보스 최다 데미지·도전과제 보상은 기여도 추적 시스템이 없어 아직 만들지 않았다(별도 작업).
    public void GrantStoryReward(StoryData storyReward)
    {
        if (!GameAuthority.IsServer) return;
        if (storyReward == null) return;

        foreach (PlayerContext context in PlayerContext.All)
        {
            // 처치 보상과 같은 이유로 빈 슬롯은 건너뛴다 — 아무도 없는 자리에 자원이 쌓인다.
            if (context == null || !context.IsOccupied) continue;

            if (storyReward.goldReward > 0 && context.GoldWallet != null)
            {
                context.GoldWallet.Add(storyReward.goldReward);
            }

            if (storyReward.resourceRewards != null && context.ResourceWallet != null)
            {
                foreach (EnemyResourceReward reward in storyReward.resourceRewards)
                {
                    context.ResourceWallet.Add(reward.type, reward.amount);
                }
            }

            GrantWisps(context, storyReward.wispRewards);

            // 특성포인트 3갈래 중 세 번째 — 스토리 8(사이버넷) 클리어 1개.
            // 사장님 확정(2026-09-03) 원문은 "8라운드"였으나, 질문 자체가 "스토리 깨면"이었고
            // 우리 스토리 8이 사이버넷이라 문맥상 스토리 8로 해석했다(라운드 8이 아니다) — PM 지시.
            // 진짜 라운드 8을 뜻한 것이면 이 조건을 RoundManager 쪽으로 옮겨야 한다.
            // 이 자리가 의미가 있다: 스토리 8 클리어 직후가 바로 《백수생활》 5분 대기 구간이라,
            // 포인트를 받자마자 초월위습 3택1이 열리는 분기점이 된다.
            if (storyReward.order == 8)
            {
                context.UnitUpgrades?.GrantStoryPoint();
            }
        }
    }

    public void GrantWisps(PlayerContext context, List<WispReward> wispRewards)
    {
        if (context == null || wispRewards == null) return;

        foreach (WispReward reward in wispRewards)
        {
            if (reward == null || reward.wisp == null) continue;
            SpawnWisp(context, reward.wisp, Mathf.Max(1, reward.count));
        }
    }

    void SpawnWisp(PlayerContext context, WispData wispData, int count)
    {
        if (wispData.prefab == null)
        {
            Debug.LogWarning($"RewardDistributor: {wispData.wispName} WispData에 prefab이 없어 위습을 생성하지 못했습니다.");
            return;
        }

        // 위습은 자기 등급 칸 안에서 생긴다. 칸은 막혀 있어 다른 등급 포탈로 새지 않는다.
        // 칸이 아직 없는 등급이면 플레이어 위치에 떨어뜨린다.
        WispCell cell = WispCell.Get(wispData.targetGrade);
        Vector3 origin = cell != null ? cell.transform.position : context.transform.position;

        // 위습 칸은 플레이어 넷이 함께 쓴다. 넷이 같은 점에 쏟아지면 스무 개가 겹쳐서,
        // 어느 것이 내 것인지 알 수 없고 남의 위습을 아무리 눌러도 안 움직인다.
        // 주인마다 칸 안의 다른 자리를 쓰고, 그 안에서 다시 원을 그린다.
        int players = Mathf.Max(1, PlayerContext.OccupiedCount);
        float ownerAngle = 360f / players * context.PlayerId;
        Vector3 ownerSpot = players > 1
            ? Quaternion.Euler(0f, ownerAngle, 0f) * Vector3.forward * OwnerSpread
            : Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            // 같은 점에 겹쳐 놓으면 서로 밀어내느라 흩어진다. 위습 굵기만큼 벌려 놓는다.
            Vector3 offset = ownerSpot + (count > 1
                ? Quaternion.Euler(0f, 360f / count * i, 0f) * Vector3.forward * WispSpread
                : Vector3.zero);

            GameObject instance = Instantiate(wispData.prefab, origin + offset, Quaternion.identity);

            // 좌표만 주고 놓으면 NavMesh에서 살짝 벗어났을 때 에이전트가 안 붙고,
            // 그 위습은 선택은 되는데 이동 명령이 조용히 무시된다.
            NavPlacement.PlaceObject(instance, origin + offset);

            if (!instance.TryGetComponent(out Wisp wisp))
            {
                wisp = instance.AddComponent<Wisp>();
            }
            wisp.SetData(wispData);

            if (!instance.TryGetComponent(out OwnedByPlayer owner))
            {
                owner = instance.AddComponent<OwnedByPlayer>();
            }
            owner.SetOwner(context.PlayerId);
        }
    }
}
