using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [SerializeField] WaveSpawner waveSpawner;
    [SerializeField] List<WaveData> rounds;
    [SerializeField] float roundDuration = 28f;
    [SerializeField] int totalRounds = 25;
    [SerializeField] int bossRoundInterval = 10;
    [SerializeField] int enemyCountThreshold = 25;   // 레인 하나 기준. 전체 합이 아니다.
    [SerializeField] bool deathCountEnabled = true;  // 구조를 볼 땐 꺼두고 테스트한다.
    [SerializeField] int startingDeathCount = 10;
    [SerializeField] float deathCountTickInterval = 1f;

    [Header("라운드 클리어 보상 — 사장님 지시: 라운드 하나 지날 때마다 랜덤위습 N개")]
    [SerializeField] WispData roundRewardWisp;
    [SerializeField] int roundRewardCount = 2;

    int currentRound;
    float roundTimer;
    int deathCount;
    float deathCountTimer;
    bool isGameOver;

    public int CurrentRound => currentRound;
    public float RoundTimeLeft => roundTimer;
    public int DeathCount => deathCount;
    public bool IsGameOver => isGameOver;

    void Start()
    {
        deathCount = startingDeathCount;
        deathCountTimer = deathCountTickInterval;

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

        if (MaxLaneEnemyCount() <= enemyCountThreshold)
        {
            deathCountTimer = deathCountTickInterval;
            return;
        }

        deathCountTimer -= Time.deltaTime;
        if (deathCountTimer > 0f) return;

        deathCountTimer = deathCountTickInterval;
        deathCount--;
        Debug.Log($"데스카운트: {deathCount}");

        if (deathCount <= 0)
        {
            Debug.Log("패배!");
            isGameOver = true;
        }
    }

    // 원작의 패배 조건은 "라인 카운트"다 — 전체 합이 아니라 한 레인에 쌓인 수.
    // 레인이 4개가 된 뒤로 전체 합을 쓰면 같은 압박에도 4배로 세어져 즉시 패배한다.
    const int MaxTrackedLanes = 8;
    static readonly int[] laneCounts = new int[MaxTrackedLanes];

    static int MaxLaneEnemyCount()
    {
        System.Array.Clear(laneCounts, 0, laneCounts.Length);
        int unassigned = 0;

        // 매 프레임 도는 경로라 한 번만 훑는다.
        // 레인별로 세고 최댓값을 취하면 O(적 수)로 끝난다.
        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            int lane = enemy.LaneIndex;
            if (lane < 0 || lane >= MaxTrackedLanes)
            {
                unassigned++;
                continue;
            }

            laneCounts[lane]++;
        }

        int max = unassigned;
        for (int i = 0; i < laneCounts.Length; i++)
            if (laneCounts[i] > max) max = laneCounts[i];

        return max;
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
