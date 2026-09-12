using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [SerializeField] WaveSpawner waveSpawner;
    [SerializeField] List<WaveData> rounds;
    // ⚠️ 2026-09-06 정정(리서치담당 원작 레지스터 직접 확인): R1만 40.65초다 — 원작이
    // 이 값을 별도로 하드코딩한다. 씬에 이미 이 값(40.65)이 굳어 있는데, 마침 R1
    // 전용값과 정확히 같아서 그대로 둬도 맞다(필드명·기본값 둘 다 안 건드림).
    [SerializeField] float roundDuration = 40.65f;
    // R2~R39. 예전엔 roundDuration 하나로 R1까지 같이 썼는데(40.65) 원작은 R2부터
    // 40.67로 미세하게 다르다 — 새 필드라 씬엔 아직 없다(추가해도 씬 덮어쓰기 문제가
    // 없다, 기존 필드는 안 건드렸다).
    [SerializeField] float normalRoundDuration = 40.67f;
    // 원작은 보스 라운드가 훨씬 길다("제한시간내에 처치하세요" 메시지까지 뜬다) — 일반
    // 라운드의 2.7배가 아니라 보스 자체가 75.4초짜리다. WaveData.IsBossRound로 가른다.
    [SerializeField] float bossRoundDuration = 75.4f;
    // ⚠️ 2026-09-06 재정정(리서치담당이 자기 해석을 뒤집음): R40에서 `Mode_TimerReal=2.00`이
    // 걸려 40.67−2=38.67이 되는 것까진 맞다. R40~R60 구간의 고정값 — 이 아래 R61+와는
    // 다른 값이라 별도 필드로 뗐다(이전 커밋은 이 둘을 같은 필드로 묶었었다, 정정).
    // ⚠️ 새 필드명 — 씬엔 아직 없다. 옛 이름(newWorldRoundDuration)이었다면 씬에 굳어있는
    // 38.67이 새 기본값을 덮었을 텐데(이번 정정에서 R61+ 값이 38.67→36.67로 바뀌어서
    // 이제 그 옛 필드를 R61+에 그대로 못 쓴다), 이름을 새로 지어서 그 문제를 피했다.
    [SerializeField] float shortRoundDuration = 38.67f;
    // R40부터 위 shortRoundDuration(38.67) 고정 구간이 시작된다 — 새 필드라 씬엔 없다.
    [SerializeField] int shortRoundStartRound = 40;
    // ⚠️ 2026-09-06 재정정: `Mode_TimerReal += 2.00`은 **맵 전체에서 한 곳뿐**이고 그
    // 블록의 진입 조건이 `Level == 61`이다 — **61라운드 진입 시 딱 한 번**만 실행된다
    // (리서치담당, 처음엔 "라운드마다 누적"으로 잘못 읽었다가 정정). 즉 R61~R75(15개)는
    // **전부 36.67로 동일** — 라운드가 갈수록 더 짧아지는 게 아니다. 예전 필드
    // (newWorldRoundDuration, 38.67 고정)는 이 값이 R40~60과 같다고 잘못 가정했던
    // 흔적이라 새 필드로 갈아치웠다 — **이름을 바꾼 이유**: 씬에 옛 필드명으로 38.67이
    // 굳어 있는데, 이름을 그대로 두고 기본값만 36.67로 고치면 씬의 38.67이 코드 기본값을
    // 덮어써서 R61+가 계속 38.67로 돈다. 새 이름을 쓰면 씬엔 아직 없는 필드라 이 기본값
    // (36.67)이 그대로 반영된다 — 씬을 직접 못 고치는 제약 안에서 값을 바꾸는 유일한
    // 방법이다. 옛 필드명은 씬에 무해하게 남는다(아무도 안 읽음).
    [SerializeField] float finalRoundDuration = 36.67f;
    [SerializeField] int newWorldStartRound = 61;
    // 원작 준비 시간 — 1라운드 시작 전 21초(첫 조합할 시간), 60라운드(신세계 진입) 전 40초.
    // 0으로 두면 예전처럼 대기 없이 바로 시작한다.
    [SerializeField] float firstRoundDelay = 21f;
    [SerializeField] float round60Delay = 40f;
    // 씬에는 75가 들어 있다. 기본값이 25로 남아 있으면 새로 만든 씬이 조용히
    // 25라운드짜리가 된다 — 웨이브 에셋은 Wave_Round01~75로 다 있다.
    // 2026-09-11(PM 지시): 난이도 선택 즉시 DifficultyModeData.totalRounds(쉬움50·보통60·
    // 나머지75)로 덮어쓴다 — 이 필드는 그때까지의(그리고 DifficultyManager가 없을 때의)
    // 기본값일 뿐이다.
    [SerializeField] int totalRounds = 75;

    [Header("41라운드 게이트 — [미확인] 원작 스토리11(드레스로사)/12(홀케이크섬) 대응 확정 안 됨")]
    // 2026-09-11(PM 지시 F) — 지옥은 원작 스토리11, 신·악몽은 스토리12 클리어 여부로 41라운드에
    // 전멸/유닛수한계변경이 갈린다. 그런데 우리 스토리 13개는 원작과 번호·내용이 무관한 창작
    // 콘텐츠고(story-numbering-is-ours), Docs/reference/DIFFICULTY_RESEARCH.md가 제안한
    // "우리 11번째(日本)=원작 스토리11· 우리 12번째(코드잇)=원작 스토리12" 매핑은 그 문서
    // 자신이 "[제안] — 순서만 맞춘 것이고 원작 근거는 아니다"라고 명시한 미확정 값이다.
    // 지어낸 매핑으로 전멸시키는 건 안 하는 게 안전하다(PM: "훅만 두고 [미확인] 꼬리표 +
    // 보고") — 그래서 여기는 "StoryManager.FinishedCount가 이 값 이상이어야 통과"라는 값을
    // 담는 빈 슬롯만 두고 기본값 0(=검사 비활성, 통과 취급)으로 둔다. 매핑이 확정되면
    // 사장님/PM이 인스펙터에서 11·12를 채우면 그걸로 끝난다 — 코드 변경이 필요 없다.
    [SerializeField] int hellRound41ClearGateOrder;      // 지옥 — [미확인], 0=검사 안 함
    [SerializeField] int godNightmareRound41ClearGateOrder; // 신·악몽 — [미확인], 0=검사 안 함

    // 보스 타임리밋 패배(2단계 A, 원작 Trig_Enemy_Boss_create/sinsekai의 SleepForStageAdd).
    // 구세계 보스(R10~60)는 스폰 후 75.30초, 신세계 보스(R65/70/75)는 34.80초 지나도 살아
    // 있으면 그 보스가 선 레인의 플레이어가 패배한다. 쉬움 모드는 구세계 보스만 면제된다
    // (원작 문구 "쉬움 모드는 보스를 잡지 않아도 패배하지 않습니다") — 신세계는 쉬움이
    // 50라운드에서 끝나 애초에 도달하지 못하지만, PM 지시대로 모드 예외 없이 균일하게 둔다.
    [SerializeField] float oldWorldBossTimeLimit = 75.30f;
    [SerializeField] float newWorldBossTimeLimit = 34.80f;
    // 레인 하나 기준. 전체 합이 아니다. 원작 udg_ModeEnemyInt — 여섯 난이도 전부 70으로
    // 시작한다(Trig_Select_effect_Actions). 2026-09-03 사장님 지시로 100이었다가 2026-09-11
    // 「원작대로 바꾸자」로 70. 씬 값은 MapGenerator.WireRoundRewardWisp가 맵 생성 때 맞춘다.
    // ⚠️ 원작 지옥·신·악몽은 41라운드에 60/55/50으로 내려간다 — 우리엔 아직 난이도가 없다.
    [SerializeField] int enemyCountThreshold = 70;
    [SerializeField] bool deathCountEnabled = true;  // 구조를 볼 땐 꺼두고 테스트한다.
    // 원작 udg_Counter_death_amount 초기값 9, 틱 0.65초(InitTrig_DeathTimer5의 TimerStart).
    [SerializeField] int startingDeathCount = 9;
    [SerializeField] float deathCountTickInterval = 0.65f;
    // 신세계 대기(「60라운드-신세계 대기중」) 때 원작이 전원의 데스카운트를 1로 덮어쓴다(Trig_Round_10ver).
    const int NewWorldDeathCount = 1;

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

    // 2026-09-11(PM 지시 B) — 원작 InitTrig_Select1/Select_effect: 라운드는 호스트가 난이도를
    // 고르기 전엔 시작되지 않는다(제한시간·기본값 없음, 무한 대기). DifficultyManager가 없는
    // 씬(맵 생성 전, 또는 구조를 볼 때 끄고 테스트하는 씬)에서는 예전처럼 즉시 시작한다 —
    // 회귀 없음.
    bool roundsStarted;

    void Start()
    {
        for (int i = 0; i < MaxTrackedLanes; i++)
        {
            laneDeathCount[i] = startingDeathCount;
            laneDeathTimer[i] = deathCountTickInterval;
        }

        currentRound = 1;

        if (DifficultyManager.Instance == null)
        {
            roundsStarted = true;
            BeginPreRoundWait(1, firstRoundDelay);
        }

        // 보스 타임리밋 패배(2단계 A) — waveSpawner가 라운드보스를 스폰할 때마다 이 알림을 받는다.
        if (waveSpawner != null) waveSpawner.OnRoundBossSpawned += OnRoundBossSpawned;
    }

    void Update()
    {
        if (!GameAuthority.IsServer) return;
        if (isGameOver) return;

        if (!roundsStarted)
        {
            if (DifficultyManager.Instance == null || !DifficultyManager.Instance.IsModeSelected) return;

            totalRounds = DifficultyManager.Instance.CurrentData.totalRounds;
            roundsStarted = true;
            BeginPreRoundWait(1, firstRoundDelay);
            return;
        }

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

            // 원작 Trig_DeathTimer5: 유닛 수가 한계 **이상**이면 깎고, 아래로 내려가도 **되돌리지
            // 않는다** — Counter_death_amount를 올리는 자리가 원문 전체에 없다(판 전체 누적).
            // 2026-09-03엔 사장님 지시로 회복식이었는데 2026-09-11 「원작대로 바꾸자」로 누적식.
            if (laneCounts[playerId] < enemyCountThreshold)
            {
                laneDeathTimer[playerId] = deathCountTickInterval;
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

        // 원작 udg_PlayerDeath[i]=1 분기의 같은 SetPlayerStateBJ(플레이어, GOLD, 0).
        RewardDistributor.Instance?.ConfiscateGoldOnPlayerDefeated(context);

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

    // 보스 타임리밋 패배(2단계 A) — 원작 Trig_Enemy_Boss_create(구세계)/Trig_Enemy_Boss_sinsekai
    // (신세계)의 SleepForStageAdd 뒤 생존 판정. WaveSpawner.OnRoundBossSpawned는 일반
    // 스폰목록을 타는 라운드보스가 뜰 때만 쏜다(사이드보스 62/66/71·광폭화 소환 몹은
    // 이 이벤트 자체가 안 온다 — WaveSpawner 쪽 게이트 참고).
    void OnRoundBossSpawned(int laneIndex, int roundNumber, EnemyDummy boss)
    {
        StartCoroutine(BossTimeoutRoutine(laneIndex, roundNumber, boss));
    }

    IEnumerator BossTimeoutRoutine(int laneIndex, int roundNumber, EnemyDummy boss)
    {
        bool isNewWorldBoss = roundNumber >= 65; // R65/70/75. 구세계는 R10/20/30/40/50/60.
        float timeLimit = isNewWorldBoss ? newWorldBossTimeLimit : oldWorldBossTimeLimit;

        yield return new WaitForSeconds(timeLimit);

        // 판이 이미 끝났으면(전멸·마지막 라운드 클리어) 판정하지 않는다 — 클리어한 사람을
        // 뒤늦게 탈락시키면 안 된다.
        if (isGameOver) yield break;

        if (boss == null) yield break; // 이미 잡았다 — 유니티 오버로드 null이라 파괴된 개체를 정확히 건진다.

        PlayerContext context = PlayerContext.Get(laneIndex);
        if (context == null || !context.IsOccupied || context.IsDead) yield break;

        // 쉬움은 구세계 보스만 면제(원작 문구, TRIGSTR 미확인 — PM이 준 원문 그대로 인용).
        // 신세계는 PM 지시대로 모드 예외 없이 균일 적용(쉬움이 50R에서 끝나 실질적으로
        // 도달 못 하는 것과는 별개로, 코드에 특례를 안 둔다).
        bool isEasy = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected
            && DifficultyManager.Instance.Current == DifficultyMode.Easy;

        if (!isNewWorldBoss && isEasy)
        {
            PlayerNotification.Show(laneIndex, "쉬움 모드는 보스를 잡지 않아도 패배하지 않습니다.");
            yield break;
        }

        // 원문 TRIGSTR_10804.
        PlayerNotification.Show(laneIndex, "제한시간안에 보스를 잡지 못해 패배하였습니다.");
        HandlePlayerDefeated(laneIndex, context);
    }

    void CheckAllDefeated()
    {
        foreach (PlayerContext context in PlayerContext.Occupied)
            if (!context.IsDead) return;

        Debug.Log("전멸 — 게임 오버");
        isGameOver = true;
        // 저장 안 함 — 여기 도달했다는 건 Occupied 전원이 IsDead라는 뜻이라(위 루프 조건),
        // 원작(SavePlayer: "패배한 상태에선 더이상 세이브가 불가능합니다")대로 저장할 대상이
        // 애초에 없다.
    }

    // war3map.j Trig_Save_sido3(신세계 클리어) 대응 — 원작은 여기서 유닛카운트 보너스를
    // 계산하고 SavePlayer를 직접 부른다. 죽은 플레이어는 여기서 걸러진다 — PersistentSave는
    // 자기 컨텍스트를 안 들고 있어서(IsDead를 스스로 못 본다) 호출부가 반드시 걸러야 한다.
    void FinishPersistentSave(bool cleared)
    {
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            if (context.IsDead || context.PersistentSave == null) continue;

            if (cleared)
            {
                int alive = PersistentSave.CountAliveInLane(context.PlayerId);
                context.PersistentSave.AddClearBonus(alive);
            }

            context.PersistentSave.FinishRun(cleared);
        }
    }

    void AdvanceRound()
    {
        GrantRoundClearWisps(currentRound);
        GrantItemGambleStock(currentRound);

        currentRound++;
        if (currentRound > totalRounds)
        {
            Debug.Log("모든 라운드 클리어!");
            isGameOver = true;
            FinishPersistentSave(cleared: true);
            return;
        }

        // F — 지옥·신·악몽만 41라운드에 게이트가 있다. 전멸 처리됐으면 다음 라운드로 안 넘어간다.
        if (currentRound == 41 && ApplyRound41DifficultyGate())
        {
            return;
        }

        // 다음 라운드가 실제로 시작될 때만 준다 — 마지막 라운드를 넘기지 못하고 위에서
        // 게임이 끝나는 경로로 빠지면 여기까지 안 온다. 1라운드 시작(Start())에서는 이 메서드
        // 자체가 안 불리므로 시작 위습(RewardDistributor.GrantStartingWisps)과도 안 겹친다.
        GrantFlatRoundReward();

        // 60라운드(신세계 진입) 전에만 원작대로 대기시간을 둔다. 나머지는 예전처럼 바로 이어진다.
        // 원작은 같은 블록에서 데스카운트를 전원 1로 덮어쓴다 — 신세계부턴 한 번 넘치면 끝이다.
        if (currentRound == 60)
        {
            for (int i = 0; i < MaxTrackedLanes; i++)
                laneDeathCount[i] = NewWorldDeathCount;
        }
        BeginPreRoundWait(currentRound, currentRound == 60 ? round60Delay : 0f);
    }

    // F — 41라운드 지옥/신/악몽 게이트. 통과 못 하면 전멸시키고 true를 돌려준다(호출부가 그
    // 라운드 진행을 멈춘다). 쉬움·보통·어려움이거나 DifficultyManager가 없으면(맵 생성 전
    // 등) 항상 false — 이 라운드는 평범하게 지나간다.
    //
    // [미확인] — hellRound41ClearGateOrder/godNightmareRound41ClearGateOrder는 기본값 0이라
    // 클리어 검사 자체가 꺼져 있다(원작 스토리11/12에 대응하는 우리 자산이 확정되지 않아서,
    // 지어낸 매핑으로 전멸시키지 않는다 — PM 지시). 검사가 꺼져 있어도 유닛수 한계(지옥60·
    // 신55·악몽50)는 그대로 적용한다 — F의 그 절반은 스토리 매핑과 무관하게 확정된 값이다.
    bool ApplyRound41DifficultyGate()
    {
        if (DifficultyManager.Instance == null || !DifficultyManager.Instance.IsModeSelected) return false;

        DifficultyMode mode = DifficultyManager.Instance.Current;
        if (mode != DifficultyMode.Hell && mode != DifficultyMode.God && mode != DifficultyMode.Nightmare) return false;

        int gateOrder = mode == DifficultyMode.Hell ? hellRound41ClearGateOrder : godNightmareRound41ClearGateOrder;
        if (gateOrder > 0 && (StoryManager.Instance == null || StoryManager.Instance.FinishedCount < gateOrder))
        {
            Debug.Log($"41라운드 게이트 — {mode.KoreanName()} 모드, 대응 스토리 미클리어(FinishedCount<{gateOrder})로 전멸 처리합니다.");
            foreach (PlayerContext context in PlayerContext.Occupied)
            {
                if (!context.IsDead) HandlePlayerDefeated(context.PlayerId, context);
            }
            return true;
        }

        int newLimit = DifficultyManager.Instance.CurrentData.round41UnitCountLimit;
        if (newLimit > 0)
        {
            enemyCountThreshold = newLimit;
            Debug.Log($"41라운드 — {mode.KoreanName()} 모드 레인당 유닛수 한계를 {newLimit}로 낮췄습니다.");
        }

        return false;
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

    // 아이템 도박(H0BS 메타몽) 재고 — 원작 툴팁 "도박회수는 6/9라운드 스토리 클리어시
    // 1회씩 증가"(2026-09-07, PM 지시). 우리 스토리(Story01~13)는 원작과 번호·내용이
    // 무관한 창작 콘텐츠라 "그 스토리를 깼을 때"에 걸 지점이 없다 — 대신 "그 라운드에
    // 도달했을 때"로 근사한다(라운드는 우리도 있는 축, §18 갱신). 실제 스토리 콘텐츠가
    // 들어오면 그 클리어 시점으로 옮길 것(되돌릴 조건).
    //
    // ⚠️ 증분이 아니라 절대값 세팅이다 — 원작 AddUnitToStockBJ가 증분 함수가 아니라
    // 그 시점의 재고를 그대로 지정하는 함수라, 6라운드=1·9라운드=2로 SetStock한다
    // (ItemGambleState.SetStock 주석 참고, [Item_Int가 세팅되는 변수]는 추정).
    void GrantItemGambleStock(int roundNumber)
    {
        int newStock;
        if (roundNumber == 6) newStock = 1;
        else if (roundNumber == 9) newStock = 2;
        else return;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            context.ItemGambleState?.SetStock(newStock);
        }
    }

    void StartRound(int roundNumber)
    {
        WaveData waveData = GetWaveData(roundNumber);

        roundTimer = ResolveRoundDuration(roundNumber, waveData);

        if (waveData != null && waveData.IsBossRound)
        {
            Debug.Log($"보스 라운드! (라운드 {roundNumber})");

            // 원작 Trig_Enemy_Boss_create/sinsekai: 보스 스폰 시점에 전원 골드 몰수
            // ("보스 전에 다 써라"는 설계). 여기서 부르면 직전 라운드의 클리어 위습·
            // 처치 보상(AdvanceRound가 StartRound보다 먼저 지급)은 이미 들어간 뒤라
            // 순서가 원작과 같다 — 방금 받은 보상을 뺏는 게 아니다.
            RewardDistributor.Instance?.ConfiscateGoldOnBossRoundStart();
        }

        // 원작 Trig_Round_10ver: 10·20·…·60라운드가 시작되면 보스 생성 직전에 보물상자를 숨긴다
        // (보스 처치와 무관). 몇 라운드인지 판정은 TreasureHunt가 한다.
        TreasureHunt.Instance?.OnRoundStarted(roundNumber);

        if (waveSpawner != null && waveData != null)
        {
            waveSpawner.SpawnRound(waveData);
        }
    }

    // 라운드 길이 — 원작 레지스터를 직접 읽어 구간을 나눴다(리서치담당, 2026-09-06,
    // 재정정 포함):
    //   R1        = roundDuration(40.65, 원작 하드코딩)
    //   R2~R39    = normalRoundDuration(40.67)
    //   R40~R60   = shortRoundDuration(38.67, 고정) — Mode_TimerReal=2.00
    //   R61~R75   = finalRoundDuration(36.67, 고정) — Mode_TimerReal +=2가 61라운드
    //               진입 시 딱 한 번만 걸린다(맵 전체에서 그 블록 하나뿐, Level==61
    //               조건). 라운드마다 누적되는 게 아니라 15개 전부 같은 값이다 —
    //               처음엔 "라운드마다 2초씩 감소"로 잘못 읽었다가 정정했다(이전 커밋
    //               2bc6d51의 감소 공식·하한 clamp를 이 커밋이 제거한다).
    // 신세계(≥newWorldStartRound)가 최우선이다 — 원작은 보스 여부와 무관하게 그 구간을
    // 이 규칙 하나로 통일한다(예전부터 있던 설계, 안 바뀜). 그 아래에서만 보스 라운드가
    // 끼어든다(bossRoundDuration).
    float ResolveRoundDuration(int roundNumber, WaveData waveData)
    {
        if (roundNumber >= newWorldStartRound)
            return finalRoundDuration;

        if (waveData != null && waveData.IsBossRound)
            return bossRoundDuration;

        if (roundNumber <= 1)
            return roundDuration;

        if (roundNumber >= shortRoundStartRound)
            return shortRoundDuration;

        return normalRoundDuration;
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
