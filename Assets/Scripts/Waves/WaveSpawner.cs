using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpawner : MonoBehaviour
{
    [SerializeField] List<WaveData> waves;
    [SerializeField] List<WaypointPath> lanePaths;

    public IReadOnlyList<WaveData> Waves => waves;

    readonly List<Coroutine> activeSpawnCoroutines = new List<Coroutine>();

    // 2026-09-06 추가(신세계 사이드보스, ORIGINAL_BOSS_COMBAT_SPEC.md §②) — 원작은 사이드보스를
    // "그 라운드 스폰 카운터가 15일 때" 표시·시작한다. WaveData.spawnList엔 없는 별도 개체라
    // 이 이벤트 하나만 추가해서 SideBossManager가 밖에서 그 순간을 알 수 있게 한다 —
    // 레인당 한 번(entry 하나에서 count번 도는) 스폰마다, 그 레인 안에서 몇 번째 스폰인지
    // (0-based, 레인 안의 모든 entry를 통틀어 누적)를 같이 보낸다. 아무도 안 구독해도
    // 기존 동작에 영향이 없다(이벤트 그대로, 안 쓰면 안 부르는 것과 같다).
    public event System.Action<int, int> OnEnemySpawned;

    // §⑧ 정산 — 보스를 스폰하기 직전에 "이 레인의 보스는 시작 체력을 몇 배로 해야 하는가"를
    // 묻는다. SideBossManager가 자신을 구독시킨다(laneIndex → multiplier, 보통 1f).
    // 아무도 안 구독하면(null) 항상 1f — 기존 동작과 완전히 같다(회귀 0).
    public System.Func<int, float> BossStartHpMultiplierProvider;

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

            // 레인 N = 플레이어 N. 아무도 없는 레인에 적을 뿌리면 막을 사람이 없어
            // 그 레인만 쌓이다가 패배 판정(가장 붐비는 레인 기준)을 건드린다.
            if (PlayerContext.GetOccupied(laneIndex) == null) continue;

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
        // 레인 하나 안에서 이 라운드에 통틀어 몇 번째 스폰인지(0-based) — entry가 여러 개라도
        // 안 끊긴다. OnEnemySpawned 전용, 그 외 로직엔 안 쓴다.
        int spawnCounter = 0;

        foreach (WaveSpawnEntry entry in wave.spawnList)
        {
            if (entry.enemyData == null || entry.enemyData.prefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                // §⑧ 정산 — 보스만 물어본다(일반 몹에 물으면 뜻이 없다, 공급자도 어차피
                // 보스 라운드에만 1이 아닌 값을 준다).
                float multiplier = entry.enemyData.isBoss
                    ? (BossStartHpMultiplierProvider?.Invoke(laneIndex) ?? 1f)
                    : 1f;
                SpawnEnemyInternal(entry.enemyData, laneIndex, lanePath, multiplier);
                OnEnemySpawned?.Invoke(laneIndex, spawnCounter);
                spawnCounter++;
                yield return new WaitForSeconds(entry.spawnInterval);
            }
        }
    }

    GameObject SpawnEnemyInternal(EnemyData enemyData, int laneIndex, WaypointPath lanePath, float startHpMultiplier = 1f)
    {
        GameObject instance = Instantiate(enemyData.prefab);

        if (instance.TryGetComponent(out WaypointMover mover))
        {
            mover.SetPath(lanePath);
            mover.SetMoveSpeed(enemyData.moveSpeed);
        }

        if (instance.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(enemyData, startHpMultiplier);
            dummy.SetLane(laneIndex);

            // ⚠️ 2026-09-06 추가(항법 "패왕의길"/히든 이벤트, NAVIGATION_ROUTES_FULL.md,
            // 리서치담당 c3b8c42 정정) — 원작 Trig_Round_10ver_Actions: 일반 라운드 몹만
            // 스폰 시점에 A11S 레벨을 이 라인의 Damage_level_Fixed만큼 영구히 올린다.
            // 라운드보스·신세계 사이드보스는 Round_Unit과 별개 경로(A11S 고정)라 안 탄다 —
            // `!enemyData.isBoss` 하나로 이 함수를 공유하는 SpawnSideBoss 호출까지 같이
            // 걸러진다(사이드보스 EnemyData도 isBoss=1). Damage_level_Fixed=0(기본)이면
            // AddA11SStack(0)이라 지금과 정확히 같다 — 회귀 없음.
            if (!enemyData.isBoss)
            {
                int fixedLevel = PlayerContext.Get(laneIndex)?.DamageLevelFixedState?.Value ?? 0;
                if (fixedLevel != 0) dummy.AddA11SStack(fixedLevel);
            }
        }

        return instance;
    }

    // 레인의 WaypointPath를 밖에 노출한다 — SideBossManager가 사이드보스를 그 레인 경로
    // 위에 직접 스폰할 때 쓴다(WaveData.spawnList를 안 거치는 별도 개체라, SpawnRound의
    // 일반 스폰 루프 밖에서 필요하다).
    public WaypointPath GetLanePath(int laneIndex) =>
        lanePaths != null && laneIndex >= 0 && laneIndex < lanePaths.Count ? lanePaths[laneIndex] : null;

    // 신세계 사이드보스(2026-09-06) 전용 — WaveData.spawnList에 없는 별도 개체라 일반
    // SpawnRound 루프를 안 거치고 SideBossManager가 직접 부른다.
    public GameObject SpawnSideBoss(EnemyData enemyData, int laneIndex)
    {
        WaypointPath lanePath = GetLanePath(laneIndex);
        if (enemyData == null || enemyData.prefab == null || lanePath == null) return null;
        return SpawnEnemyInternal(enemyData, laneIndex, lanePath);
    }

    // §⑦ 광폭화 소환(2026-09-06) — SideBossManager/SideBossEncounter가 "그 라운드의 잡몹
    // 1기"(원작 udg_Round_UnitType[udg_Level])를 찾을 때 쓴다. RoundManager.GetWaveData와
    // 같은 조회를 여기 공개로 하나 더 둔다 — RoundManager 쪽은 private이고, 그 라운드의
    // WaypointPath는 어차피 WaveSpawner만 갖고 있어 여기서 같이 찾는 게 자연스럽다.
    public WaveData GetWaveData(int roundNumber)
    {
        if (waves == null) return null;
        foreach (WaveData waveData in waves)
            if (waveData != null && waveData.roundNumber == roundNumber)
                return waveData;
        return null;
    }
}
