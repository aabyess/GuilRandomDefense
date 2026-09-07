using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 크립 섬 하나를 맡는다. **원작은 리스폰 루프가 아니라 3단계 순차 체인이다**:
//
//   1단계 물범  HP   360,000  →  전원에게 1,000골드 + 나무1   → 죽으면 그 자리에 2단계
//   2단계 노루  HP 1,000,000  →  처치자에게 나무7             → 죽으면 그 자리에 3단계
//   3단계 양    HP 1,200,000  →  처치자에게 5,000골드 + 나무1 → 끝
//
// 「전원」과 「처치자만」이 단계마다 갈리는 것도 원작 그대로다 —
// EnemyData의 rewardsAllPlayers / rewardsKillerOnly가 그 자리다.
//
// 옮기지 못한 것 둘(원작에는 있다):
//   · 2단계의 **세이브 플레이포인트 1(처치자 전용, 50/50 두 분기 모두 적용, EVENTS_RESEARCH_
//     NAMED.md 확인)** — ⚠️ 2026-09-07 정정(PM 지시, "필드만·아직 없다" 뼈대 구멍 점검):
//     "세이브 시스템이 아직 없다"는 낡은 이유였다 — PersistentSave(11번)가 이미 있고
//     PersistentSave.AddSessionPoints가 정확히 이 용도의 단일 창구다(SeaKingSpawner.
//     GrantReward가 이미 그 창구를 쓰는 실제 호출부). 다만 SealSpawner는 지금 "누가
//     2단계를 처치했는지"를 안 받는다(ChainRoutine이 WaitUntil(current==null)만 보고
//     killerPlayerId를 안 받는 구조) — **시스템은 있는데 이 지급 하나가 여전히 안
//     옮겨진 상태**다. 컴파일 검증이 막혀있는 지금(Assets/Editor/Tests 정리 전) 킬러
//     추적 배선까지 새로 만드는 건 보류한다 — 다음에 이어서 할 것.
//   · 2단계의 **50% [나무2 + 「해적선」] / 50% [나무7]** 분기 — 해적선에 해당하는 유닛이
//     우리 로스터에 없다. 분기를 남기면 한쪽이 그냥 나쁜 결과가 되므로 **나무7로 통일했다**
//     (PM 판단). 해적선이 생기면 분기를 되살릴 것.
//
// TODO(멀티플레이): 스폰도 서버 권위로 옮겨야 한다 — 지금은 클라이언트가 직접 처리.
public class SealSpawner : MonoBehaviour
{
    [Header("1단계 → 2단계 → 3단계 순서대로")]
    [SerializeField] EnemyData sealData;                       // 1단계 물범
    [SerializeField] List<EnemyData> laterStages = new List<EnemyData>();   // 2·3단계

    GameObject current;
    Coroutine chain;

    void Start()
    {
        if (!GameAuthority.IsServer) return;
        chain = StartCoroutine(ChainRoutine());
    }

    void OnDestroy()
    {
        if (chain != null)
        {
            StopCoroutine(chain);
            chain = null;
        }
    }

    // 단계마다 하나씩, 죽으면 다음. 3단계가 죽으면 이 섬은 끝난다 —
    // **리스폰하지 않는다.** 원작에서 크립은 판당 한 번 도는 콘텐츠다.
    IEnumerator ChainRoutine()
    {
        if (!Spawn(sealData)) yield break;
        yield return new WaitUntil(() => current == null);

        foreach (EnemyData stage in laterStages)
        {
            if (!Spawn(stage)) yield break;
            yield return new WaitUntil(() => current == null);
        }
    }

    bool Spawn(EnemyData data)
    {
        if (!GameAuthority.IsServer) return false;

        if (data == null || data.prefab == null)
        {
            Debug.LogWarning($"{name}: 크립 단계 데이터나 prefab이 비어있어 스폰하지 못했습니다.", this);
            return false;
        }

        current = Instantiate(data.prefab, transform.position, Quaternion.identity);

        if (current.TryGetComponent(out EnemyDummy dummy))
        {
            dummy.Initialize(data);
            // 크립은 레인 몹이 아니다 — 레인 번호가 붙으면 패배 판정(가장 붐비는 레인 기준)에
            // 섞여 들어간다. 그래서 보상도 레인 주인이 아니라 처치자에게 간다
            // (EnemyData.rewardsKillerOnly).
            dummy.SetLane(-1);
        }

        if (current.TryGetComponent(out WaypointMover mover))
        {
            mover.enabled = false;
        }

        return true;
    }
}
