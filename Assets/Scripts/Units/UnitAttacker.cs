using UnityEngine;

public class UnitAttacker : MonoBehaviour
{
    [SerializeField] float attackRange = 5f;
    [SerializeField] float attackDamage = 2f;
    [SerializeField] float attackInterval = 1f;

    float attackTimer;

    void Update()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        EnemyDummy target = FindClosestEnemyInRange();
        if (target == null) return;

        target.TakeDamage(attackDamage);
        attackTimer = attackInterval;
    }

    EnemyDummy FindClosestEnemyInRange()
    {
        EnemyDummy[] enemies = FindObjectsByType<EnemyDummy>(FindObjectsSortMode.None);

        EnemyDummy closest = null;
        float closestSqrDistance = attackRange * attackRange;

        foreach (EnemyDummy enemy in enemies)
        {
            float sqrDistance = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance > closestSqrDistance) continue;

            closestSqrDistance = sqrDistance;
            closest = enemy;
        }

        return closest;
    }
}
