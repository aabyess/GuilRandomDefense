using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

    EnemyData data;
    bool isDead;

    public static readonly List<EnemyDummy> Active = new List<EnemyDummy>();

    // 라운드 보스(EnemyData.isBoss)가 죽으면 몇 라운드였는지를 실어 알린다. 보스가 여러
    // 라운드에 걸쳐 여럿이라(현재 9종 예정) "보스가 죽었다"만으로는 부족해서 라운드 번호를 싣는다.
    // TakeDamage의 사망 처리 흐름 안에서는 이 신호만 보내고, 실제 처리(도박소 해금 등)는
    // 구독하는 쪽이 한다 — 사망 처리는 가벼워야 한다.
    public static event System.Action<int> OnBossKilled;

    static RoundManager roundManagerCache;
    static RoundManager RoundManagerRef => roundManagerCache != null
        ? roundManagerCache
        : roundManagerCache = FindFirstObjectByType<RoundManager>();

    public int SpawnRound { get; private set; }

    public float Hp => hp;
    public float MaxHp { get; private set; }
    public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(hp / MaxHp) : 0f;

    // 어느 레인에 스폰됐는지. 팀 현황판이 플레이어별 적 수를 세는 데 쓴다.
    // -1은 레인에 속하지 않는 적(물범 등).
    public int LaneIndex { get; private set; } = -1;

    // 스토리 건물은 변신 전까지 죽지 않는다. 피해는 그대로 쌓이고, 변신할 때 남은 체력이 보스 체력이 된다.
    bool invulnerable;

    // 스턴·구속을 거는 쪽이 각자 "원래 켜져 있었나"를 기억했다가 되돌리면, 효과가 겹쳤을 때
    // 나중에 끝나는 쪽이 "꺼져 있었다"를 복원해 적이 영영 멈춘다. 겹침 수만 세고,
    // 0이 될 때만 다시 움직이게 한다.
    int freezeCount;
    WaypointMover mover;

    public void AddFreeze()
    {
        freezeCount++;
        ApplyFreeze();
    }

    public void RemoveFreeze()
    {
        freezeCount = Mathf.Max(0, freezeCount - 1);
        ApplyFreeze();
    }

    void ApplyFreeze()
    {
        if (mover != null) mover.enabled = freezeCount == 0;
    }

    public void SetInvulnerable(bool value)
    {
        invulnerable = value;
    }

    public void SetLane(int laneIndex)
    {
        LaneIndex = laneIndex;
    }

    public static int CountInLane(int laneIndex)
    {
        int count = 0;
        foreach (EnemyDummy enemy in Active)
            if (enemy.LaneIndex == laneIndex)
                count++;
        return count;
    }

    public void Initialize(float maxHp)
    {
        hp = maxHp;
        MaxHp = maxHp;
    }

    // WaveSpawner가 EnemyData 전체를 넘겨줄 수 있게 되면 이 오버로드로 전환 — 보상 지급에 필요한 데이터를 함께 보관한다.
    public void Initialize(EnemyData enemyData)
    {
        data = enemyData;
        if (enemyData != null)
        {
            hp = enemyData.hp;
            MaxHp = enemyData.hp;
        }

        // Instantiate는 동기 호출이라(Awake가 그 안에서 바로 돈다) Update가 끼어들 틈이 없다 —
        // 지금 CurrentRound가 곧 이 적을 내보낸 웨이브의 라운드다. WaveSpawner를 거치지 않고
        // 이렇게 읽어서, 라운드 번호를 실어 나르려고 그 파일을 고칠 필요가 없다.
        SpawnRound = RoundManagerRef != null ? RoundManagerRef.CurrentRound : 0;
    }

    void Awake()
    {
        mover = GetComponent<WaypointMover>();

        // Initialize()를 거치지 않고 인스펙터 기본값(hp)만으로 씬에 배치된 경우를 위한 폴백 —
        // 이게 없으면 MaxHp가 0으로 남아 체력바가 항상 빈 채로 표시된다.
        if (MaxHp <= 0f)
        {
            MaxHp = hp;
        }
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    public void TakeDamage(float amount, int killerPlayerId)
    {
        if (isDead) return;

        hp -= amount;

        if (invulnerable)
        {
            hp = Mathf.Max(1f, hp);   // 다 깎여도 남겨둔다 — 변신할 때 최소 1로 시작
            return;
        }

        if (hp <= 0f)
        {
            if (!GameAuthority.IsServer) return;

            // Destroy는 프레임 끝에야 실제로 처리되므로, 같은 프레임에 다른 유닛이 또 때려서
            // 보상이 중복 지급되지 않도록 죽음 확정 시점에 바로 플래그를 세우고 등록도 해제한다.
            isDead = true;
            Active.Remove(this);

            // 파괴보다 먼저 부른다 — Destroy가 걸린 뒤엔 재생될 틈이 없다.
            // (지금은 즉시 파괴라 사실상 안 보이지만, 사망 연출을 넣을 자리를 여기로 정해둔다.)
            GetComponent<CharacterAnimator>()?.PlayDeath();

            if (data != null && RewardDistributor.Instance != null)
            {
                RewardDistributor.Instance.GrantKillReward(data, killerPlayerId);
            }

            // 신호만 보낸다 — 실제 처리(도박소 해금 등)는 구독하는 쪽 몫이다.
            if (data != null && data.isBoss)
            {
                OnBossKilled?.Invoke(SpawnRound);
            }

            Destroy(gameObject);
        }
    }
}
