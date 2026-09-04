using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리를 순서대로 내보내고 클리어를 감지한다.
/// 진행은 라운드가 아니라 연쇄다 — 하나를 깨면 다음이 (대기시간이 있으면 그만큼 뒤에) 나온다.
/// </summary>
public class StoryManager : MonoBehaviour
{
    [SerializeField] List<StoryData> stories = new List<StoryData>();
    [SerializeField] Transform spawnPoint;          // 스토리존 한가운데. 비면 이 오브젝트 위치
    [SerializeField] float firstStoryDelay = 10f;   // 게임 시작 후 첫 스토리까지
    [SerializeField] int transformRoundStep = 5;    // 끝자리가 이 배수인 라운드에 건물이 보스로 변신

    public static StoryManager Instance { get; private set; }

    StoryData running;      // 지금 필드에 나와 있는 스토리
    StoryData pending;      // 다음에 나올 스토리
    EnemyDummy activeEnemy;
    bool transformed;       // 건물이 보스로 바뀌었는가
    int spawnedAtRound;
    float pendingTime;
    int finished;

    RoundManager rounds;

    public StoryData Running => running;
    public bool IsWaiting => running == null && pending != null;
    public float SecondsUntilNext => Mathf.Max(0f, pendingTime - Time.time);
    public int FinishedCount => finished;

    /// <summary>
    /// 지금 대기 중인 구간의 이름이 이것과 같은가(예: "백수생활"). 스토리가 진행 중이거나
    /// 아예 대기 중이 아니면 false — 특정 구간에만 열리는 포탈이 이걸로 자기 상태를 묻는다.
    /// </summary>
    public bool IsInterlude(string interludeName)
    {
        return IsWaiting && pending.interludeName == interludeName;
    }

    /// <summary>대기 중이면 그 구간 이름(백수생활 등), 진행 중이면 스토리 이름.</summary>
    public string StatusLabel
    {
        get
        {
            if (running != null) return transformed ? $"{running.storyName} (보스)" : running.storyName;
            if (pending == null) return "";
            return string.IsNullOrEmpty(pending.interludeName) ? pending.storyName : pending.interludeName;
        }
    }

    void OnEnable() => Instance = this;

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsTransformed => transformed;

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
        rounds = FindFirstObjectByType<RoundManager>();
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
                if (!transformed && ShouldTransform()) TransformIntoBoss();
                return;
            }

            Finish(running);
            return;
        }

        if (pending != null && Time.time >= pendingTime)
            Spawn(pending);
    }

    // 끝자리가 0 또는 5인 라운드에 변신한다. 나온 그 라운드에 바로 변신하지는 않는다 —
    // 때릴 틈도 없이 보스가 되면 "미리 깎아둔다"는 구조가 성립하지 않는다.
    bool ShouldTransform()
    {
        if (rounds == null) return false;
        if (rounds.CurrentRound <= spawnedAtRound) return false;

        return rounds.CurrentRound % transformRoundStep == 0;
    }

    void TransformIntoBoss()
    {
        transformed = true;
        activeEnemy.SetInvulnerable(false);

        // 체력은 건드리지 않는다 — 지금까지 깎아둔 만큼이 그대로 보스 체력이 된다.
        EnemyData bossData = running.BossOrBuilding;
        if (bossData != running.building)
        {
            activeEnemy.name = $"스토리보스_{running.storyName}";
            activeEnemy.transform.localScale *= 1.4f;
        }

        Debug.Log($"스토리 변신: {running.storyName} — 남은 체력 {activeEnemy.Hp:F0} / {activeEnemy.MaxHp:F0}");
    }

    void Finish(StoryData story)
    {
        Debug.Log($"스토리 클리어: {story.storyName}");
        running = null;
        activeEnemy = null;
        transformed = false;
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

        pending = stories[index];
        // 대기시간은 다음 스토리가 들고 있다 — 8번을 깬 뒤의 백수생활 5분이 그것이다.
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
        dummy.SetLane(-1);          // 레인 몹이 아니다. 패배 판정(가장 붐비는 레인)에 섞이면 안 된다.
        dummy.SetInvulnerable(true); // 변신 전까지는 죽지 않는다. 피해만 쌓인다.

        // 건물은 제자리를 지킨다 — 레인 몹처럼 경로를 돌지 않는다.
        if (instance.TryGetComponent(out WaypointMover mover)) mover.enabled = false;

        running = story;
        activeEnemy = dummy;
        transformed = false;
        spawnedAtRound = rounds != null ? rounds.CurrentRound : 0;

        Debug.Log($"스토리 건물 등장: {story.storyName} (체력 {dummy.MaxHp:F0}) — " +
                  $"끝자리 {transformRoundStep} 라운드에 보스로 변신");
    }
}
