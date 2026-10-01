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

    // 맵 초기배치 특수 유닛(2026-09-06, PM 지시) — 원작 CreateBuildingsForPlayerN의
    // CreateUnit 직접배치 대응. 우리 관례는 "맵에 오브젝트로 놓기"가 아니라 "게임 시작
    // 지급"이라 startingWisp와 같은 자리에 둔다. 지금은 h05X(레일리, "판매-특수") 하나뿐 —
    // 원작에서 이런 유닛이 몇 종인지 안 나와서 목록으로 안 만들었다(근거 없이 "나중에
    // 늘 것 같다"로 목록화하지 말 것, PM 지시). 늘어나면 그때 목록으로 바꾼다. 비어있으면
    // (기본값 null) 아무 일도 안 한다 — 기존 씬 무영향.
    [SerializeField] UnitData startingSpecialUnit;

    // 05번 「고대의 배」 지급 경로 ㉡(스토리, ANCIENT_SHIP_AND_BUFF_SOURCES.md) — 원작
    // Trig_Story_reward7의 h05Y 지급 대상. 우리 스토리 번호는 원작과 별개라 이름으로는
    // 못 찾지만, 그 트리거 자체가 "완료한 스토리 개수(udg_Story_Count)==7"이라는 순서
    // 조건이라 우리 쪽도 StoryData.order==7(Story07_메가스터디, MapGenerator가 order로
    // 정렬해 배선한다)이 정확히 같은 자리다 — 확인 완료(구현담당3).
    [SerializeField] UnitData ancientShipUnit;

    // 항법 "연합세력"(NavigationChoice.Union) 전용 위습 — 원작 e0IX. Wisp_흔함.asset이
    // 그 자리다(2026-09-07, APPROXIMATION_LEDGER.md §① 설계 확정 후 배선). 비어 있으면
    // (기본값 null) GrantUnionWispIfEligible이 아무 일도 안 한다.
    [SerializeField] WispData unionWisp;
    // 41R 2차 세이브 보상(원작 Trig_SaveReward_2, j:5364-5372) — 흔함선택 위습(e018)·랜덤위습(e0IX). MapGenerator.RepairSaveRewardWisps가 채운다.
    [SerializeField] WispData saveRewardCommonChoiceWisp;
    [SerializeField] WispData saveRewardRandomWisp;

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
        GrantStartingSpecialUnits();
        GrantStartingSpecialGradeUnit();
        GrantStartingTraitPoints();
        GrantSaveThresholdRewards();
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

    // 맵 초기배치 특수 유닛 지급 — GrantAncientShip(아래)과 같은 패턴(창고 근처 스폰)을
    // 게임 시작 시 전원에게 적용한다. startingSpecialUnit이 비어 있으면(기본값) 아무
    // 일도 안 한다 — 회귀 없음.
    void GrantStartingSpecialUnits()
    {
        if (!GameAuthority.IsServer) return;
        if (startingSpecialUnit == null) return;

        UnitSpawner spawner = SpawnerRef;
        if (spawner == null)
        {
            Debug.LogWarning("RewardDistributor: UnitSpawner를 찾지 못해 시작 특수 유닛을 지급하지 못했습니다.", this);
            return;
        }

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            Vector3 position = context.Warehouse != null ? context.Warehouse.transform.position : context.transform.position;
            spawner.Spawn(startingSpecialUnit, position, context.PlayerId);
        }
    }

    // 시작 특별함 1기(2026-09-29 유저 피드백 「원랜디처럼 특별함 하나 주면 진입장벽이 낮아진다 — 상위 조합에도,
    // 초반 버티기에도」). 원작 근거 없는 우리 결정이다. 포탈과 같은 뽑기표(MainGachaTable)의 특별함 풀에서
    // 한 기를 뽑아 포탈이 소환하는 자리(흔함 아닌 유닛 = 레인 가운데 고리)에 세운다.
    void GrantStartingSpecialGradeUnit()
    {
        if (!GameAuthority.IsServer) return;

        UnitSpawner spawner = SpawnerRef;
        GachaTable table = null;
        foreach (UnitPortal portal in FindObjectsByType<UnitPortal>(FindObjectsSortMode.None))
            if (portal.Table != null && portal.Table.entries != null
                && portal.Table.entries.Exists(e => e != null && e.grade == UnitGrade.Special && e.pool != null && e.pool.Count > 0))
            { table = portal.Table; break; }
        if (spawner == null || table == null)
        {
            Debug.LogWarning("RewardDistributor: UnitSpawner나 뽑기표를 찾지 못해 시작 특별함을 지급하지 못했습니다.", this);
            return;
        }

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            UnitData unit = table.RollFromGrade(UnitGrade.Special);
            if (unit == null || unit.prefab == null) continue;
            LaneMarker lane = LaneMarker.Get(context.PlayerId);
            Vector3 position = lane != null ? lane.TakeSpawnPosition(unit)
                             : context.Warehouse != null ? context.Warehouse.transform.position : context.transform.position;
            spawner.Spawn(unit, position, context.PlayerId);
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

    // 11번(영속 저장) — war3map.j Trig_SaveReward_1 대응. 게임 시작 시 한 번, 로드된 누적
    // 세이브 포인트 문턱(10/100/300/600/900)에 걸린 만큼 골드·목재·특성포인트를 준다.
    // PersistentSave.Awake가 이미 파일을 읽어둔 뒤라(컴포넌트 실행 순서상 Start가 더 늦다)
    // 여기서 바로 판정해도 된다.
    void GrantSaveThresholdRewards()
    {
        if (!GameAuthority.IsServer) return;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            context.PersistentSave?.ApplyLoadThresholdRewards(
                context.GoldWallet, context.ResourceWallet, context.UnitUpgrades);
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
    // deathPosition: 죽은 적 자리(처치 골드 「+N」 글자 위치, 원작 CreateTextTagUnitBJ). 없으면 글자만 생략한다.
    public void GrantKillReward(EnemyData data, int laneIndex, int round, int killerPlayerId = -1, Vector3? deathPosition = null)
    {
        if (!GameAuthority.IsServer) return;
        if (data == null) return;

        AnnounceKill(data, killerPlayerId);

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

        GrantKillGold(owner, round, deathPosition);
        GrantResources(owner, data);

        if (data.isBoss) GrantBossReward(owner, round, data);
    }

    // 원작 문구의 보상 부분 — 「N골드와 나무 N개」(우리 데이터 그대로).
    static string RewardPhrase(int gold, List<EnemyResourceReward> resources)
    {
        var parts = new List<string>();
        if (gold > 0) parts.Add($"<color=#FFD700>{gold}골드</color>");
        if (resources != null)
            foreach (EnemyResourceReward r in resources)
                if (r != null && r.amount > 0) parts.Add(r.type == ResourceType.Wood ? $"<color=#20B2AA>나무 {r.amount}개</color>" : $"{r.type} {r.amount}");
        return string.Join("<color=#FF8200>와</color> ", parts);
    }

    void AnnounceKill(EnemyData data, int killerPlayerId)
    {
        if (string.IsNullOrEmpty(data.killAnnounceLabel)) return;
        string reward = RewardPhrase(data.goldReward, data.resourceRewards);
        string label = $"<color=#FF0000>{data.killAnnounceLabel}</color>";
        string particle = RoundManager.ObjectParticle(data.killAnnounceLabel);
        if (data.rewardsAllPlayers)
        {
            string line = $"<color=#FF8200>{PlayerDisplayName.Of(Mathf.Max(0, killerPlayerId))}</color> <color=#FF8200>님이</color> {label}<color=#FF8200>{particle} 사냥하여 모든플레이어에게</color> {reward} <color=#FF8200>를 지급합니다.</color>";
            foreach (PlayerContext context in PlayerContext.Occupied) PlayerNotification.Show(context.PlayerId, line, 10f);
        }
        else if (killerPlayerId >= 0)
            PlayerNotification.Show(killerPlayerId, $"{label}<color=#FF8200>{particle} 사냥하여</color> {reward} <color=#FF8200>를 지급합니다.</color>", 10f);
    }

    // 원작 스토리 완료 공지(j:13499~13632, 스토리마다 한 줄, 전원 10초) — 틀 「{스토리}까지의 스토리 진행완료 모든플레이어에게 … 를 지급합니다.」.
    //    보상 부분은 원작 문장을 옮기지 않고 **우리 StoryData 보상 그대로** 적는다(원작 값과 다르면 거짓말이 된다). 알림 묶음 8/13.
    void AnnounceStoryReward(StoryData story)
    {
        var parts = new List<string>();
        string resources = RewardPhrase(story.goldReward, story.resourceRewards);
        if (resources.Length > 0) parts.Add(resources);
        if (story.wispRewards != null)
            foreach (WispReward w in story.wispRewards)
                if (w != null && w.wisp != null && w.count > 0) parts.Add($"<color=#FF8200>{w.wisp.wispName} {w.count}기</color>");
        string rewardText = parts.Count > 0 ? string.Join(" ", parts) + " <color=#FF8200>를 지급합니다.</color>" : "";
        string line = $"<color=#FF0000>{story.storyName}</color><color=#FF8200>까지의 스토리 진행완료 모든플레이어에게</color> {rewardText}";
        foreach (PlayerContext context in PlayerContext.Occupied) PlayerNotification.Show(context.PlayerId, line, 10f);
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
        if (data.bonusRewardUnit != null && Random.value < data.bonusRewardChance)
        {
            // 원작 크립 2단계 50%: 목재 2 + 해적선(기본 목재 7 대신).
            if (data.bonusRewardWood > 0) killer.ResourceWallet?.Add(ResourceType.Wood, data.bonusRewardWood);
            SpawnUnitAtWarehouse(killer, data.bonusRewardUnit);
            if (!string.IsNullOrEmpty(data.bonusRewardMessage)) PlayerNotification.Show(killerPlayerId, data.bonusRewardMessage, 10f);
        }
        else
        {
            GrantResources(killer, data);
            if (data.bonusRewardUnit != null && !string.IsNullOrEmpty(data.baseRewardMessage)) PlayerNotification.Show(killerPlayerId, data.baseRewardMessage, 10f);
        }
        if (data.savePointReward > 0) killer.PersistentSave?.AddSessionPoints(data.savePointReward);
        if (data.isBoss) GrantBossReward(killer, round, data);
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

        if (data.savePointReward > 0) context.PersistentSave?.AddSessionPoints(data.savePointReward);
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

    void GrantKillGold(PlayerContext context, int round, Vector3? deathPosition)
    {
        if (context.GoldWallet == null) return;
        if (Random.value >= KillGoldChance) return;

        // 원작: R2I( Gold_Math × (2 + Gold_Plus) ) — 곱한 결과를 한 번만 버린다.
        // Gold_Plus가 소수라 곱셈 전에 버리면(예전엔 (int)로 암묵 변환) 소수점 이하가
        // 사라져 최종 배수가 과소해진다. Gold_Math 자체는 원작에서도 정수 나눗셈이 맞다.
        int goldMath = ComputeGoldMath(round);
        float goldPlus = context.GoldWallet.GoldPlus;
        int gold = Mathf.FloorToInt(goldMath * (2f + goldPlus));
        context.GoldWallet.Add(gold);
        // 원작 j:3374-3375 — 골드를 받은 플레이어에게만 죽은 자리에 금색 「+N」이 1.5초 떠오른다.
        if (deathPosition.HasValue) KillGoldPopup.Show(context.PlayerId, deathPosition.Value, gold);
    }

    // 원작 udg_PlayerDeath[i]=1 분기의 SetPlayerStateBJ(플레이어, GOLD, 0) — 탈락한
    // 플레이어의 골드를 몰수한다. 호출부는 RoundManager.HandlePlayerDefeated.
    // ⚠️ 2026-09-25: 원작에서 골드를 0으로 만드는 곳은 이 패배 분기뿐이다. 「보스 라운드
    // 시작에 전원 몰수」(ConfiscateGoldOnBossRoundStart)는 이 분기를 잘못 읽은 것이라 지웠다.
    public void ConfiscateGoldOnPlayerDefeated(PlayerContext context)
    {
        if (!GameAuthority.IsServer) return;
        if (context == null) return;

        context.GoldWallet?.ZeroOut();
    }

    void GrantBossReward(PlayerContext context, int round, EnemyData boss)
    {
        if (!BossRewardByRound.TryGetValue(round, out (int gold, int wood) reward)) return;

        context.GoldWallet?.Add(reward.gold);
        context.ResourceWallet?.Add(ResourceType.Wood, reward.wood);

        // 원작 Trig_BossReward(j:13456~13480, 레인 주인에게만): 「{보스}  처치!」(7초) + 「N골드 + 나무 N개 를 획득!」(10초) — 알림 묶음 11/13, GAP 102.
        //    패왕의길 「항법효과: + 나무 1개 를 추가로 획득!」은 그 보상 자체가 아직 없어(GAP 59) 띄우지 않는다.
        string name = boss != null && !string.IsNullOrEmpty(boss.enemyName) ? boss.enemyName : "보스";
        PlayerNotification.Show(context.PlayerId, $"{name}  <color=#FF8200>처치!</color>", 7f);
        PlayerNotification.Show(context.PlayerId, $"<color=#FFD700>{reward.gold}골드</color> + <color=#20B2AA>나무 {reward.wood}개</color> <color=#FF8200>를 획득!</color>", 10f);
    }

    // 스토리 클리어 보상: 전체 플레이어에게 골드 + 자원 + 위습 지급.
    // 보스 최다 데미지·도전과제 보상은 기여도 추적 시스템이 없어 아직 만들지 않았다(별도 작업).
    public void GrantStoryReward(StoryData storyReward)
    {
        if (!GameAuthority.IsServer) return;
        if (storyReward == null) return;
        AnnounceStoryReward(storyReward);

        foreach (PlayerContext context in PlayerContext.All)
        {
            // 처치 보상과 같은 이유로 빈 슬롯은 건너뛴다 — 아무도 없는 자리에 자원이 쌓인다.
            if (context == null || !context.IsOccupied) continue;

            // 스토리 7(임펠다운 대응, Story07_메가스터디) 전용 — 원작 Trig_Story_reward7이
            // udg_PlayerDeath[플레이어]==0(그 플레이어가 아직 패배 판정을 안 받음)을
            // 개별로 검사한다. 다른 스토리엔 이 조건이 없다(원작에 없음) — order==7일
            // 때만 건다. PlayerContext.IsDead가 그 자리다(HandlePlayerDefeated 주석
            // "원작 udg_PlayerDeath[i]=1 분기의 같은 SetPlayerStateBJ" 참고).
            // 🔴 정정(09-27, j 전수 확인): Trig_Story_reward1~13의 Func002Func001C가 **전부** udg_PlayerDeath[플레이어]==0이다 — 7번만이 아니다.
            //    (옛 주석 「다른 스토리엔 이 조건이 없다」는 틀렸다.) 죽은 플레이어는 스토리 보상을 못 받는다.
            if (context.IsDead) continue;

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

            // 특성포인트 4갈래 중 세 번째 — 스토리 12(코드잇) 클리어 1개.
            // ⚠️ 정정(2026-09-05, 사장님 확정 07번): "원작대로 스토리 12로 옮기고 피카 퀘스트도
            // 준다 — 총 4개"라고 명시적으로 확정됐다. 이전엔 2026-09-03 "8라운드"라는 원문을
            // "스토리 깨면"이라는 질문 문맥에 맞춰 스토리 8(사이버넷)로 해석했던 잠정값이었다 —
            // 그 해석이 틀렸던 것으로 확인됐다. 네 번째 갈래(피카 퀘스트)는
            // PirateQuestManager.HandleSuccess를 참고.
            if (storyReward.order == 12)
            {
                context.UnitUpgrades?.GrantStoryPoint();
            }

            // 05번 「고대의 배」 지급 경로 ㉡ — 스토리 7(임펠다운 대응) 전용, h05Y 1기.
            // 위 IsDead 게이트를 이미 통과한 플레이어만 여기 온다.
            if (storyReward.order == 7)
            {
                GrantAncientShip(context);
            }
        }
    }

    UnitSpawner cachedUnitSpawner;
    UnitSpawner SpawnerRef => cachedUnitSpawner != null
        ? cachedUnitSpawner
        : cachedUnitSpawner = FindFirstObjectByType<UnitSpawner>();

    // 05번 ㉡ — CreateNUnitsAtLoc(1,'h05Y', 소유자) 대응. 위습과 달리 등급 칸이 없는 실제
    // 유닛이라 창고(그 플레이어 소유물이 모이는 자리) 근처에 놓는다 — 창고가 아직 없으면
    // (극히 초반) 플레이어 위치로 물러난다(SpawnWisp의 fallback과 같은 이유).
    void GrantAncientShip(PlayerContext context)
    {
        if (ancientShipUnit == null) return; // 콘텐츠 결손이 아니라 배선 누락 — 조용히 넘기지 않는다.
        SpawnUnitAtWarehouse(context, ancientShipUnit);
    }

    // 창고 근처에 유닛을 세운다(고대의 배·크립 2단계 해적선 공용).
    void SpawnUnitAtWarehouse(PlayerContext context, UnitData unit)
    {
        if (unit == null || context == null) return;
        UnitSpawner spawner = SpawnerRef;
        if (spawner == null)
        {
            Debug.LogWarning($"RewardDistributor: UnitSpawner를 찾지 못해 {unit.unitName}을(를) 지급하지 못했습니다.", this);
            return;
        }

        Vector3 position = context.Warehouse != null ? context.Warehouse.transform.position : context.transform.position;
        spawner.Spawn(unit, position, context.PlayerId);
    }

    public void GrantWisps(PlayerContext context, List<WispReward> wispRewards)
    {
        // MP: 위습(실물)은 호스트만 만든다 — 클라에서 불리면 호스트가 모르는 위습이 생긴다(09-26 클라 G키 사고).
        //     지금 호출부는 전부 가드돼 있지만 여기가 지급 관문이라 한 번 더 막는다. 싱글·호스트는 지나친다.
        if (!GameAuthority.IsServer) return;

        if (context == null || wispRewards == null) return;

        foreach (WispReward reward in wispRewards)
        {
            if (reward == null || reward.wisp == null) continue;
            SpawnWisp(context, reward.wisp, Mathf.Max(1, reward.count));
        }
    }

    // 2026-09-07 연결 완료(PM 지시, "필드만·아직 없다" 뼈대 구멍 점검) — 원작
    // Trig_UnitJohabCounter_Actions 재현. 원문 확정(직접 대조):
    //   TriggerRegisterEnterRectSimple(전체 맵) + 조건 IsUnitType(유닛, UNIT_TYPE_GIANT)
    //   → "Giant 타입 유닛이 맵에 등장할 때마다"(획득 경로 불문 — 조합·가챠·그 밖 전부
    //   포함, 트리거 자체가 방법을 안 가린다) 발동. 액션: udg_Tech_union[플레이어]==true
    //   AND GetUnitPointValue(유닛)>100이면 e0IX 1기 지급.
    // 원작 로스터 원본 유닛 전수 확인 결과 h001~ 등 222종이 전부 "undead,giant"라
    // (war3map_new.w3u, utyp 필드) 우리 로스터 240종 전체가 이 GIANT 분류에 대응한다 —
    // 그래서 훅을 딱 하나(UnitSpawner.Spawn, "플레이어 유닛을 만드는 곳이 여기뿐" 주석
    // 참고)에 걸면 조합·가챠 구분 없이 원작과 같은 범위를 덮는다.
    // 포인트값>100은 UnitGrade.Tier()>=Superior.Tier()로 근사한다(원작 유닛 개별
    // 매핑 불가라 등급으로 근사 — APPROXIMATION_LEDGER.md §① 확정, 이미 설계돼 있던
    // 자리를 그대로 썼다).
    public void GrantUnionWispIfEligible(UnitData data, int ownerId)
    {
        if (data == null || unionWisp == null) return;
        if (data.grade.Tier() < UnitGrade.Superior.Tier()) return;

        PlayerContext context = PlayerContext.Get(ownerId);
        if (context == null || !context.IsOccupied) return;
        if (context.NavigationState == null || context.NavigationState.Choice != NavigationChoice.Union) return;

        GrantWisps(context, new List<WispReward> { new WispReward { wisp = unionWisp, count = 1 } });
    }

    /// <summary>
    /// 41라운드 진입 때 1회(원작 Trig_SaveReward_2, 호출 j:5687 udg_Level==41, 한 번 돌면 트리거 삭제): 플레이어 1~4 **전원**(생존 검사 없음)에게 각자 자기 세이브값으로 —
    /// 클리어 횟수 ≥30 → 흔함선택 위습 1 · 누적 포인트 ≥1000 → 랜덤위습 1 · ≥1500 → 랜덤위습 1 더(합 2). 문구는 전원 10초.
    /// </summary>
    public void GrantSecondSaveRewards()
    {
        if (!GameAuthority.IsServer) return;
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            PlayerSaveData data = context.PersistentSave != null ? context.PersistentSave.Data : null;
            if (data == null) continue;
            var rewards = new List<WispReward>();
            if (data.cumulativeClearCount >= 30 && saveRewardCommonChoiceWisp != null)
                rewards.Add(new WispReward { wisp = saveRewardCommonChoiceWisp, count = 1 });
            int randoms = (data.cumulativePlayPoint >= 1000 ? 1 : 0) + (data.cumulativePlayPoint >= 1500 ? 1 : 0);
            if (randoms > 0 && saveRewardRandomWisp != null)
                rewards.Add(new WispReward { wisp = saveRewardRandomWisp, count = randoms });
            if (rewards.Count > 0) GrantWisps(context, rewards);
        }
        foreach (PlayerContext context in PlayerContext.Occupied)
            PlayerNotification.Show(context.PlayerId, "세이브회수에 따른 2차 보상이 지급됩니다.", 10f);
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
            wisp.ApplyOwnerColor(context.PlayerId);
        }
    }
}
