using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 거대 해왕류(원작 [퀘스트] 카테고리, o02N, "하늘섬 퀘스트 3형제" 중 3번) — 이동속도 0,
// 맵에 좌표 하나로 고정 배치되는 표적이다. 웨이브가 아니라 게임 시작에 한 번만 세운다
// (PM 지시, 2026-09-05) — SealSpawner와 비슷한 모양이지만 단계 전환이 없어서 더 단순하다.
//
// ⚠️ 하늘섬 3형제(sky_1 파괴물 "황금 조각" 5만 · sky_2 파괴물 "등불" 650만 · sky_3 이 유닛
// 3600만)는 체인이 아니라 각각 독립이다(리서치담당 확인) — 조건은 "플레이어 생존" 하나뿐이고
// 순서를 정하는 건 이름이 아니라 체력이다. 1·2는 발동 조건이 없어서가 아니라 **이번 범위가
// 3번뿐이라서** 안 만들었다(PM 지시) — 나중에 만들 때 이 클래스를 그대로 복제하면 된다.
//
// 처치 보상(전 플레이어: 골드 3,000 + 흔함선택위습 1기 + 세이브 플레이포인트 1)이 EnemyData로
// 표현 못 하는 다단 지급이라, PirateQuestManager와 같은 이유로 RewardDistributor.
// GrantKillReward 표준 파이프라인을 안 타고 이 스크립트가 직접 지급한다 — 그래서 EnemyData는
// goldReward=0/resourceRewards 비움으로 둔다(PirateQuestManager 클래스 주석과 같은 관례).
//
// ⚠️ 원작 보상 중 둘은 안 옮겼다(PM 확인, 2026-09-05) — **1/6 확률 ItemGet**(아이템 계통이
// 우리에 없다)과 **보유 유닛 전원에게 영웅경험치 325**(경험치 축 자체가 없다, UnitData.cs의
// "아군 유닛엔 체력·경험치 개념이 없다" 주석과 같은 이유). 지어내지 않고 뺐다.
//
// ⚠️ Enemy_거대해왕류.asset은 isBoss=false다 — 원작 upoi(포인트값)=200으로 "보스"지만,
// 우리 isBoss는 **라운드보스 보상·도박소 해금 경로**를 타는 스위치라 뜻이 다르다(round=0인
// 이 유닛에 그 경로를 타게 하면 아무 라운드에도 안 걸려 조용히 no-op만 될 뿐이라 굳이 켤
// 이유가 없다). 원작의 진짜 의도(보스는 %체력 비례 스킬피해를 안 받는다, war3map.j 게이트)는
// 이 유닛에서 EnemyData.takesPercentDamage=false로 정확히 옮겼다 — 두 개념을 안 섞었다.
public class SeaKingSpawner : MonoBehaviour
{
    [SerializeField] EnemyData seaKingData;

    // 원작 처치 보상의 위습 몫 — 흔함선택위습. RewardDistributor.GrantWisps로 지급한다.
    [SerializeField] WispData rewardWisp;

    const int RewardGold = 3000;
    const int RewardWispCount = 1;

    // ⚠️ 2026-09-11 추가(PM 지시, 원문 직접 대조) — 원작 o02N은 Player(5) 소유
    // (CreateUnitsForPlayer5, war3map_new.j:13434)이고 upgr에 R01A~R01E가 걸려 있는데,
    // 모드 선택 시 여섯 모드 전부 그중 하나를 Player(5)에 연구한다(각 +50%, rhpo
    // gba1=0.5) — 즉 실제 체력은 36,000,000(=EnemyData.hp, uhpm 그대로)이 아니라
    // 36,000,000 × 1.5 = 54,000,000이다. EnemyData.hp는 "원작 uhpm 그대로"라는 규칙을
    // 지키려고 자산은 안 고치고 여기서 곱한다(EnemyDummy.Initialize(EnemyData,
    // startHpMultiplier)가 이미 있는 자리 — 신세계 사이드보스 §⑧과 같은 메커니즘).
    const float HpMultiplier = 1.5f;

    // war3map.j Trig_Quest_sky_3 — PersistentSave.AddSessionPoints 코멘트의 "Quest_sky_1/2/3
    // +1씩(3곳)" 중 3번째 — 이 메서드의 첫 실제 호출부다.
    const int RewardSessionPoints = 1;

    GameObject current;

    void Start()
    {
        if (!GameAuthority.IsServer) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (!Spawn()) yield break;

        // current는 UnityEngine.Object의 == null 오버로드를 탄다 — Destroy() 직후부터
        // (실제 파괴가 처리되는 프레임 끝보다 먼저) true가 된다(PirateQuestManager.RunQuest와
        // 같은 패턴).
        yield return new WaitUntil(() => current == null);

        GrantReward();
    }

    bool Spawn()
    {
        if (seaKingData == null || seaKingData.prefab == null)
        {
            Debug.LogWarning($"{name}: 거대 해왕류 데이터나 prefab이 비어있어 스폰하지 못했습니다.", this);
            return false;
        }

        current = Instantiate(seaKingData.prefab, transform.position, Quaternion.identity);

        if (current.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(seaKingData, HpMultiplier);
            // 퀘스트류라 레인 소속이 없다 — 크립·해적단 미니보스와 같은 이유
            // (레인 카운트·패배판정에 안 섞이게, 보상도 레인 주인이 아니라 이 스크립트가 직접).
            dummy.SetLane(-1);
        }

        if (current.TryGetComponent(out WaypointMover mover))
        {
            mover.enabled = false; // 맵에 고정된 표적 — 순찰하지 않는다
        }

        return true;
    }

    // 원작 처치 보상은 전 플레이어에게 간다(물범류·크립과 같은 축) — 골드·위습·세이브
    // 포인트 셋 다 마찬가지다.
    void GrantReward()
    {
        List<WispReward> wisp = rewardWisp != null
            ? new List<WispReward> { new WispReward { wisp = rewardWisp, count = RewardWispCount } }
            : null;

        foreach (PlayerContext context in PlayerContext.Occupied)
        {
            context.GoldWallet?.Add(RewardGold);
            context.PersistentSave?.AddSessionPoints(RewardSessionPoints);

            if (wisp != null && RewardDistributor.Instance != null)
                RewardDistributor.Instance.GrantWisps(context, wisp);

            PlayerNotification.Show(context.PlayerId, "거대 해왕류를 처치했습니다!");
        }
    }
}
