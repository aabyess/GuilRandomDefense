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
    // 원작 도움소 마나 회복 공식(20 + 라운드×1.5)이 정수배가 아니라 float로 뒀다 —
    // 최종 지급량은 아래서 반올림한다.
    [SerializeField] float perRound;
    // 원작 금화 포탈은 고정값이 아니라 **범위**다 — 15 + 라운드×12~35. 골드포탈은 원작
    // 최대 수입원이라(75라운드 누적 상한 13.6만 > 스토리 6.9만 > 처치 3.3만) 그 폭이
    // 곧 판의 기복이다. 고정값으로 뭉개면 원작의 "위습을 골드로 돌릴까" 도박성이 사라진다.
    // perRound 이하면 범위가 없는 것으로 보고 perRound 하나만 쓴다(목재·마나 포탈이 그렇다).
    [SerializeField] float perRoundMax;
    [SerializeField, Range(0f, 100f)] float successChancePercent = 100f;

    RoundManager roundManager;
    readonly HashSet<Wisp> loggedRejectionFor = new HashSet<Wisp>();

    void Awake()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
    }

    // ⚠️ 2026-09-05: MapGenerator.BuildResourcePortal이 SerializedObject(enumValueIndex)로
    // acceptedGrades를 채웠는데 씬 파일에 한 번도 안 들어갔다(사장님 재생성 확인, PM 재조사).
    // UnitPortal도 같은 증상이라 SerializedProperty 경로 자체를 의심해 이 메서드로 우회했다 —
    // 씬 오브젝트라 프리팹 오버라이드 문제가 없다. 호출부가 EditorUtility.SetDirty를 반드시
    // 같이 불러야 한다(직접 대입은 자동으로 dirty 표시가 안 된다).
    public void SetAcceptedGrade(UnitGrade grade)
    {
        acceptedGrades.Clear();
        acceptedGrades.Add(grade);
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

        int ownerId = wisp.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        // ⚠️ 플레이어가 보고 행동을 바꿀 수 있는 실패(안 받는 위습·도박 실패)는
        // PlayerNotification으로 띄운다 — 그전엔 Debug.Log뿐이라 콘솔에만 남고 화면엔
        // "넣었는데 아무 일도 안 일어남"으로 보였다(PM 지시, 2026-09-05).
        if (!Accepts(wisp.Data.targetGrade))
        {
            if (loggedRejectionFor.Add(wisp))
                PlayerNotification.Show(ownerId, $"{wisp.Data.wispName}은(는) 이 포탈에 쓸 수 없습니다.");
            return;
        }

        PlayerContext context = PlayerContext.Get(ownerId);
        if (context == null)
        {
            // 플레이어를 못 찾는 건 배선 오류지 플레이어의 선택이 아니다 — Debug.Log에 남긴다.
            Debug.LogWarning($"{name}: 플레이어 {ownerId}를 찾지 못해 지급하지 못했습니다.", this);
            return;
        }

        // 확률에 실패해도 위습은 소모된다 — 원작의 "66% 확률로 목재 1획득"이 그런 구조다.
        wisp.MarkConsumed();
        Destroy(wisp.gameObject);

        if (Random.Range(0f, 100f) >= successChancePercent)
        {
            // 원작 문구 그대로("목재도박에 실패하였습니다") — 목재 도박(66%)만 실제로
            // 이 분기를 탄다(골드·마나는 100%). 다른 자원이 나중에 확률부로 바뀌어도
            // 말이 되게 일반형을 폴백으로 둔다.
            string failMessage = payout == Payout.Resource && resourceType == ResourceType.Wood
                ? "목재도박에 실패하였습니다."
                : $"{(payout == Payout.Gold ? "골드" : resourceType.ToString())} 획득에 실패했습니다.";
            PlayerNotification.Show(ownerId, failMessage);
            return;
        }

        int round = roundManager != null ? roundManager.CurrentRound : 1;
        float scale = perRoundMax > perRound ? Random.Range(perRound, perRoundMax) : perRound;
        int amount = Mathf.Max(0, Mathf.RoundToInt(baseAmount + scale * round));
        string payoutLabel = payout == Payout.Gold ? "골드" : resourceType.ToString();

        if (payout == Payout.Gold)
            context.GoldWallet?.Add(amount);
        else
            context.ResourceWallet?.Add(resourceType, amount);

        // amount==0은 위습이 이미 소모된 뒤(81-82줄)라 알림 없이 return하면 자원(위습)만
        // 사라진 것처럼 보인다 — 성공 지급도 실패 메시지(92줄)처럼 화면에 알려야 한다
        // (PM 지시 2026-09-05). 0이든 아니든 같은 자리에서 처리하면 분기가 하나로 끝난다.
        PlayerNotification.Show(ownerId, $"{payoutLabel} {amount} 획득!");
        Debug.Log($"{name}: 플레이어 {ownerId}에게 {payoutLabel} {amount} 지급.");
    }
}
