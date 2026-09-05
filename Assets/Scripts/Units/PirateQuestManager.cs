using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 해적단류 퀘스트(와포루/스모커 등) 진행을 맡는다. UnitSellPortal이 트리거하면 미니보스를
// EnemyDummy로 스폰하고, 제한시간 안에 죽으면 성공 보상을, 못 죽이면 실패 페널티를 준다.
//
// 미니보스는 EnemyDummy를 그대로 재사용한다(SealSpawner와 같은 패턴) — UnitAttacker의 타겟
// 탐색이 EnemyDummy.Active를 레인 구분 없이 거리로 훑어서, 별도 타겟팅 시스템 없이도
// "아무 유닛이나 근처에서 잡으면 된다"가 그대로 성립한다. LaneIndex는 반드시 -1로 둔다
// (크립과 같은 이유 — RoundManager의 레인별 카운트·패배판정에 안 섞이게).
//
// 보상은 EnemyDummy의 킬 보상 파이프라인(EnemyData.rewardsKillerOnly)을 타지 않는다.
// 원작은 "마지막 타격을 넣은 플레이어"가 아니라 "퀘스트를 연 플레이어"(토큰을 판 사람)에게
// 보상을 준다 — war3map.j의 Trig_Quest_waporu_Actions는 판매 시점에 플레이어 ID를 한 번만
// 저장해 두고(s__TrigVariables_Setinteger) 끝까지 그 값을 쓴다. 그래서 미니보스 EnemyData는
// rewardsKillerOnly를 켜지 않고(goldReward=0, resourceRewards 비움), 이 매니저가 "죽었는지"만
// 폴링해서 직접 보상을 지급한다 — 누가 죽였는지는 안 본다.
public class PirateQuestManager : MonoBehaviour
{
    public static PirateQuestManager Instance { get; private set; }

    // 게임 시작 시 각 플레이어에게 무상으로 하나씩 지급하는 퀘스트 토큰 목록.
    // 원작은 건물 "재고"(AddUnitToStockBJ)에 1개 등록하는 방식인데, 그 시스템 자체가
    // 우리에 없어서(설계 승인, 2026-09-05 PM) 게임 시작 무상 지급으로 단순화했다.
    [SerializeField] List<PirateQuestData> startingQuests = new List<PirateQuestData>();
    [SerializeField] UnitSpawner unitSpawner;

    UnitSpawner Spawner => unitSpawner != null ? unitSpawner : unitSpawner = FindFirstObjectByType<UnitSpawner>();

    RoundManager roundManager;
    RoundManager RoundManagerRef => roundManager != null ? roundManager : roundManager = FindFirstObjectByType<RoundManager>();

    public int CurrentRound => RoundManagerRef != null ? RoundManagerRef.CurrentRound : 0;

    // 같은 플레이어가 같은 퀘스트를 동시에 두 번 열면 타이머·미니보스가 겹친다.
    // 원작도 스테이지 머신(Stage 0/1/2) 하나로 직렬화한다 — 이게 그 대체다.
    readonly HashSet<(PirateQuestData quest, int playerId)> active = new HashSet<(PirateQuestData, int)>();

