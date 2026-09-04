using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [SerializeField] WaveSpawner waveSpawner;
    [SerializeField] List<WaveData> rounds;
    // 원작 40.65초(리서치담당 확인, 2026-09-04). 씬에 이미 굳어 있는 값은 별도로 맞춰야
    // 한다 — 이 기본값은 새로 만들어지는 인스턴스용이다.
    [SerializeField] float roundDuration = 40.65f;
    // 원작은 보스 라운드가 훨씬 길다("제한시간내에 처치하세요" 메시지까지 뜬다) — 일반
    // 라운드의 2.7배가 아니라 보스 자체가 75.4초짜리다. WaveData.IsBossRound로 가른다.
    [SerializeField] float bossRoundDuration = 75.4f;
    // 신세계(61라운드+)는 원작에서 `Mode_TimerReal +2`로 오히려 짧아진다 — 후반이 빨라지는
    // 게 원작 설계다. 보스 여부와 무관하게 이 값 하나로 통일된다(원작이 그렇다).
    [SerializeField] float newWorldRoundDuration = 38.67f;
    [SerializeField] int newWorldStartRound = 61;
    // 원작 준비 시간 — 1라운드 시작 전 21초(첫 조합할 시간), 60라운드(신세계 진입) 전 40초.
    // 0으로 두면 예전처럼 대기 없이 바로 시작한다.
    [SerializeField] float firstRoundDelay = 21f;
    [SerializeField] float round60Delay = 40f;
    // 씬에는 75가 들어 있다. 기본값이 25로 남아 있으면 새로 만든 씬이 조용히
    // 25라운드짜리가 된다 — 웨이브 에셋은 Wave_Round01~75로 다 있다.
    [SerializeField] int totalRounds = 75;
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

    // 해적단 퀘스트 실패 페널티("다음 2라운드동안 랜덤위습을 받지 못합니다", war3map.j
    // 원문 그대로) — 플레이어별로 몇 라운드 더 막을지 센다. GrantFlatRoundReward가 매
    // 라운드 넘어갈 때 이 값을 보고 건너뛴 뒤 하나씩 줄인다.
    readonly int[] wispBlockRoundsRemaining = new int[MaxTrackedLanes];

    int currentRound;
    float roundTimer;
    bool isGameOver;

    // 라운드 시작 전 대기(1라운드 전 21초, 60라운드 전 40초). 대기 중엔 웨이브를 안 내보내고
    // roundTimer도 안 돈다 — 새 라운드가 아니라 "아직 안 시작함" 상태라서다.
    bool waitingForNextRound;
    int pendingRoundNumber;
    float preRoundTimer;

    public int CurrentRound => currentRound;
    public float RoundTimeLeft => roundTimer;
    public bool IsGameOver => isGameOver;
    public bool IsWaitingForNextRound => waitingForNextRound;
    public float PreRoundTimeLeft => preRoundTimer;

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
        BeginPreRoundWait(1, firstRoundDelay);
    }

    void Update()
    {
        if (!GameAuthority.IsServer) return;
        if (isGameOver) return;

        UpdateDeathCount();
        if (isGameOver) return;

        if (waitingForNextRound)
        {
            preRoundTimer -= Time.deltaTime;
            if (preRoundTimer <= 0f)
            {
                waitingForNextRound = false;
                StartRound(pendingRoundNumber);
            }
            return;
        }

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
        {
            AdvanceRound();
        }
    }

    // delay가 0 이하면 대기 없이 바로 시작한다 — 준비 시간을 끄고 싶을 때 인스펙터에서 0으로만
    // 두면 된다(기존 즉시-시작 동작으로 되돌아간다).
    void BeginPreRoundWait(int roundNumber, float delay)
    {
        if (delay <= 0f)
        {
            StartRound(roundNumber);
            return;
        }

        pendingRoundNumber = roundNumber;
        preRoundTimer = delay;
        waitingForNextRound = true;
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

        // 60라운드(신세계 진입) 전에만 원작대로 대기시간을 둔다. 나머지는 예전처럼 바로 이어진다.
        BeginPreRoundWait(currentRound, currentRound == 60 ? round60Delay : 0f);
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
            int playerId = context.PlayerId;
            if (playerId >= 0 && playerId < MaxTrackedLanes && wispBlockRoundsRemaining[playerId] > 0)
            {
                wispBlockRoundsRemaining[playerId]--;
                Debug.Log($"플레이어 {playerId + 1}: 해적단 퀘스트 실패 페널티로 이번 라운드 위습을 받지 못했습니다 " +
                          $"(남은 차단 {wispBlockRoundsRemaining[playerId]}라운드).");
                continue;
            }

            distributor.GrantWisps(context, rewards);
        }
    }

    /// <summary>
    /// 해적단 퀘스트 실패 페널티. 다음 <paramref name="rounds"/>라운드 동안 그 플레이어는
    /// 라운드 클리어 위습(GrantFlatRoundReward)을 못 받는다. 이미 남아있는 차단과는
    /// 더하지 않고 더 큰 쪽을 취한다 — 같은 퀘스트를 연속으로 실패해도 무한히 안 쌓인다.
    /// </summary>
    public void BlockRoundRewardWisp(int playerId, int rounds)
    {
        if (playerId < 0 || playerId >= MaxTrackedLanes || rounds <= 0) return;

        wispBlockRoundsRemaining[playerId] = Mathf.Max(wispBlockRoundsRemaining[playerId], rounds);
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
        WaveData waveData = GetWaveData(roundNumber);

        // 신세계가 최우선이다 — 원작은 보스 여부와 무관하게 61라운드부터 이 길이 하나로
        // 통일한다(Mode_TimerReal +2로 오히려 짧아짐). 그 아래에서만 보스/일반을 가른다.
        if (roundNumber >= newWorldStartRound)
            roundTimer = newWorldRoundDuration;
        else if (waveData != null && waveData.IsBossRound)
            roundTimer = bossRoundDuration;
        else
            roundTimer = roundDuration;

        if (waveData != null && waveData.IsBossRound)
        {
            Debug.Log($"보스 라운드! (라운드 {roundNumber})");
        }

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
