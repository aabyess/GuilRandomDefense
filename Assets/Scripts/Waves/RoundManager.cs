using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [SerializeField] WaveSpawner waveSpawner;
    [SerializeField] List<WaveData> rounds;
    [SerializeField] float roundDuration = 28f;
    [SerializeField] int totalRounds = 25;
    [SerializeField] int bossRoundInterval = 10;
    // 레인 하나 기준. 전체 합이 아니다. 씬의 실제 값은 MapGenerator.WireRoundRewardWisp가
    // 맵 생성 때마다 100으로 맞춘다(사장님 지시, 2026-09-03: 25→100) — 여기 기본값도
    // 새로 만들어지는 인스턴스가 헷갈리지 않게 같이 맞춰둔다.
    [SerializeField] int enemyCountThreshold = 100;
    [SerializeField] bool deathCountEnabled = true;  // 구조를 볼 땐 꺼두고 테스트한다.
    [SerializeField] int startingDeathCount = 10;
    [SerializeField] float deathCountTickInterval = 1f;

    [Header("라운드 클리어 보상 — 사장님 지시: 라운드 하나 지날 때마다 랜덤위습 N개")]
    [SerializeField] WispData roundRewardWisp;
    [SerializeField] int roundRewardCount = 2;

    int currentRound;
    float roundTimer;
    bool isGameOver;

    public int CurrentRound => currentRound;
    public float RoundTimeLeft => roundTimer;
    public bool IsGameOver => isGameOver;

    // 원작의 패배 조건은 "라인 카운트"다 — 전체 합이 아니라 한 레인에 쌓인 수.
    // 레인이 4개가 된 뒤로 전체 합을 쓰면 같은 압박에도 4배로 세어져 즉시 패배한다.
    const int MaxTrackedLanes = 8;
    static readonly int[] laneCounts = new int[MaxTrackedLanes];

    // 데스카운트도 이제 레인(=플레이어 ID)마다 따로 돈다(사장님 지시, 2026-09-03) — 예전엔
    // 레인 중 최댓값 하나로 게임 전체가 끝났는데, 4인 게임에서 한 명 때문에 전부 끝나면 안 된다.
    readonly int[] laneDeathCount = new int[MaxTrackedLanes];
    readonly float[] laneDeathTimer = new float[MaxTrackedLanes];

    /// <summary>그 플레이어의 지금 데스카운트. 없는 플레이어면 0.</summary>
    public int DeathCountFor(int playerId)
    {
        return playerId >= 0 && playerId < MaxTrackedLanes ? laneDeathCount[playerId] : 0;
    }

    void Start()
    {
        for (int i = 0; i < MaxTrackedLanes; i++)
        {
            laneDeathCount[i] = startingDeathCount;
            laneDeathTimer[i] = deathCountTickInterval;
        }

        currentRound = 1;
        StartRound(currentRound);
    }

    void Update()
    {
        if (!GameAuthority.IsServer) return;
        if (isGameOver) return;

        UpdateDeathCount();
        if (isGameOver) return;

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
        {
            AdvanceRound();
        }
    }

    void UpdateDeathCount()
    {
        if (!deathCountEnabled) return;

        UpdateLaneEnemyCounts();

        for (int playerId = 0; playerId < MaxTrackedLanes; playerId++)
        {
            PlayerContext context = PlayerContext.Get(playerId);
            if (context == null || !context.IsOccupied || context.IsDead) continue;

            if (laneCounts[playerId] <= enemyCountThreshold)
            {
                // 사장님 원문: "그 안에 100 아래로 줄지 않을시" — 줄이면 사는 뜻이라 카운트다운
                // 자체가 취소돼야 한다(PM 지시, 2026-09-03). 예전(레인이 하나였을 때)엔 안
                // 돌려놨는데, 그러면 75라운드짜리 게임에서 평생 10초치만 밀려도 죽는
                // "누적 경고" 시스템이 되어 사장님 의도와 달라진다 — 회복 방식으로 바꿨다.
                laneDeathTimer[playerId] = deathCountTickInterval;
                laneDeathCount[playerId] = startingDeathCount;
                continue;
            }

            laneDeathTimer[playerId] -= Time.deltaTime;
            if (laneDeathTimer[playerId] > 0f) continue;

            laneDeathTimer[playerId] = deathCountTickInterval;
            laneDeathCount[playerId]--;
            Debug.Log($"플레이어 {playerId + 1} 데스카운트: {laneDeathCount[playerId]}");

            if (laneDeathCount[playerId] <= 0)
            {
                HandlePlayerDefeated(playerId, context);
            }
        }
    }

    // 매 프레임 도는 경로라 한 번만 훑는다. 레인이 없는 적(-1, 물범·스토리 건물 등)은 어느
    // 플레이어의 카운트다운과도 무관해서 이제 안 센다 — 예전 전역 최댓값 방식의 부산물이었다.
    static void UpdateLaneEnemyCounts()
    {
        System.Array.Clear(laneCounts, 0, laneCounts.Length);

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            int lane = enemy.LaneIndex;
            if (lane < 0 || lane >= MaxTrackedLanes) continue;

            laneCounts[lane]++;
        }
    }

    // 사망 정리 — (가) 해석: 그 플레이어의 레인만 정리한다(4인 중 하나가 죽었다고 남의 레인
    // 적까지 사라지면 그건 보상이 된다). PM 승인 완료(2026-09-03) — 사장님 확인은 PM이 진행.
    // 위습·상점 건물은 안 건드린다 — 사장님 원문("유닛들"·"적유닛")에 없고, 특히 건물을
    // 부수면 되돌릴 방법이 없어진다.
    void HandlePlayerDefeated(int playerId, PlayerContext context)
    {
        context.MarkDead();
        Debug.Log($"플레이어 {playerId + 1} 사망");

        if (context.UnitInventory != null)
        {
            // Consume()이 이 목록 자체를 지운다 — 돌면서 지우면 안 되니 스냅샷부터 뜬다.
            List<UnitIdentity> units = new List<UnitIdentity>(context.UnitInventory.Members);
            foreach (UnitIdentity unit in units)
                if (unit != null) unit.Consume();
        }

        // TakeDamage를 거치지 않고 바로 없앤다 — 전멸 정리는 킬이 아니라서 보상이 나가면 안 된다.
        List<EnemyDummy> enemies = new List<EnemyDummy>(EnemyDummy.Active);
        foreach (EnemyDummy enemy in enemies)
            if (enemy != null && enemy.LaneIndex == playerId) Destroy(enemy.gameObject);

        CheckAllDefeated();
    }

    void CheckAllDefeated()
    {
        foreach (PlayerContext context in PlayerContext.Occupied)
            if (!context.IsDead) return;

        Debug.Log("전멸 — 게임 오버");
        isGameOver = true;
    }

    void AdvanceRound()
    {
        GrantRoundClearWisps(currentRound);

        currentRound++;
        if (currentRound > totalRounds)
        {
            Debug.Log("모든 라운드 클리어!");
            isGameOver = true;
            return;
        }

        // 다음 라운드가 실제로 시작될 때만 준다 — 마지막 라운드를 넘기지 못하고 위에서
        // 게임이 끝나는 경로로 빠지면 여기까지 안 온다. 1라운드 시작(Start())에서는 이 메서드
        // 자체가 안 불리므로 시작 위습(RewardDistributor.GrantStartingWisps)과도 안 겹친다.
        GrantFlatRoundReward();

        StartRound(currentRound);
    }

    // "라운드 하나 지날 때마다" 랜덤위습 N개(사장님 지시, 기본 2개) — RewardDistributor의
    // 기존 GrantWisps 경로를 그대로 쓴다(새 지급 경로를 만들지 않는다).
    void GrantFlatRoundReward()
    {
        if (!GameAuthority.IsServer) return;
        if (roundRewardWisp == null || roundRewardCount <= 0) return;

        RewardDistributor distributor = RewardDistributor.Instance;
        if (distributor == null)
        {
            Debug.LogWarning("RoundManager: RewardDistributor.Instance가 없어 라운드 클리어 위습을 지급하지 못했습니다.");
            return;
        }

        List<WispReward> rewards = new List<WispReward> { new WispReward { wisp = roundRewardWisp, count = roundRewardCount } };

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            distributor.GrantWisps(context, rewards);
        }
    }

    // 방금 끝난 라운드(roundNumber)의 위습 보상을 전체 플레이어에게 지급한다.
    // AdvanceRound()는 Update() 안에서만 호출되고, Update()는 최상단에서 GameAuthority.IsServer를 확인하므로
    // 이 메서드도 자연히 서버에서만 실행된다.
    void GrantRoundClearWisps(int roundNumber)
    {
        WaveData waveData = GetWaveData(roundNumber);
        if (waveData == null || waveData.wispRewards == null || waveData.wispRewards.Count == 0) return;

        RewardDistributor distributor = RewardDistributor.Instance;
        if (distributor == null)
        {
            Debug.LogWarning($"RoundManager: RewardDistributor.Instance가 없어 {roundNumber}라운드 클리어 위습을 지급하지 못했습니다.");
            return;
        }

        foreach (PlayerContext context in PlayerContext.All)
        {
            // 위습은 인벤토리가 아니라 필드에 실물로 생긴다. 빈 슬롯에 주면 아무도 안 쓰는 채로 쌓인다.
            if (!context.IsOccupied) continue;
            distributor.GrantWisps(context, waveData.wispRewards);
        }
    }

    void StartRound(int roundNumber)
    {
        roundTimer = roundDuration;

        if (roundNumber % bossRoundInterval == 0)
        {
            Debug.Log("보스 라운드!");
        }

        WaveData waveData = GetWaveData(roundNumber);
        if (waveSpawner != null && waveData != null)
        {
            waveSpawner.SpawnRound(waveData);
        }
    }

    WaveData GetWaveData(int roundNumber)
    {
        if (rounds == null) return null;

        foreach (WaveData waveData in rounds)
        {
            if (waveData != null && waveData.roundNumber == roundNumber)
                return waveData;
        }

        return null;
    }
}
