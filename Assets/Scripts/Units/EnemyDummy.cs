using System.Collections.Generic;
using UnityEngine;

public class EnemyDummy : MonoBehaviour
{
    [SerializeField] float hp = 10f;

    public static readonly List<EnemyDummy> Active = new List<EnemyDummy>();

    public void Initialize(float maxHp)
    {
        hp = maxHp;
    }

    void OnEnable()
    {
        Active.Add(this);
    }

    void OnDisable()
    {
        Active.Remove(this);
    }

    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
