using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveSpawnEntry
{
    public EnemyData enemyData;
    public int count;
    public float spawnInterval;
}

[CreateAssetMenu(fileName = "NewWaveData", menuName = "GuilRandomDefense/Wave Data")]
public class WaveData : ScriptableObject
{
    public int roundNumber;
    public List<WaveSpawnEntry> spawnList;

    // 이 라운드를 마치면 전체 플레이어에게 지급.
    public List<WispReward> wispRewards;
}
