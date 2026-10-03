using UnityEngine;

// 풀카운트 점수 — 원작 udg_full_count(플레이어별). 원작 근거(Tools/w3x/원본/war3map_new.j, python 줄 번호):
//   시작 500(InitGlobals 10923) · 신세계 진입(Stage 11, 「60라운드-신세계 대기중」 29684~29709)에서 생존 여부와 상관없이 1~4번 전원 1250으로 덮고 full_C=0, 멀티보드 3번째 행 「풀카운트 : N 점」이 이때 처음 뜬다(setStyle 29704).
//   매 라운드 끝(Round_10ver Stage 20, 29763~29814): 생존자(PlayerDeath==0)마다 +배수×(5+full_C), 배수 = |clamp(내 레인 적수 P_Counter,1,50) − 50|(0마리면 50).
//     그 뒤 새 Level이 5의 배수면 full_C += 5(29785). 점수 가산은 구세계에서도 돌지만 신세계 진입 때 1250으로 덮이므로 결과에 안 남는다.
//   Level>75 클리어 블록(29790~29814): 생존자마다 +배수×10, 그리고 최종상위유닛 수 one_dill<2면 +1500(「최종상위유닛1개이하 클리어:추가점수 + 1500점을 부여합니다!」 5초), 아니면 +250(문구는 원작이 「클리어 점수 추가 300점을 부여합니다.」 — 원작 불일치 그대로).
//   신세계 보스 사망(sinsekai_boss_death 30201~30210): +300, 보스가 뜬 지 17.5초 이내면 +200 더(「신속한 보스처치:기본 300점 + 풀카운트 점수 200점 추가」, 아니면 「보스처치:풀카운트 점수 300점 추가」, 둘 다 2초).
//   소비처는 표시뿐이다(신세계 보상 사용 −625×횟수만 예외 — 우리엔 그 보상 위습(e015)이 아직 없다).
// one_dill(최종상위유닛 수) = 플레이어별 (a) PV 106~110 유닛이 맵에 들어올 때마다 +1(Trig_UnitJohabCounter 33504 — 우리 제한됨 등급), (b) 영원·불멸·영원히 채팅 코드 성공마다 +1(Trig_Eternal_*/IM_*/Forever_* 각 1회).
//   ⚠️ 빠진 것: 랜덤전용(PV 180) 유닛의 [조합]이 +8(Fusion3___Action 15146~15155 — 루프 안 +1이라 8번)인 경로는 우리 조합표에 대응이 정해지지 않아 안 넣었다.
// 호스트가 판정하고 NetGameState가 [Networked]로 복제해 클라 화면에 보인다.
public static class FullCountScore
{
    public const int Players = 4;
    const int StartScore = 500;
    const int NewWorldScore = 1250;
    public const float QuickBossSeconds = 17.5f;

    static readonly int[] score = new int[Players];
    static readonly int[] topUnits = new int[Players];
    static readonly float[] bossSpawnedAt = new float[Players];
    static int fullC;
    static bool visible;

    /// <summary>신세계에 들어선 뒤에만 점수판에 보인다(원작 멀티보드 행이 그때 생김).</summary>
    public static bool Visible => !GameAuthority.IsServer && NetGameState.Instance != null ? NetGameState.Instance.FullCountVisible : visible;

    public static int HostGet(int slot) => slot >= 0 && slot < Players ? score[slot] : 0;
    public static bool HostVisible => visible;

    /// <summary>화면에 보일 값 — 클라는 호스트가 복제한 것.</summary>
    public static int Get(int slot)
    {
        if (slot < 0 || slot >= Players) return 0;
        if (!GameAuthority.IsServer && NetGameState.Instance != null) return NetGameState.Instance.FullCounts.Get(slot);
        return score[slot];
    }

    public static void Begin()
    {
        for (int i = 0; i < Players; i++) { score[i] = StartScore; topUnits[i] = 0; bossSpawnedAt[i] = -1f; }
        fullC = 0;
        visible = false;
        cleared = false;
        UnitIdentity.OnAcquired -= HandleAcquired;
        UnitIdentity.OnAcquired += HandleAcquired;
        EnemyDummy.OnLaneBossKilled -= HandleLaneBossKilled;
        EnemyDummy.OnLaneBossKilled += HandleLaneBossKilled;
    }

    // (a) PV 106~110 = 원작 제한됨 10기 — 우리 제한됨 등급.
    static void HandleAcquired(UnitIdentity unit, UnitInventory inventory)
    {
        if (unit == null || unit.Data == null || unit.Data.grade != UnitGrade.Limited) return;
        AddTopUnit(unit.OwnerId);
    }

    /// <summary>one_dill +1 — 호출부: 제한됨 획득(위) · 영원·불멸·영원히 채팅 코드 성공(ChatUnlockManager).</summary>
    public static void AddTopUnit(int slot)
    {
        if (!GameAuthority.IsServer || slot < 0 || slot >= Players) return;
        topUnits[slot]++;
        Debug.Log($"[풀카운트] 슬롯 {slot} 최상위 유닛 {topUnits[slot]}");
    }

