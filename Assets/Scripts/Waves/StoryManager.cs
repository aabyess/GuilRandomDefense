using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리를 순서대로 내보내고 클리어를 감지한다.
/// 진행은 라운드가 아니라 연쇄다 — 하나를 깨면 다음이 (대기시간이 있으면 그만큼 뒤에) 나온다.
/// 2026-09-25 원작화(사장님 「스토리는 원작대로」): 원작 Story_count1~12는 다음 섬을
/// **소환 즉시 잡을 수 있게** 세운다(nfgo, 무적 없음). 예전의 「무적 건물 → 끝자리 0/5
/// 라운드에 보스로 변신」(09-01 dc685e14)은 원작에 없어 걷어냈다. 대기는 8번 뒤 60초
/// (war3map.j 85736), 13번은 신·악몽 전용으로 12번 뒤 275초(85745/85750)·제한 285초·
/// 실패 시 전원 패배(86022).
/// </summary>
public class StoryManager : MonoBehaviour
{
    [SerializeField] List<StoryData> stories = new List<StoryData>();
    [SerializeField] Transform spawnPoint;          // 스토리존 한가운데. 비면 이 오브젝트 위치
    [SerializeField] float firstStoryDelay = 10f;   // 게임 시작 후 첫 스토리까지

    public static StoryManager Instance { get; private set; }

    StoryData running;      // 지금 필드에 나와 있는 스토리
    StoryData pending;      // 다음에 나올 스토리
    EnemyDummy activeEnemy;
    float spawnedAt;        // 제한시간 기준(Time.time)
    float pendingTime;
    int finished;

    public StoryData Running => running;
    public bool IsWaiting => replica ? replicaWaiting : running == null && pending != null; // MP: 클라는 호스트 값
    public float SecondsUntilNext => replica ? replicaSeconds : Mathf.Max(0f, pendingTime - Time.time); // MP

    // MP: 멀티 클라의 StoryManager는 멈춰 있다(Update가 IsServer 가드) — 호스트가 NetGameState로 보낸 표시 값을
    //     여기 적는다. HUD 스토리 칸·막간 문(InterludeGate)이 읽는 네 값만 덮는다. 싱글·호스트는 replica=false라 무동작.
    bool replica;
    bool replicaRunning;
    bool replicaWaiting;
    float replicaSeconds;
    string replicaLabel = "";
    string replicaInterlude = "";
    int replicaFinished;

    /// <summary>MP: 멀티 클라가 호스트의 「끝낸 스토리 수」를 적는다(도박소 고급 유닛 생성의 스토리 5 조건 표시).</summary>
    public void ApplyReplicatedFinished(int count) { replica = true; replicaFinished = count; }

    /// <summary>MP: 지금 진행 중인 스토리가 있는가(HUD). 클라는 호스트 값.</summary>
    public bool HasRunningStory => replica ? replicaRunning : running != null;

    /// <summary>MP: 대기 중인 막간 이름(백수생활 등), 없으면 빈 문자열 — 호스트가 NetGameState에 싣는다.</summary>
    public string CurrentInterludeName => IsWaiting && pending != null && !string.IsNullOrEmpty(pending.interludeName) ? pending.interludeName : "";

    /// <summary>MP: 멀티 클라가 호스트의 스토리 표시 값을 적는다.</summary>
    public void ApplyReplicated(bool isRunning, bool isWaiting, string label, float seconds, string interlude)
    {
        replica = true;
        replicaRunning = isRunning;
        replicaWaiting = isWaiting;
        replicaLabel = label ?? "";
        replicaSeconds = seconds;
        replicaInterlude = interlude ?? "";
    }
    public int FinishedCount => replica ? replicaFinished : finished; // MP: 클라는 호스트 값(도박소 「스토리 N/5」 조건)

    /// <summary>
    /// 지금 대기 중인 구간의 이름이 이것과 같은가(예: "백수생활"). 스토리가 진행 중이거나
    /// 아예 대기 중이 아니면 false — 특정 구간에만 열리는 포탈이 이걸로 자기 상태를 묻는다.
    /// </summary>
    public bool IsInterlude(string interludeName)
    {
        if (replica) return replicaWaiting && replicaInterlude == interludeName; // MP
        return IsWaiting && pending.interludeName == interludeName;
    }

    /// <summary>대기 중이면 그 구간 이름(백수생활 등), 진행 중이면 스토리 이름.</summary>
    public string StatusLabel
    {
        get
        {
            if (replica) return replicaLabel; // MP
            if (running != null) return running.storyName;
            if (pending == null) return "";
            return string.IsNullOrEmpty(pending.interludeName) ? pending.storyName : pending.interludeName;
        }
    }

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>제한시간이 있는 스토리가 나와 있으면 남은 초, 아니면 -1.</summary>
    public float SecondsLeftInLimit =>
        running != null && running.timeLimitSeconds > 0f ? Mathf.Max(0f, spawnedAt + running.timeLimitSeconds - Time.time) : -1f;

