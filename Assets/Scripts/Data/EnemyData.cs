using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyResourceReward
{
    public ResourceType type;
    public int amount;
}

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "GuilRandomDefense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public float hp;
    public float moveSpeed;
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;

    // true면 처치자 1명이 아니라 전체 플레이어에게 각자 보상 지급 (예: 물범 → 목재 1개씩).
    public bool rewardsAllPlayers;

    public bool isBoss;
    public GameObject prefab;
}
