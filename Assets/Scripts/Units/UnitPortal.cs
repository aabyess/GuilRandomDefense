using System.Collections.Generic;
using UnityEngine;

// 포탈에 허용된 등급의 위습이 들어오면 유닛을 지급하고 위습을 소모한다.
// 포탈 오브젝트에 Collider(isTrigger = true)가 필요하다.
[RequireComponent(typeof(Collider))]
public class UnitPortal : MonoBehaviour, ISerializationCallbackReceiver
{
    // 비어 있으면 모든 등급 허용.
    [SerializeField] List<UnitGrade> acceptedGrades = new List<UnitGrade>();

    [SerializeField] UnitData specificUnit; // 선택형 포탈(흔함 등)이면 지정, 비어있으면 등급 내 랜덤 지급

    // 받는 위습의 등급과 지급하는 유닛의 등급이 다른 포탈이 있다 —
    // 자원 칸 북쪽은 "랜덤유닛 위습"을 받아 "흔함 유닛"을 준다.
    // 끄면 예전처럼 위습 등급 그대로 뽑는다.
    [SerializeField] bool overrideRewardGrade;
    [SerializeField] UnitGrade rewardGrade;
    // 원작의 "희귀함, 특수함(3%확률) 등급유닛 전체 랜덤" 같은 칸을 위한 것.
    // 낮은 확률로 지정한 등급에서 대신 뽑는다. 0이면 쓰지 않는다.
    [SerializeField] UnitGrade bonusGrade;
    // 보너스가 특정 유닛일 때 쓴다(랜덤 포탈의 1% 상붕카). 비어 있으면 bonusGrade에서 뽑는다.
    [SerializeField] UnitData bonusUnit;
    [SerializeField, Range(0f, 100f)] float bonusChancePercent;

    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] Transform spawnPoint;   // 비워두면 위습 주인의 레인 한가운데에 소환한다

    // 마이그레이션 전용 필드 — acceptedGrade(단일 등급) → acceptedGrades(리스트) 전환 전에 씬에 저장된
    // 값을 흡수하기 위해 이름·타입을 그대로 남겨뒀다. 새로 만드는 포탈은 이 필드를 쓰지 말고
    // acceptedGrades를 직접 채울 것 — 비워두면 최초 직렬화 시 이 필드의 기본값(0)으로 한 번 채워진다.
    [SerializeField, HideInInspector] UnitGrade acceptedGrade;
    [SerializeField, HideInInspector] bool legacyGradeMigrated;

    readonly HashSet<Wisp> loggedRejectionFor = new HashSet<Wisp>();

    public void OnAfterDeserialize()
    {
        if (legacyGradeMigrated) return;
        legacyGradeMigrated = true;

        if (acceptedGrades == null)
        {
            acceptedGrades = new List<UnitGrade>();
        }

        if (acceptedGrades.Count == 0)
        {
            acceptedGrades.Add(acceptedGrade);
        }
    }

    public void OnBeforeSerialize() { }

    bool Accepts(UnitGrade grade)
    {
        return acceptedGrades == null || acceptedGrades.Count == 0 || acceptedGrades.Contains(grade);
    }

    // isBonus: 이 결과가 bonusChancePercent 확률(1% 상붕카 등)에서 나왔는지. 이름으로
    // "상붕카인지" 판단하지 않고(사장님이 유닛 이름을 자주 바꾼다) 어느 뽑기 경로를 탔는지로
    // 직접 표시한다 — 호출부가 이 값으로 자리(우리 vs 레인 한가운데)를 정한다.
    UnitData RollReward(UnitGrade grade, out bool isBonus)
    {
        isBonus = false;
        if (gachaTable == null) return null;

        if (bonusChancePercent > 0f && Random.Range(0f, 100f) < bonusChancePercent)
        {
            UnitData bonus = bonusUnit != null ? bonusUnit : gachaTable.RollFromGrade(bonusGrade);
            // 보너스 등급 풀이 비어 있어도 뽑기 자체가 실패하면 안 된다 — 원래 등급으로 넘어간다.
            if (bonus != null) { isBonus = true; return bonus; }
        }

        return gachaTable.RollFromGrade(grade);
    }

    // 뽑기 섬에서 소환하면 지상 유닛은 바다에 막혀 레인까지 갈 수 없다.
    // 위습을 가져온 플레이어의 레인에 내보낸다 — 보통은 유닛 우리(칸 배정), 단 보너스로 나온
    // 유닛(1% 상붕카 등)은 우리 칸에 끼워 넣지 않고 레인 한가운데에 세운다(사장님 지시,
    // 2026-09-03) — 흔치 않은 만큼 눈에 띄어야 한다는 의도로 읽었다.
    Vector3 ResolveSpawnPosition(int ownerId, UnitData reward, bool isBonus)
    {
        if (spawnPoint != null) return spawnPoint.position;

        LaneMarker lane = LaneMarker.Get(ownerId);
        if (lane != null) return isBonus ? lane.LaneCenter : lane.TakeSpawnPosition(reward);

        Debug.LogWarning($"UnitPortal: 플레이어 {ownerId}의 레인을 찾지 못해 포탈 자리에 소환합니다.", this);
        return transform.position;
    }

    // TODO(멀티): 포탈 진입 판정·지급은 서버 권위로 이동해야 함 — 지금은 클라이언트가 직접 처리.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out Wisp wisp)) return;
        if (wisp.IsConsumed) return;
        if (wisp.Data == null) return;

        UnitGrade grade = wisp.Data.targetGrade;

        if (!Accepts(grade))
        {
            if (loggedRejectionFor.Add(wisp))
            {
                string allowed = acceptedGrades.Count == 0 ? "모든" : string.Join(", ", acceptedGrades);
                Debug.Log($"UnitPortal: 이 포탈은 {allowed} 등급만 받습니다 ({wisp.Data.wispName} 거부).");
            }
            return;
        }

        bool isBonusReward = false;
        UnitData reward = specificUnit != null
            ? specificUnit
            : RollReward(overrideRewardGrade ? rewardGrade : grade, out isBonusReward);

        if (reward == null)
        {
            Debug.LogWarning($"UnitPortal: {grade} 등급에서 지급할 유닛을 찾지 못해 위습을 소모하지 않았습니다.");
            return;
        }

        // 소환할 수 있는지까지 확인한 뒤에 위습을 소모한다.
        // 예전엔 소환에 실패해도 인벤토리에는 들어가서 완전한 손실은 아니었는데, 인벤토리가
        // 필드 인스턴스의 등록부가 된 뒤로는 소환이 곧 지급이다 — 여기서 빠지면 위습만 사라진다.
        if (unitSpawner == null)
        {
            Debug.LogWarning("UnitPortal: unitSpawner가 비어있어 소환하지 못했습니다 — 위습을 소모하지 않았습니다.", this);
            return;
        }

        if (reward.prefab == null)
        {
            Debug.LogWarning($"UnitPortal: {reward.unitName}에 prefab이 없어 소환하지 못했습니다 — 위습을 소모하지 않았습니다.", this);
            return;
        }

        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        wisp.MarkConsumed();
        Destroy(wisp.gameObject);

        // Spawn이 인벤토리 등록까지 한다 — 여기서 따로 Add하면 필드에 없는 유닛이 인벤토리에 생긴다.
        unitSpawner.Spawn(reward, ResolveSpawnPosition(ownerId, reward, isBonusReward), ownerId);
    }
}
