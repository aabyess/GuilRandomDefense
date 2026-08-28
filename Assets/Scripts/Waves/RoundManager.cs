using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    [SerializeField] WaveSpawner waveSpawner;
    [SerializeField] List<WaveData> rounds;
    [SerializeField] float roundDuration = 28f;
    [SerializeField] int totalRounds = 25;
    [SerializeField] int bossRoundInterval = 10;
    [SerializeField] int maxFieldEnemies = 25;

    int currentRound;
    float roundTimer;
    bool isGameOver;

    void Start()
    {
        currentRound = 1;
        StartRound(currentRound);
    }

    void Update()
    {
        if (isGameOver) return;

        if (EnemyDummy.Active.Count > maxFieldEnemies)
        {
            Debug.Log("패배!");
            isGameOver = true;
            return;
        }

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
        {
            AdvanceRound();
        }
    }

    void AdvanceRound()
    {
        currentRound++;
        if (currentRound > totalRounds)
        {
            Debug.Log("모든 라운드 클리어!");
            isGameOver = true;
            return;
        }

        StartRound(currentRound);
    }

    void StartRound(int roundNumber)
    {
        roundTimer = roundDuration;

        if (roundNumber % bossRoundInterval == 0)
        {
            Debug.Log("보스 라운드!");
        }

        WaveData waveData = GetWaveData(roundNumber);
        if (waveSpawner != null && waveData != null)
        {
            waveSpawner.SpawnRound(waveData);
        }
    }

    WaveData GetWaveData(int roundNumber)
    {
        if (rounds == null) return null;

        foreach (WaveData waveData in rounds)
        {
            if (waveData != null && waveData.roundNumber == roundNumber)
                return waveData;
        }

        return null;
    }
}
