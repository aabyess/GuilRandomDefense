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

    public static StoryManager Instance { get; private set; }

    StoryData running;      // 지금 필드에 나와 있는 스토리
    StoryData pending;      // 다음에 나올 스토리
    EnemyDummy activeEnemy;
    float pendingTime;
    int finished;

    public StoryData Running => running;
    public bool IsWaiting => running == null && pending != null;
    public float SecondsUntilNext => Mathf.Max(0f, pendingTime - Time.time);
    public int FinishedCount => finished;

    /// <summary>대기 중이면 그 구간 이름(백수생활 등), 진행 중이면 스토리 이름.</summary>
    public string StatusLabel
    {
        get
        {
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
            if (activeEnemy != null) return;

            Finish(running);
            return;
        }

        if (pending != null && Time.time >= pendingTime)
            Spawn(pending);
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

        pending = stories[index];
        // 대기시간은 다음 스토리가 들고 있다 — 8번을 깬 뒤의 백수생활 5분이 그것이다.
        pendingTime = Time.time + extraDelay + pending.delayAfterPreviousSeconds;
    }

    void Spawn(StoryData story)
    {
        int index = stories.IndexOf(story);
        pending = null;

        // 적이 안 정해진 스토리를 조용히 클리어 처리하면, 뒤 스토리 보상까지 한꺼번에 나가버린다.
        if (!story.IsPlayable || story.enemy.prefab == null)
        {
            Debug.LogWarning($"StoryManager: '{story.storyName}'에 적이 없어 건너뜁니다.", this);
            Queue(index + 1);
            return;
        }

        Vector3 at = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject instance = Instantiate(story.enemy.prefab, at, Quaternion.identity);

        if (!instance.TryGetComponent(out EnemyDummy dummy))
        {
            Debug.LogWarning($"StoryManager: {story.enemy.name} 프리팹에 EnemyDummy가 없습니다.", this);
            Destroy(instance);
            Queue(index + 1);
            return;
        }

        dummy.Initialize(story.enemy);
        dummy.SetLane(-1);   // 레인 몹이 아니다. 패배 판정(가장 붐비는 레인)에 섞이면 안 된다.

        // 스토리 적은 제자리를 지킨다 — 레인 몹처럼 경로를 돌지 않는다.
        if (instance.TryGetComponent(out WaypointMover mover)) mover.enabled = false;

        running = story;
        activeEnemy = dummy;
        Debug.Log($"스토리 등장: {story.storyName}");
    }
}