    /// <summary>
    /// 해적단류 퀘스트가 성공 시 스토리에 얹는 보너스 피해(데이터 구동 — PirateQuestData.storyDamage가
    /// 0이면 아무 일도 안 한다. 와포루는 원작 트리거에 실제 배선이 없어 지금 0이다, 해당 필드 주석
    /// 참고). "마법데미지"라 방어력을 무시하고(DamageType.AP) 스킬 배율표 행을 탄다(AttackType.Spells
    /// — 평타 행인 Magic과는 다른 행이다, EnemyDummy.MitigatedDamage 참고). 방어력을 받는 기존
    /// 경로(EnemyDummy.TakeDamage)를 그대로 재사용한다 — 무적 상태(변신 전)면 1까지만 깎이고
    /// 살아남는 것도 레인 몹과 같은 규칙이다.
    /// </summary>
    public void ApplyQuestDamage(float amount, int killerPlayerId)
    {
        if (!GameAuthority.IsServer) return;
        if (amount <= 0f || activeEnemy == null) return;

        activeEnemy.TakeDamage(amount, DamageType.AP, AttackType.Spells, killerPlayerId);
    }

    void Start()
    {
        Queue(0, firstStoryDelay);
    }

    void Update()
    {
        if (!GameAuthority.IsServer) return;

        if (running != null)
        {
            // EnemyDummy는 죽을 때 GameObject를 파괴한다. 파괴된 참조는 == null 로 판정된다.
            if (activeEnemy != null)
            {
                if (running.timeLimitSeconds > 0f && Time.time >= spawnedAt + running.timeLimitSeconds)
                    FailTimeLimit(running);
                return;
            }

            Finish(running);
            return;
        }

        if (pending != null && Time.time >= pendingTime)
            Spawn(pending);
    }

    // 원작 13번(와노쿠니): 제한시간 안에 못 깨면 CustomDefeat — 전원 패배(war3map.j 86022).
    void FailTimeLimit(StoryData story)
    {
        Debug.Log($"스토리 제한시간 초과: {story.storyName} — 전원 패배");
        if (activeEnemy != null) activeEnemy.RemoveInstantly();
        running = null;
        activeEnemy = null;
        pending = null;
        FindFirstObjectByType<RoundManager>()?.DefeatAllPlayers($"스토리 {story.storyName} 제한시간 초과");
    }

    void Finish(StoryData story)
    {
        Debug.Log($"스토리 클리어: {story.storyName}");
        running = null;
        activeEnemy = null;
        finished++;

        if (RewardDistributor.Instance != null)
            RewardDistributor.Instance.GrantStoryReward(story);
        else
            Debug.LogWarning("StoryManager: RewardDistributor가 없어 스토리 보상을 지급하지 못했습니다.", this);

        Queue(stories.IndexOf(story) + 1);
    }

    void Queue(int index, float extraDelay = 0f)
    {
        if (index < 0 || index >= stories.Count)
        {
            Debug.Log("스토리를 모두 클리어했습니다.");
            pending = null;
            return;
        }

        // 신·악몽 전용(원작 와노쿠니)은 다른 난이도에선 없는 스토리다 — 건너뛴다.
        if (stories[index] != null && stories[index].godNightmareOnly && !IsGodOrNightmare())
        {
            Queue(index + 1, extraDelay);
            return;
        }

        pending = stories[index];
        // 대기시간은 다음 스토리가 들고 있다 — 8번 뒤 60초, 12번 뒤 275초(신·악몽)가 그것이다.
        pendingTime = Time.time + extraDelay + pending.delayAfterPreviousSeconds;
    }

    void Spawn(StoryData story)
    {
        int index = stories.IndexOf(story);
        pending = null;

        // 건물이 안 정해진 스토리를 조용히 클리어 처리하면, 뒤 스토리 보상까지 한꺼번에 나가버린다.
        if (!story.IsPlayable || story.building.prefab == null)
        {
            Debug.LogWarning($"StoryManager: '{story.storyName}'에 적이 없어 건너뜁니다.", this);
            Queue(index + 1);
            return;
        }

        Vector3 at = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject instance = Instantiate(story.building.prefab, at, Quaternion.identity);

        if (!instance.TryGetComponent(out EnemyDummy dummy))
        {
            Debug.LogWarning($"StoryManager: {story.building.name} 프리팹에 EnemyDummy가 없습니다.", this);
            Destroy(instance);
            Queue(index + 1);
            return;
        }

        dummy.Initialize(story.building);
        dummy.MarkStoryHpTarget(); // MP: 원작 R01G(퇴장 시 Player(5) 계열 최대 체력 감소) 대상
        dummy.SetLane(-1);          // 레인 몹이 아니다. 패배 판정(가장 붐비는 레인)에 섞이면 안 된다.
        // 원작: 나오자마자 잡을 수 있다(무적 없음, 변신 없음).

        // 건물은 제자리를 지킨다 — 레인 몹처럼 경로를 돌지 않는다.
        if (instance.TryGetComponent(out WaypointMover mover)) mover.enabled = false;

        running = story;
        activeEnemy = dummy;
        spawnedAt = Time.time;

        Debug.Log($"스토리 등장: {story.storyName} (체력 {dummy.MaxHp:F0})" +
                  (story.timeLimitSeconds > 0f ? $" — 제한 {story.timeLimitSeconds:F0}초, 못 깨면 전원 패배" : ""));
    }

    static bool IsGodOrNightmare()
    {
        DifficultyManager difficulty = DifficultyManager.Instance;
        if (difficulty == null || !difficulty.IsModeSelected) return false;
        return difficulty.Current == DifficultyMode.God || difficulty.Current == DifficultyMode.Nightmare;
    }
}
