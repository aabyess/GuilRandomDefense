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

    // 보스 라운드인지는 웨이브가 실제로 뭘 내보내는지로 정한다. 예전엔 RoundManager가
    // "라운드 % 10 == 0"으로 판정했는데, 우리 보스는 10·20·30·40·50·60·**65**·70·**75**라
    // 65와 75를 놓쳤다. 원작도 마지막 세 보스가 5라운드 간격이라 주기로는 못 잡는다.
    public bool IsBossRound
    {
        get
        {
            if (spawnList == null) return false;
            foreach (WaveSpawnEntry entry in spawnList)
                if (entry.enemyData != null && entry.enemyData.isBoss) return true;
            return false;
        }
    }
}
