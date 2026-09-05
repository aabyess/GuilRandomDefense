using System.Collections.Generic;
using UnityEngine;

// 신세계 사이드보스(R62·66·71 전용) 오케스트레이터 — Docs/reference/ORIGINAL_BOSS_COMBAT_SPEC.md
// 전체를 구현한다. 원작이 플레이어별로 완전히 독립(트리거 sin_boss_skill1~4 4벌, 스펙 §②)
// 이라 이 매니저는 "인스턴스 하나"가 아니라 플레이어 배열로 상태를 들고, 실제 상태기계는
// SideBossEncounter가 보스 인스턴스마다 하나씩 돈다.
//
// ⚠️ 씬에 PlayerContext 슬롯마다 컴포넌트를 추가로 붙일 필요가 없다 — RoundManager가 이미
// 쓰는 관례(라운드 정보를 playerId로 인덱싱한 배열 필드로 들고 있는다, MaxTrackedLanes)를
// 그대로 따른다. 씬(SampleScene.unity)을 직접 못 고치는 제약과도 맞다.
//
// ⚠️ 우리 게임엔 "난이도(어려움 등)" 개념이 없다 — 원작 조건(§②, udg_Mode != "어려움")은
// 항상 통과로 취급한다(그 상태 자체가 될 수 없으니 항상 열려 있는 게 맞다). 원작의
// "Level mod 5 == 0이면 즉시 종료" 안전장치도 안 옮겼다 — 우리 트리거 조건 자체가 정확히
// {62, 66, 71}만 골라내서 5의 배수가 애초에 안 걸린다(62·66·71 전부 5의 배수가 아니다).
public class SideBossManager : MonoBehaviour
{
    const int MaxPlayers = 8; // RoundManager.MaxTrackedLanes와 같은 값 — 그쪽은 private라 따로 둔다.

    // 라운드 → 스폰 조건(§②) 확정 — 셋뿐이다.
    static readonly int[] TriggerRounds = { 62, 66, 71 };

    [SerializeField] RoundManager roundManager;
    [SerializeField] WaveSpawner waveSpawner;

    [Header("사이드보스 3기 — 라운드별(§③). 콘텐츠 담당이 채운다 — 비어 있으면 그 라운드는" +
            " 조용히 아무 일도 안 한다(사양 §⑪ 미확인 항목과 같은 관례, 지어내지 않는다).")]
    [SerializeField] EnemyData boss62;
    [SerializeField] EnemyData boss66;
    [SerializeField] EnemyData boss71;

    // §⑥ 스턴게이지 영속값 — 플레이어별, 라운드(R62→66→71)를 넘어 이어진다. 맵 시작 100
    // (스펙 §⑥ "초기화: 맵 시작 시 100").
    readonly float[] stunGauge = NewArray(100f);

    // §⑧ 정산 — 그 플레이어의 "다음 보스 라운드(R65/70/75) 시작 체력 배율". 기본 1f
    // (사이드보스전을 안 겪었으면 그대로) — 62/66/71을 거치면 실제 값으로 채워진다.
    // WaveSpawner.BossStartHpMultiplierProvider가 이 배열을 그대로 읽는다.
    readonly float[] nextBossStartHpMultiplier = NewArray(1f);

    // 이 라운드에서 이미 스폰했는지(레인별) — 스폰카운터가 15를 넘긴 뒤에도 계속 늘어나므로
    // 한 번만 트리거되게 막는다.
    readonly HashSet<int> spawnedLanesThisRound = new HashSet<int>();
    int spawnedForRound = -1;

    static float[] NewArray(float value)
    {
        float[] arr = new float[MaxPlayers];
        for (int i = 0; i < MaxPlayers; i++) arr[i] = value;
        return arr;
    }

    void OnEnable()
    {
        if (waveSpawner != null)
        {
            waveSpawner.OnEnemySpawned += HandleEnemySpawned;
            waveSpawner.BossStartHpMultiplierProvider = ProvideBossStartHpMultiplier;
        }
    }

