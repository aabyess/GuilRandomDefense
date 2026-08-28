using UnityEngine;

public class UnitAttacker : MonoBehaviour
{
    [SerializeField] float attackRange = 5f;
    [SerializeField] float attackDamage = 2f;
    [SerializeField] float attackInterval = 1f;

    float attackTimer;

    public void ApplyStats(float damage, float range, float attacksPerSecond)
    {
        attackDamage = damage;
        attackRange = range;
        if (attacksPerSecond > 0f)
            attackInterval = 1f / attacksPerSecond;
    }

    void Update()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        attackTimer = attackInterval;

        EnemyDummy target = FindClosestEnemyInRange();
        if (target == null) return;

        target.TakeDamage(attackDamage);
    }

    EnemyDummy FindClosestEnemyInRange()
    {
        EnemyDummy closest = null;
        float closestSqrDistance = attackRange * attackRange;

        foreach (EnemyDummy enemy in EnemyDummy.Active)
        {
            float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = enemy;
        }

        return closest;
    }
}
