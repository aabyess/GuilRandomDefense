using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

    EnemyData data;
    bool isDead;

    public static readonly List<EnemyDummy> Active = new List<EnemyDummy>();

    public void Initialize(float maxHp)
    {
        hp = maxHp;
    }

    // WaveSpawner가 EnemyData 전체를 넘겨줄 수 있게 되면 이 오버로드로 전환 — 보상 지급에 필요한 데이터를 함께 보관한다.
    public void Initialize(EnemyData enemyData)
    {
        data = enemyData;
        if (enemyData != null)
        {
            hp = enemyData.hp;
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
        if (hp <= 0f)
        {
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
