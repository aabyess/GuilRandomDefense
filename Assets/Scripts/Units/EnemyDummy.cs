using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

    EnemyData data;
    bool isDead;

    public static readonly List<EnemyDummy> Active = new List<EnemyDummy>();

    public float Hp => hp;
    public float MaxHp { get; private set; }
    public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(hp / MaxHp) : 0f;

    // 어느 레인에 스폰됐는지. 팀 현황판이 플레이어별 적 수를 세는 데 쓴다.
    // -1은 레인에 속하지 않는 적(물범 등).
    public int LaneIndex { get; private set; } = -1;

    // 스토리 건물은 변신 전까지 죽지 않는다. 피해는 그대로 쌓이고, 변신할 때 남은 체력이 보스 체력이 된다.
    bool invulnerable;

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
    }

    void Awake()
    {
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

            if (data != null && RewardDistributor.Instance != null)
            {
                RewardDistributor.Instance.GrantKillReward(data, killerPlayerId);
            }

            Destroy(gameObject);
        }
    }
}