    static bool cleared;   // 마지막 라운드를 끝낸 뒤(원작 Level>75) — 보상 사용 감점 없음

    /// <summary>
    /// 신세계 보상 위습(표식) 사용 — 원작 Trig_Story_Tier6_Legend(j 84689~84696): Level≤75면 풀카운트 −= sinsekai_reward_int×625(신·악몽 2, 그 밖 1),
    /// 문구 「|cffFF0000신세계 보상사용:풀카운트{N}점 감소!」 2초(본인).
    /// </summary>
    public static void OnNewWorldRewardUsed(int slot)
    {
        if (!GameAuthority.IsServer || slot < 0 || slot >= Players || cleared) return;
        DifficultyMode mode = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current : DifficultyMode.Normal;
        int cost = (mode == DifficultyMode.God || mode == DifficultyMode.Nightmare ? 2 : 1) * 625;
        score[slot] -= cost;
        PlayerNotification.Show(slot, $"<color=#FF0000>신세계 보상사용:풀카운트{cost}점 감소!</color>", 2f);
        Debug.Log($"[풀카운트] 슬롯 {slot} 신세계 보상 사용 −{cost} · 합 {score[slot]}");
    }

    public static void OnNewWorldEnter()
    {
        if (!GameAuthority.IsServer) return;
        for (int i = 0; i < Players; i++) score[i] = NewWorldScore;
        fullC = 0;
        visible = true;
        Debug.Log($"[풀카운트] 신세계 진입 — 전원 {NewWorldScore}점, full_C=0");
    }

    public static void OnBossSpawned(int lane, int round)
    {
        if (lane >= 0 && lane < Players && round >= 65) bossSpawnedAt[lane] = Time.time;
    }

    static void HandleLaneBossKilled(int round, int lane)
    {
        if (!GameAuthority.IsServer || lane < 0 || lane >= Players || round < 65 || round % 5 != 0) return;
        float elapsed = bossSpawnedAt[lane] >= 0f ? Time.time - bossSpawnedAt[lane] : float.MaxValue;
        score[lane] += 300;
        if (elapsed <= QuickBossSeconds)
        {
            score[lane] += 200;
            PlayerNotification.Show(lane, "신속한 보스처치:기본 300점 + 풀카운트 점수 200점 추가", 2f);
        }
        else
        {
            PlayerNotification.Show(lane, "보스처치:풀카운트 점수 300점 추가", 2f);
        }
        Debug.Log($"[풀카운트] 슬롯 {lane} R{round} 보스 처치 {elapsed:F1}초 → {(elapsed <= QuickBossSeconds ? "+500(신속)" : "+300")} · 합 {score[lane]}");
    }

    /// <summary>
    /// 라운드가 끝났다(AdvanceRound 맨 앞, 아직 다음 라운드로 안 넘어감). finishedRound = 방금 끝난 라운드.
    /// finalClear = 마지막 라운드(75)를 끝냈다(원작 Level>75 블록).
    /// </summary>
    public static void OnRoundEnd(int finishedRound, bool finalClear)
    {
        if (!GameAuthority.IsServer) return;
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            int p = context.PlayerId;
            if (p < 0 || p >= Players || context.IsDead) continue;
            int multiple = Multiple(EnemyDummy.CountInLane(p));
            int add = multiple * (5 + fullC);
            score[p] += add;
            Debug.Log($"[풀카운트] R{finishedRound} 끝 슬롯 {p}: 적 {EnemyDummy.CountInLane(p)} → 배수 {multiple} × (5+{fullC}) = +{add} · 합 {score[p]}");
        }
        if ((finishedRound + 1) % 5 == 0) fullC += 5;   // 원작: 증가한 Level이 5의 배수

        if (!finalClear) return;
        cleared = true;
        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            int p = context.PlayerId;
            if (p < 0 || p >= Players || context.IsDead) continue;
            int multiple = Multiple(EnemyDummy.CountInLane(p));
            score[p] += multiple * 10;
            if (topUnits[p] < 2)
            {
                score[p] += 1500;
                PlayerNotification.Show(p, "최종상위유닛1개이하 클리어:추가점수 + 1500점을 부여합니다!", 5f);
            }
            else
            {
                PlayerNotification.Show(p, "클리어 점수 추가 300점을 부여합니다.", 5f);   // 원작 문구 그대로(실제 가산은 250)
                score[p] += 250;
            }
            Debug.Log($"[풀카운트] 클리어 슬롯 {p}: 배수 {multiple}×10 + {(topUnits[p] < 2 ? 1500 : 250)}(최상위 {topUnits[p]}기) · 합 {score[p]}");
        }
    }

    /// <summary>원작 full_count_multiple: |clamp(P,1,50) − 50|, P==0이면 50.</summary>
    public static int Multiple(int enemiesAlive)
    {
        if (enemiesAlive <= 0) return 50;
        return Mathf.Abs(Mathf.Clamp(enemiesAlive, 1, 50) - 50);
    }
}
