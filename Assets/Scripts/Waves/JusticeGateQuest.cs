using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 정의문(ZTsg) 뒤 보상 사슬 — 원작 Trig_door_quest + Trig_Red_dog(war3map_new.j 13990~14040 직접 대조).
//
// ① 문 파괴: 생존자(udg_PlayerDeath==0)만 골드 +20,000 · 흔함선택 위습 2 · 세이브 +3 · 업적판 1단계(A02U→A0MY, 우리에 업적판이 없어 로그만).
//    영웅 경험치 600은 Exp_Group 전원(생존 검사 없음). 문구 TRIGSTR_12732 전원 10초.
// ② dog_zone(펑크해저드 공터)에 3제독 중 하나가 **항상** 1기, GetRandomInt(1,3) 균등. 우리는 SeaKingSpawner처럼 고정 표적으로 세운다.
//    o02M 붉은개(아카이누, 강렬함) · o02Q 노란원숭이(키자루, 신속함) · o02R 푸른꿩(아오키지, 냉철함) — 스탯은 셋 다 같다(HP 33,700,000·방어 140, w3u).
// ③ 그 몹 처치(죽인 사람 무관): 생존자 FOOD_USED+1(=특성 포인트 1) · 세이브 +1 · 영웅 경험치 600 · 종류별 팀 전체 버프(TeamBuffs, PlayerDeath 조건 없음).
public class JusticeGateQuest : MonoBehaviour
{
    [SerializeField] EnemyData redDog;        // o02M 붉은개 — 강렬함(R025)
    [SerializeField] EnemyData yellowMonkey;  // o02Q 노란원숭이 — 신속함(R027)
    [SerializeField] EnemyData blueBird;      // o02R 푸른꿩 — 냉철함(R026)
    [SerializeField] WispData commonChoiceWisp;   // e018 흔함선택위습

    const int DoorGold = 20000;
    const int DoorWispCount = 2;
    const int DoorSavePoints = 3;
    const int DogSavePoints = 1;
    const int HeroXp = 600;

    // o02M/Q/R 모두 upgr에 R01A~R01E(모드별 +50%)가 있어 거대 해왕류와 같은 1.5배(SeaKingSpawner.HpMultiplier 주석 참고).
    const float HpMultiplier = 1.5f;

    /// <summary>점검 전용 — 1·2·3이면 GetRandomInt(1,3) 대신 그 종류로 소환한다(JusticeQuestProbe). 0이면 원작대로 무작위.</summary>
    public static int ForcedKind;

    bool started;
    GameObject current;

    void Awake() => TeamBuffs.Reset();

    void OnEnable() => DestructibleGate.OnBroken += HandleGateBroken;

    void OnDisable() => DestructibleGate.OnBroken -= HandleGateBroken;

    void HandleGateBroken(DestructibleGate gate)
    {
        if (started || !GameAuthority.IsServer) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        GrantDoorRewards();

        int kind = ForcedKind >= 1 && ForcedKind <= 3 ? ForcedKind : Random.Range(1, 4);   // GetRandomInt(1,3)
        EnemyData data = kind == 1 ? redDog : kind == 2 ? yellowMonkey : blueBird;
        if (!SpawnDog(data)) yield break;

        // 파괴된 Unity 객체의 == null은 참이다 — 죽어서 Destroy되는 순간부터(SeaKingSpawner.Run과 같은 패턴).
        yield return new WaitUntil(() => current == null);

        GrantDogRewards(kind);
    }

    void GrantDoorRewards()
    {
        var wisp = commonChoiceWisp != null
            ? new List<WispReward> { new WispReward { wisp = commonChoiceWisp, count = DoorWispCount } }
            : null;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            PlayerNotification.Show(context.PlayerId,
                "<color=#959595>도전과제 -도로개척 완료!</color>\n<color=#DB7093>보상-흔함선택 위습 2기</color><color=#FFD700>+20000골드</color> 를 획득합니다!", 10f);
            if (context.IsDead) continue;   // 원작 udg_PlayerDeath[i]==0

            context.GoldWallet?.Add(DoorGold);
            if (wisp != null && RewardDistributor.Instance != null) RewardDistributor.Instance.GrantWisps(context, wisp);
            context.PersistentSave?.AddSessionPoints(DoorSavePoints);
            Debug.Log($"[정의문] P{context.PlayerId} 업적판 1단계(A02U→A0MY) — 우리에 업적판이 없어 생략");
        }
        UnitAttacker.GrantHeroXpToAllHeroes(HeroXp);
        Debug.Log($"[정의문] 문 파괴 보상: 생존자 골드 {DoorGold} · 위습 {DoorWispCount} · 세이브 {DoorSavePoints} · 영웅 XP {HeroXp}");
    }

    bool SpawnDog(EnemyData data)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning($"{name}: 3제독 데이터나 prefab이 비어 있어 소환하지 못했습니다.", this);
            return false;
        }

        current = Instantiate(data.prefab, transform.position, transform.rotation);
        if (current.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(data, HpMultiplier);
            dummy.SetLane(-1);   // 퀘스트 몹 — 레인 카운트·패배 판정에 안 섞인다
        }
        if (current.TryGetComponent(out WaypointMover mover)) mover.enabled = false;
        Debug.Log($"[정의문] 3제독 소환: {data.enemyName} HP {data.hp * HpMultiplier:N0} 위치 {transform.position}");
        return true;
    }

    void GrantDogRewards(int kind)
    {
        string text;
        switch (kind)
        {
            case 1: TeamBuffs.ActivateIntense(); text = "강렬함버프 발동!"; break;   // R025
            case 2: TeamBuffs.ActivateSwift(); text = "신속함 버프발동!"; break;     // R027
            default: TeamBuffs.ActivateCalm(); text = "냉철함 버프 발동!"; break;    // R026
        }

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            PlayerNotification.Show(context.PlayerId,
                "<color=#959595>도전과제 -해군대장 저지 완료!</color>\n<color=#1E90FF>+ 특성 포인트 1개를 획득합니다!</color>", 10f);
            if (!context.IsDead)
            {
                context.UnitUpgrades?.AddTraitPoints(1);   // FOOD_USED +1
                context.PersistentSave?.AddSessionPoints(DogSavePoints);
            }
            PlayerNotification.Show(context.PlayerId, text, 10f);
        }
        UnitAttacker.GrantHeroXpToAllHeroes(HeroXp);
        Debug.Log($"[정의문] 3제독 처치: {text} · 생존자 특성+1 · 세이브+{DogSavePoints} · 영웅 XP {HeroXp}");
    }
}
