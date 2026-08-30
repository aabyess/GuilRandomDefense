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
    [SerializeField] GachaTable gachaTable;
    [SerializeField] UnitSpawner unitSpawner;
    [SerializeField] Transform spawnPoint;

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

        UnitData reward = specificUnit != null
            ? specificUnit
            : gachaTable != null ? gachaTable.RollFromGrade(grade) : null;

        if (reward == null)
        {
            Debug.LogWarning($"UnitPortal: {grade} 등급에서 지급할 유닛을 찾지 못해 위습을 소모하지 않았습니다.");
            return;
        }

        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        wisp.MarkConsumed();
        Destroy(wisp.gameObject);

        PlayerContext.Get(ownerId)?.UnitInventory?.Add(reward);

        if (unitSpawner != null)
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            unitSpawner.Spawn(reward, position, ownerId);
        }
        else
        {
            Debug.LogWarning("UnitPortal: unitSpawner가 비어있어 필드에 소환하지 못했습니다.");
        }
    }
}
