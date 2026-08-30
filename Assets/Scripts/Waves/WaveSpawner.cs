using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] List<WaveData> waves;
    [SerializeField] List<WaypointPath> lanePaths;

    public IReadOnlyList<WaveData> Waves => waves;

    readonly List<Coroutine> activeSpawnCoroutines = new List<Coroutine>();

    public void SpawnRound(WaveData wave)
    {
        if (!GameAuthority.IsServer) return;

        StopActiveSpawns();

        if (wave == null || wave.spawnList == null) return;

        if (lanePaths == null || lanePaths.Count == 0)
        {
            Debug.LogWarning("WaveSpawner: lanePaths가 비어있어 스폰할 레인이 없습니다.");
            return;
        }

        for (int laneIndex = 0; laneIndex < lanePaths.Count; laneIndex++)
        {
            WaypointPath lanePath = lanePaths[laneIndex];
            if (lanePath == null)
            {
                Debug.LogWarning($"WaveSpawner: {laneIndex}번 레인의 WaypointPath가 비어있어 이 레인은 건너뜁니다.");
                continue;
            }

            activeSpawnCoroutines.Add(StartCoroutine(SpawnRoutine(wave, laneIndex, lanePath)));
        }
    }

    // 새 라운드가 시작될 때 이전 라운드의 레인별 스폰 코루틴이 남아있으면 정리한다.
    void StopActiveSpawns()
    {
        foreach (Coroutine routine in activeSpawnCoroutines)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        activeSpawnCoroutines.Clear();
    }

    IEnumerator SpawnRoutine(WaveData wave, int laneIndex, WaypointPath lanePath)
    {
        foreach (WaveSpawnEntry entry in wave.spawnList)
        {
            if (entry.enemyData == null || entry.enemyData.prefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry.enemyData, laneIndex, lanePath);
                yield return new WaitForSeconds(entry.spawnInterval);
            }
        }
    }

    void SpawnEnemy(EnemyData enemyData, int laneIndex, WaypointPath lanePath)
    {
        GameObject instance = Instantiate(enemyData.prefab);

        if (instance.TryGetComponent(out WaypointMover mover))
        {
            mover.SetPath(lanePath);
            mover.SetMoveSpeed(enemyData.moveSpeed);
        }

        if (instance.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(enemyData);
            dummy.SetLane(laneIndex);
        }
    }
}
