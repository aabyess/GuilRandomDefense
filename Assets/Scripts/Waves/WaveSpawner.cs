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

    // 보스 타임리밋 패배(2단계 A, 원작 Trig_Enemy_Boss_create/sinsekai) — 일반 스폰
    // 목록(spawnList)을 타는 라운드보스가 스폰될 때만 쏜다(laneIndex, roundNumber, 그
    // EnemyDummy). SpawnSideBoss(사이드보스 62/66/71, 광폭화 소환 몹)는 이 이벤트를
    // 아예 안 거쳐서 자동으로 제외된다 — 사이드보스는 시간이 지나도 패배가 아니라는
    // PM 지시와 정확히 일치한다. RoundManager가 구독해 라운드별 제한시간(구세계
    // 75.30초/신세계 34.80초)을 잰다.
    public event System.Action<int, int, EnemyDummy> OnRoundBossSpawned;

    // SpawnSideBoss가 일반 몹(광폭화 소환)을 스폰할 때 원작 R00A 구간 보너스를 적용하려면
    // 그 라운드 번호가 필요하다(2026-09-11, PM 리뷰 정정). GameHud 등과 같은 지연 조회 관례.
    RoundManager roundManager;
    RoundManager RoundManagerRef => roundManager != null
        ? roundManager
        : roundManager = FindFirstObjectByType<RoundManager>();

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
            if (berserkMode == BerserkMode.FixedTime) activeSpawnCoroutines.Add(StartCoroutine(FixedTimeBerserkRoutine(wave, laneIndex, lanePath)));
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
        int berserkCount = 0;   // 이 레인·이 라운드에 이미 나온 광폭화 수

        foreach (WaveSpawnEntry entry in wave.spawnList)
        {
            if (entry.enemyData == null || entry.enemyData.prefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                // §⑧ 정산 — 보스만 물어본다(일반 몹에 물으면 뜻이 없다, 공급자도 어차피
                // 보스 라운드에만 1이 아닌 값을 준다).
                float sideBossSettlement = entry.enemyData.isBoss
                    ? (BossStartHpMultiplierProvider?.Invoke(laneIndex) ?? 1f)
                    : 1f;

                // 난이도 체력 배율(2026-09-11, PM 지시) — 사이드보스 정산 배율과는 완전히
                // 다른 축이라 곱해서 합친다(둘 다 "시작 체력에 곱하는 배수"라 startHpMultiplier
                // 하나로 합쳐 넘길 수 있다, DifficultyMode.cs 참고).
                float difficultyMultiplier = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected
                    ? (entry.enemyData.isBoss
                        ? DifficultyTable.BossHpMultiplier(DifficultyManager.Instance.Current)
                        : DifficultyTable.MobHpMultiplier(DifficultyManager.Instance.Current, wave.roundNumber))
                    : 1f;

                bool berserk = ShouldSpawnBerserk(entry.enemyData, wave.roundNumber, berserkCount);
                if (berserk) berserkCount++;
                GameObject spawned = SpawnEnemyInternal(entry.enemyData, laneIndex, lanePath, sideBossSettlement * difficultyMultiplier, berserk);

                // 보스 타임리밋 패배(2단계 A) — 일반 스폰 목록을 타는 라운드보스만 쏜다.
                if (entry.enemyData.isBoss && spawned != null && spawned.TryGetComponent(out EnemyDummy bossDummy))
                {
                    OnRoundBossSpawned?.Invoke(laneIndex, wave.roundNumber, bossDummy);
                }

                OnEnemySpawned?.Invoke(laneIndex, spawnCounter);
                spawnCounter++;
                yield return new WaitForSeconds(entry.spawnInterval);
            }
        }
    }

    // 보스는 같은 줄의 일반 적보다 크게(2026-09-29 유저 피드백 「보스유닛은 일반유닛들보다 사이즈 크게」).
    // 모델 키는 ArtBinder가 실제 키(미터)로 맞춰 두어 와폴(2.07m)이 라인몹과 거의 같은 크기였다 — 여기서 한 배율을 더 건다.
    // MP: 거울 루트(NetEntity)가 호스트의 스케일을 실어 가므로 클라도 같다.
    public const float BossScale = 1.6f;
    const float BossMaxScale = 4f;   // 목표 키로 키울 때 상한 — 키 측정이 틀려도 화면을 덮지 않게

    /// <summary>보스 배율 — 기본 BossScale, 목표 키(EnemyData.bossTargetHeight)가 있으면 max(BossScale, 목표÷프리팹 키). 키는 막 만든 인스턴스의 렌더러 경계 높이.</summary>
    public static float BossScaleFor(EnemyData data, GameObject instance)
    {
        if (data == null || data.bossTargetHeight <= 0f || instance == null) return BossScale;
        float height = data.bossModelHeight > 0f ? data.bossModelHeight : MeasureHeight(instance);
        if (height < 1f) return BossScale;
        return Mathf.Clamp(data.bossTargetHeight / height, BossScale, BossMaxScale);
    }

    static float MeasureHeight(GameObject root)
    {
        bool any = false; Bounds bounds = default;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        return any ? bounds.size.y : 0f;
    }

    // 광폭화 유닛(2026-10-07 사장님 사양, BerserkMob) — R61~ 레인 적이 나올 때 확률로 일반 적 한 기를 광폭화 변형으로 바꾼다(라운드 전체 수는 그대로).
    // 확률·상한·회복%·이속·크기는 테스트 뒤 바꿀 값이라 인스펙터에 둔다. 서버(호스트)만 판정.
    public enum BerserkMode { Chance, FixedTime }
    [Header("광폭화 유닛 (R61~)")]
    [SerializeField] bool berserkEnabled = true;
    // Chance: 일반 적이 나올 때마다 확률로 그 한 기를 광폭화로 대체(사장님 사양). FixedTime: 라운드 시작 N초 뒤 레인마다 1기를 더 낸다(현행 ORDR식 — 보스 라운드 제외).
    [SerializeField] BerserkMode berserkMode = BerserkMode.Chance;
    [SerializeField] float berserkFixedTimeSeconds = 25f;
    [SerializeField] int berserkMinRound = 61;
    [SerializeField, Range(0f, 1f)] float berserkChance = 0.10f;
    [SerializeField] int berserkMaxPerLanePerRound = 1;           // 사장님 10-07 「한 라운드에 한 마리」
    [SerializeField] float berserkRegenFractionPerSecond = 0.01f;   // 원작 A14I 100만/초 ≈ 신 R61~75 최대체력의 0.7~2.2%/초 → 1%
    [SerializeField] float berserkDefenseAura = 5f;                 // 원작 A125
    [SerializeField] float berserkMoveSpeedMultiplier = 1f;         // 사장님 10-07 「적 유닛이랑 속도 맞춰라」 — 같은 라운드 일반 적과 같은 속도(난이도 R024 배율 포함). 원작도 이속 증가 0(Absk bsk2=0)
    [SerializeField] float berserkLaneOutwardOffset = 45f;          // 왼쪽 길 중심선보다 바다 쪽(−x)으로 비키는 거리(길 폭 반쯤~한 칸) — 일반 적 줄과 안 겹치게
    [SerializeField] float berserkLaneMobScale = 2f;                       // 사장님 10-07 「라인몹 ×2.0」: 일반(~39) < 광폭화(~72) < 보스(목표 96)
    /// <summary>시험용 — 0 이상이면 확률을 이 값으로 덮는다(1 = 항상). 인스펙터 값은 그대로.</summary>
    public static float BerserkChanceOverride = -1f;
    public static int BerserkMinRoundOverride = -1;

    // 고정 시각 모드 — 라운드 시작 berserkFixedTimeSeconds 뒤 이 레인에 광폭화 1기를 더 낸다(그 라운드의 첫 일반 적 종류, 난이도 배율 포함). 보스 라운드는 건너뛴다.
    IEnumerator FixedTimeBerserkRoutine(WaveData wave, int laneIndex, WaypointPath lanePath)
    {
        int minRound = BerserkMinRoundOverride >= 0 ? BerserkMinRoundOverride : berserkMinRound;
        if (!berserkEnabled || wave.roundNumber < minRound) yield break;
        WaveSpawnEntry pick = null;
        foreach (WaveSpawnEntry e in wave.spawnList) if (e.enemyData != null && e.enemyData.prefab != null) { if (e.enemyData.isBoss) yield break; pick ??= e; }
        if (pick == null) yield break;
        yield return new WaitForSeconds(berserkFixedTimeSeconds);
        float mult = DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected
            ? DifficultyTable.MobHpMultiplier(DifficultyManager.Instance.Current, wave.roundNumber) : 1f;
        SpawnEnemyInternal(pick.enemyData, laneIndex, lanePath, mult, berserk: true);
    }

    bool ShouldSpawnBerserk(EnemyData enemyData, int roundNumber, int alreadyThisRound)
    {
        // 🔴 2026-10-09 사장님(「광폭화 몬스터는 원작처럼 개인 보스 소환물로」): 「일반 적 대신 10% 확률」 대체는 폐기 — 시험용 override(BerserkChanceOverride ≥ 0)만 남긴다.
        //    광폭화는 이제 SideBossEncounter가 시전을 끝낼 때만 나온다(SpawnSideBoss(berserk: true)).
        if (BerserkChanceOverride < 0f) return false;
        if (!berserkEnabled || berserkMode != BerserkMode.Chance || !GameAuthority.IsServer || enemyData == null || enemyData.isBoss) return false;
        int minRound = BerserkMinRoundOverride >= 0 ? BerserkMinRoundOverride : berserkMinRound;
        if (roundNumber < minRound || alreadyThisRound >= berserkMaxPerLanePerRound) return false;
        float chance = BerserkChanceOverride >= 0f ? BerserkChanceOverride : berserkChance;
        return Random.value < chance;
    }

    GameObject SpawnEnemyInternal(EnemyData enemyData, int laneIndex, WaypointPath lanePath, float startHpMultiplier = 1f, bool berserk = false)
    {
        GameObject instance = Instantiate(enemyData.prefab);
        if (enemyData.isBoss) instance.transform.localScale *= BossScaleFor(enemyData, instance);
        if (berserk) instance.transform.localScale *= berserkLaneMobScale;

        if (instance.TryGetComponent(out WaypointMover mover))
        {
            mover.SetPath(lanePath);

            // 이동속도(R024, 2단계 B) — 원작 라운드 몹 upgr에만 걸린다. 보스(라운드보스·
            // 신세계보스·사이드보스)와 광폭화 소환 몹은 안 받는다 — 여기 SpawnEnemyInternal은
            // 셋 다 거치는 공용 경로라 !enemyData.isBoss로 가른다(A11S 고정 레벨과 같은 게이트).
            float moveSpeedMultiplier = !enemyData.isBoss && DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected
                ? DifficultyTable.MoveSpeedMultiplier(DifficultyManager.Instance.Current)
                : 1f;
            mover.SetMoveSpeed(enemyData.moveSpeed * moveSpeedMultiplier * (berserk ? berserkMoveSpeedMultiplier : 1f));
            if (berserk) mover.SetShuttleLeftEdge(berserkLaneOutwardOffset);
        }

        if (instance.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(enemyData, startHpMultiplier);
            dummy.SetLane(laneIndex);
            if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected)
                dummy.DifficultyArmorBonus = enemyData.isBoss
                    ? DifficultyTable.BossArmorBonus(DifficultyManager.Instance.Current)
                    : DifficultyTable.MobArmorBonus(DifficultyManager.Instance.Current, dummy.SpawnRound);
            if (berserk) instance.AddComponent<BerserkMob>().Begin(berserkRegenFractionPerSecond, berserkDefenseAura);

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
    public GameObject SpawnSideBoss(EnemyData enemyData, int laneIndex, bool berserk = false)
    {
        WaypointPath lanePath = GetLanePath(laneIndex);
        if (enemyData == null || enemyData.prefab == null || lanePath == null) return null;

        // 🔴 2026-09-11 정정(PM 리뷰) — 이 함수는 사이드보스 전용이 아니다. SideBossEncounter.
        // SpawnBerserkMob(§⑦ 광폭화 소환)도 같은 함수로 "그 라운드의 잡몹"(berserkMobData,
        // isBoss=false)을 스폰한다. 원작 Trig_sin_boss_skill1~4는 그 잡몹을 udg_Round_UnitType로
        // 만들어 R00A(구간 보너스 포함)+R00W를 그대로 받는다 — 보스 전용표(BossHpMultiplier)를
        // 무조건 곱하면 지옥·신·악몽에서 그 잡몹이 실제보다 저체력이 된다(지옥 ×5.5 vs 원작
        // ×7.86 등, isBoss로 안 가르면 원작보다 약하게 나가는 사고). 신세계 사이드보스는 isBoss=1이라
        // 원래 의도대로 BossHpMultiplier를 그대로 받는다(PM 지시 C — R00A 안 받고 보스 전용
        // 표만 적용). §⑧ 정산 배율은 SideBossManager가 이 보스 자체엔 안 건다(다음
        // 라운드보스한테만 건다, ProvideBossStartHpMultiplier 참고) — 여기선 난이도 배율만
        // 곱하면 된다.
        float difficultyMultiplier;
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsModeSelected)
        {
            difficultyMultiplier = enemyData.isBoss
                ? DifficultyTable.BossHpMultiplier(DifficultyManager.Instance.Current)
                : DifficultyTable.MobHpMultiplier(DifficultyManager.Instance.Current, RoundManagerRef != null ? RoundManagerRef.CurrentRound : 0);
        }
        else
        {
            difficultyMultiplier = 1f;
        }

        return SpawnEnemyInternal(enemyData, laneIndex, lanePath, difficultyMultiplier, berserk);
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
