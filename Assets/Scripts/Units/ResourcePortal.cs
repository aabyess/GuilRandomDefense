using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 위습을 넣으면 유닛 대신 자원을 주는 포탈. 목재·금화 칸이 여기 해당한다.
/// UnitPortal과 나란히 쓰이며, 지급 대상만 다르다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ResourcePortal : MonoBehaviour
{
    public enum Payout { Gold, Resource }

    [SerializeField] List<UnitGrade> acceptedGrades = new List<UnitGrade>();
    [SerializeField] Payout payout = Payout.Resource;
    [SerializeField] ResourceType resourceType = ResourceType.Wood;

    [Header("지급량 — 라운드가 오를수록 늘어난다")]
    [SerializeField] int baseAmount = 1;
    [SerializeField] int perRound;
    [SerializeField, Range(0f, 100f)] float successChancePercent = 100f;

    RoundManager roundManager;
    readonly HashSet<Wisp> loggedRejectionFor = new HashSet<Wisp>();

    void Awake()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
    }

    bool Accepts(UnitGrade grade)
    {
        return acceptedGrades == null || acceptedGrades.Count == 0 || acceptedGrades.Contains(grade);
    }

    // TODO(멀티): UnitPortal과 같은 이유로 서버 권위로 옮겨야 한다.
    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out Wisp wisp)) return;
        if (wisp.IsConsumed || wisp.Data == null) return;

        if (!Accepts(wisp.Data.targetGrade))
        {
            if (loggedRejectionFor.Add(wisp))
                Debug.Log($"{name}: 이 포탈이 받지 않는 위습입니다 ({wisp.Data.wispName}).");
            return;
        }

        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;
        PlayerContext context = PlayerContext.Get(ownerId);
        if (context == null)
        {
            Debug.LogWarning($"{name}: 플레이어 {ownerId}를 찾지 못해 지급하지 못했습니다.", this);
            return;
        }

        // 확률에 실패해도 위습은 소모된다 — 원작의 "66% 확률로 목재 1획득"이 그런 구조다.
        wisp.MarkConsumed();
        Destroy(wisp.gameObject);

        if (Random.Range(0f, 100f) >= successChancePercent)
        {
            Debug.Log($"{name}: 지급에 실패했습니다 (확률 {successChancePercent}%).");
            return;
        }

        int round = roundManager != null ? roundManager.CurrentRound : 1;
        int amount = Mathf.Max(0, baseAmount + perRound * round);
        if (amount == 0) return;

        if (payout == Payout.Gold)
            context.GoldWallet?.Add(amount);
        else
            context.ResourceWallet?.Add(resourceType, amount);

        Debug.Log($"{name}: 플레이어 {ownerId}에게 {(payout == Payout.Gold ? "골드" : resourceType.ToString())} {amount} 지급.");
    }
}