    // 도전 횟수 — 스모커처럼 재도전마다 세지는 미니보스용. 실패해도 카운트는 안 줄어든다
    // (원작 `udg_smokehp`도 누적만 하고 리셋하는 경로가 없었다).
    readonly Dictionary<(PirateQuestData quest, int playerId), int> attemptCounts =
        new Dictionary<(PirateQuestData, int), int>();

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!GameAuthority.IsServer) return;
        GrantStartingTokens();
    }

    // ⚠️ 이 "게임 시작 1회 무상 지급"이 원작의 "상점 재고 구매(재입고됨)"를 단순화한 자리다
    // (클래스 상단 주석 참고). 그 단순화의 대가: 토큰을 다시 얻을 방법이 어디에도 없어서,
    // PirateQuestData.scalesWithAttempts(재도전 시 강해짐)가 구조적으로 미도달이 됐다
    // (2026-09-05, 전 구간 손 추적). 재고 보충 구조를 만들 때 여기부터 손댈 것.
    void GrantStartingTokens()
    {
        if (Spawner == null)
        {
            Debug.LogWarning("PirateQuestManager: UnitSpawner를 찾지 못해 시작 퀘스트 토큰을 지급하지 못했습니다.");
            return;
        }

        foreach (PirateQuestData quest in startingQuests)
        {
            if (quest == null || quest.sellUnit == null) continue;

            foreach (PlayerContext context in PlayerContext.Occupied)
            {
                LaneMarker lane = LaneMarker.Get(context.PlayerId);
                Vector3 position = lane != null ? lane.TakeSpawnPosition(quest.sellUnit) : transform.position;
                Spawner.Spawn(quest.sellUnit, position, context.PlayerId);
            }
        }
    }

    /// <summary>UnitSellPortal이 부른다. 이미 진행 중이면 시작하지 않는다.</summary>
    public bool StartQuest(PirateQuestData quest, int playerId)
    {
        if (quest == null || quest.miniboss == null || quest.miniboss.prefab == null)
        {
            Debug.LogWarning($"PirateQuestManager: {quest?.questName}의 미니보스 설정이 비어있어 시작하지 못했습니다.");
            return false;
        }

        (PirateQuestData, int) key = (quest, playerId);
        if (active.Contains(key))
        {
            Debug.Log($"[해적단] {quest.questName}: 플레이어 {playerId + 1}가 이미 진행 중입니다.");
            return false;
        }

        active.Add(key);
        StartCoroutine(RunQuest(quest, playerId, key));
        return true;
    }

    IEnumerator RunQuest(PirateQuestData quest, int playerId, (PirateQuestData, int) key)
    {
        GameObject miniboss = SpawnMiniboss(quest, playerId);
        if (miniboss == null)
        {
            active.Remove(key);
            yield break;
        }

        Debug.Log($"[해적단] {quest.questName} 발동! 플레이어 {playerId + 1}, {quest.timerSeconds:F0}초 안에 처치하세요.");

        float timer = quest.timerSeconds;
        // miniboss는 UnityEngine.Object 오버로드된 == null을 쓴다 — Destroy() 직후부터
        // (실제 파괴가 처리되는 프레임 끝보다 먼저) true가 되므로, 전투로 죽었는지
        // 시간이 다 됐는지를 이 루프 하나로 가른다.
        while (timer > 0f && miniboss != null)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        bool killed = miniboss == null;
        if (killed)
        {
            HandleSuccess(quest, playerId);
        }
        else
        {
            Destroy(miniboss);
            HandleFailure(quest, playerId);
        }

        active.Remove(key);
    }

    GameObject SpawnMiniboss(PirateQuestData quest, int playerId)
    {
        LaneMarker lane = LaneMarker.Get(playerId);
        Vector3 position = lane != null ? lane.LaneCenter : transform.position;

        GameObject instance = Instantiate(quest.miniboss.prefab, position, Quaternion.identity);

        if (!instance.TryGetComponent(out EnemyDummy dummy))
        {
            Debug.LogWarning($"PirateQuestManager: {quest.miniboss.enemyName} 프리팹에 EnemyDummy가 없어 스폰을 취소했습니다.", this);
            Destroy(instance);
            return null;
        }

        dummy.Initialize(quest.miniboss);
        dummy.SetLane(-1); // 레인 카운트·패배판정에서 제외 (크립과 같은 이유)

        if (quest.scalesWithAttempts)
        {
            int attempt = NextAttempt(quest, playerId);
            float hp = quest.miniboss.hp * (1f + (attempt - 1) * quest.hpIncreasePerAttempt);
            dummy.Initialize(hp); // data는 그대로 두고 hp/MaxHp만 덮어쓴다
        }

        if (instance.TryGetComponent(out NavMeshAgent agent))
        {
            NavPlacement.Place(agent, position);
        }

        if (instance.TryGetComponent(out WaypointMover mover))
        {
            mover.enabled = false; // 미니보스는 순찰하지 않는다 — 제자리에서 맞선다
        }

        return instance;
    }

    int NextAttempt(PirateQuestData quest, int playerId)
    {
        (PirateQuestData, int) key = (quest, playerId);
        attemptCounts.TryGetValue(key, out int count);
        count++;
        attemptCounts[key] = count;
        return count;
    }

    // ⚠️ PlayerContext.Get은 occupied만 보고 IsDead는 안 본다 — 그래서 퀘스트 진행 중에
    // 그 플레이어가 전멸해도(RoundManager.HandlePlayerDefeated) 이 성공 판정이 뒤늦게 나면
    // 죽은 플레이어에게 보상이 그대로 들어간다. 의도적으로 그대로 둔다(PM 판단, 2026-09-05) —
    // 쓸 사람이 없을 뿐 해가 없고, 막으려면 코루틴에 사망 판정을 새로 넣어야 해서 얻는 것보다
    // 비용이 크다.
    void HandleSuccess(PirateQuestData quest, int playerId)
    {
        PlayerContext context = PlayerContext.Get(playerId);
        if (context == null || !context.IsOccupied)
        {
            Debug.LogWarning($"PirateQuestManager: {quest.questName} 성공했지만 플레이어 {playerId + 1}를 찾지 못해 보상을 지급하지 못했습니다.");
            return;
        }

        if (quest.successGold > 0) context.GoldWallet?.Add(quest.successGold);

        if (quest.successResources != null && context.ResourceWallet != null)
        {
            foreach (EnemyResourceReward reward in quest.successResources)
                context.ResourceWallet.Add(reward.type, reward.amount);
        }

        if (quest.successWisp != null && quest.successWispCount > 0 && RewardDistributor.Instance != null)
        {
            RewardDistributor.Instance.GrantWisps(context,
                new List<WispReward> { new WispReward { wisp = quest.successWisp, count = quest.successWispCount } });
        }

        // 특성포인트 4갈래 중 네 번째 — 피카 퀘스트 성공(사장님 확정 2026-09-05, 07번).
        // ⚠️ 이 호출 자체는 맞게 이어졌지만 지금 당장은 도달하지 않는다 — `context.UnitUpgrades`가
        // 씬에서 여전히 null이다(`MapGenerator`가 `UnitUpgrades` 컴포넌트를 `PlayerContext`에
        // 안 붙인다, `WIRING_AUDIT.md` §1, 2026-09-05 재확인). `?.`라 조용히 아무 일도 안 하고
        // 넘어간다 — 예외는 안 나지만 포인트도 안 쌓인다. §1이 풀리면 이 줄은 손 안 대도 된다.
        // 그리고 설령 §1이 풀려도 **쓰는 쪽(트레잇 상점)이 아직 없다**(UnitTraitData.
        // costTraitPoints를 읽어 Unlock()을 부르는 코드가 프로젝트 어디에도 없음) — 포인트는
        // UnitUpgrades.TraitPoints에 쌓이기만 하고 당장 쓸 곳이 없다.
        if (quest.successTraitPoints > 0)
        {
            context.UnitUpgrades?.GrantPirateQuestPoint();
        }

        // 와포루류: 처치 성공 시 스토리 건물/보스에 직접 마법데미지를 얹는다 — 데이터에 값이
        // 있을 때만(PirateQuestData.storyDamage 주석 참고: 와포루는 원작에도 배선이 없어 0이다).
        if (quest.storyDamage > 0f && StoryManager.Instance != null)
        {
            StoryManager.Instance.ApplyQuestDamage(quest.storyDamage, playerId);
        }

        Debug.Log($"[해적단] {quest.questName} 성공! 플레이어 {playerId + 1} 보상 지급.");
    }

    void HandleFailure(PirateQuestData quest, int playerId)
    {
        if (quest.failWispBlockRounds > 0 && RoundManagerRef != null)
        {
            RoundManagerRef.BlockRoundRewardWisp(playerId, quest.failWispBlockRounds);
        }

        Debug.Log($"[해적단] {quest.questName} 실패. 플레이어 {playerId + 1}: 다음 {quest.failWispBlockRounds}라운드 동안 랜덤위습을 받지 못합니다.");
    }
}