    void OnDisable()
    {
        if (waveSpawner != null)
        {
            waveSpawner.OnEnemySpawned -= HandleEnemySpawned;
            if (waveSpawner.BossStartHpMultiplierProvider == (System.Func<int, float>)ProvideBossStartHpMultiplier)
                waveSpawner.BossStartHpMultiplierProvider = null;
        }
    }

    // §②·§203 Stage0 진입 — "그 라운드 스폰 카운터가 15일 때" 표시·시작한다. 우리는 "숨긴 채
    // 미리 스폰"을 안 하고(관찰 가능한 차이가 없다) 이 시점에 바로 스폰한다.
    // ⚠️ §⑪ 미확인 — 15번째/16번째 스폰 중 정확히 어느 쪽인지 원작 조건함수만으론 못 가른다.
    // spawnCounter(0-based)==15로 건다 — "우리 스폰 카운터를 같은 자리에 걸면 된다"(사양
    // 본문)는 지시를 그대로 따른 것이고, 정확한 순번 자체는 여전히 미확인이다.
    void HandleEnemySpawned(int laneIndex, int spawnCounter)
    {
        if (roundManager == null || waveSpawner == null) return;
        int round = roundManager.CurrentRound;
        if (System.Array.IndexOf(TriggerRounds, round) < 0) return;
        if (spawnCounter != 15) return;

        if (spawnedForRound != round)
        {
            spawnedForRound = round;
            spawnedLanesThisRound.Clear();
        }
        if (!spawnedLanesThisRound.Add(laneIndex)) return; // 이미 이 라운드에 이 레인은 스폰함.

        PlayerContext player = PlayerContext.GetOccupied(laneIndex);
        if (player == null || player.IsDead) return; // §② "생존 플레이어만".

        EnemyData bossData = round switch
        {
            62 => boss62,
            66 => boss66,
            71 => boss71,
            _ => null,
        };
        if (bossData == null) return; // 콘텐츠 미배정 — 조용히 아무 일도 안 한다.

        GameObject instance = waveSpawner.SpawnSideBoss(bossData, laneIndex);
        if (instance == null) return;

        SideBossEncounter encounter = instance.AddComponent<SideBossEncounter>();
        int playerId = laneIndex; // "레인 N = 플레이어 N"(WaveSpawner 관례 그대로).
        float startingGauge = playerId >= 0 && playerId < MaxPlayers ? stunGauge[playerId] : 100f;

        encounter.BeginEncounter(
            startingGauge,
            onFinishedCallback: pct => HandleEncounterFinished(playerId, pct),
            onStunGaugeChangedCallback: value => HandleStunGaugeChanged(playerId, value));
    }

    void HandleStunGaugeChanged(int playerId, float value)
    {
        if (playerId < 0 || playerId >= MaxPlayers) return;
        stunGauge[playerId] = value;
    }

    // §⑧ 정산 — 다음 R65/70/75 보스 시작 체력 배율을 저장해둔다.
    // 보스체력% = 보스체력%×0.85 + 사이드보스체력%×0.15.
    void HandleEncounterFinished(int playerId, float sideBossHpPercent)
    {
        if (playerId < 0 || playerId >= MaxPlayers) return;
        float multiplier = 0.85f + (sideBossHpPercent / 100f) * 0.15f;
        nextBossStartHpMultiplier[playerId] = multiplier;
    }

    // WaveSpawner가 보스를 스폰하기 직전에 묻는다. 쓴 뒤엔 다음 보스를 위해 1f(기본, "사이드
    // 보스전 없었음"과 같은 취급)로 되돌린다 — R65 결과가 R70·R75까지 계속 적용되면 안 된다.
    float ProvideBossStartHpMultiplier(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= MaxPlayers) return 1f;
        float value = nextBossStartHpMultiplier[laneIndex];
        nextBossStartHpMultiplier[laneIndex] = 1f;
        return value;
    }
}
