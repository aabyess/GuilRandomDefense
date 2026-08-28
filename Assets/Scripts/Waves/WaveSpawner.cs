using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] List<WaveData> waves;
    [SerializeField] WaypointPath path;

    public IReadOnlyList<WaveData> Waves => waves;

    public void SpawnRound(WaveData wave)
    {
        if (wave == null || wave.spawnList == null) return;
        StartCoroutine(SpawnRoutine(wave));
    }

    IEnumerator SpawnRoutine(WaveData wave)
    {
        foreach (WaveSpawnEntry entry in wave.spawnList)
        {
            if (entry.enemyData == null || entry.enemyData.prefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry.enemyData);
                yield return new WaitForSeconds(entry.spawnInterval);
            }
        }
    }

    void SpawnEnemy(EnemyData enemyData)
    {
        GameObject instance = Instantiate(enemyData.prefab);

        if (instance.TryGetComponent(out WaypointMover mover))
        {
            mover.SetPath(path);
            mover.SetMoveSpeed(enemyData.moveSpeed);
        }

        if (instance.TryGetComponent(out EnemyDummy dummy))
            dummy.Initialize(enemyData.hp);
    }
}
