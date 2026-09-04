using UnityEngine;

// 판매 포탈: 지정된 유닛(퀘스트 토큰)이 들어오면 소모하고 해적단 퀘스트를 발동한다.
// UnitPortal(위습을 소모해 유닛을 준다)과 짝이 되는 구조 — 여기는 유닛을 소모해 퀘스트를 연다.
// war3map.j의 진짜 EVENT_PLAYER_UNIT_SELL 계열(익명 토큰, 이름 매핑 불필요 — 확인 완료)을 옮긴 것.
[RequireComponent(typeof(Collider))]
public class UnitSellPortal : MonoBehaviour
{
    [SerializeField] PirateQuestData quest;

    static PirateQuestManager Manager => PirateQuestManager.Instance;

    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (quest == null || quest.sellUnit == null) return;
        if (!other.TryGetComponent(out UnitIdentity identity)) return;
        if (identity.Data != quest.sellUnit) return;

        int round = Manager != null ? Manager.CurrentRound : 0;
        if (quest.minRound > 0 && round < quest.minRound) return;
        if (quest.maxRound > 0 && round > quest.maxRound) return;

        int ownerId = other.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        // 판매(소모)는 발동 가능 여부와 무관하게 먼저 확정한다 — 확인부터 위에서 끝냈으니
        // 여기 도달했으면 반드시 발동한다. 소모 뒤에 매니저가 없어 못 열더라도,
        // 원작처럼 "판 유닛은 사라진다"만은 지킨다.
        identity.Consume();

        if (Manager == null)
        {
            Debug.LogWarning($"UnitSellPortal: PirateQuestManager가 없어 {quest.questName} 퀘스트를 시작하지 못했습니다 (유닛은 이미 소모됨).", this);
            return;
        }

        Manager.StartQuest(quest, ownerId);
    }
}
