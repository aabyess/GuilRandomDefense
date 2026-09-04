using System.Collections.Generic;
using UnityEngine;

// 판매 포탈: 목록에 있는 퀘스트 토큰이 들어오면 소모하고 그 해적단 퀘스트를 발동한다.
// UnitPortal(위습을 소모해 유닛을 준다)과 짝이 되는 구조 — 여기는 유닛을 소모해 퀘스트를 연다.
// war3map.j의 진짜 EVENT_PLAYER_UNIT_SELL 계열(익명 토큰, 이름 매핑 불필요 — 확인 완료)을 옮긴 것.
//
// 퀘스트 하나당 필드 하나였던 과거 설계면 7퀘스트 × 4레인 = 포탈 28개가 맵에 서야 한다.
// 대신 포탈이 퀘스트 목록을 들고, 들어온 유닛의 sellUnit으로 어느 퀘스트인지 스스로 고른다 —
// 레인당 포탈 1개면 된다(MapGenerator가 그렇게 배치한다).
[RequireComponent(typeof(Collider))]
public class UnitSellPortal : MonoBehaviour
{
    [SerializeField] List<PirateQuestData> quests = new List<PirateQuestData>();

    static PirateQuestManager Manager => PirateQuestManager.Instance;

    void OnTriggerEnter(Collider other)
    {
        if (!GameAuthority.IsServer) return;
        if (!other.TryGetComponent(out UnitIdentity identity)) return;

        // 목록 어디에도 안 맞는 유닛 — 이 포탈이 볼 대상이 아니다. 소모하지 않고 그냥 통과시킨다.
        // (라운드 제한에 걸린 것과는 다르다 — 아래에서 가른다.)
        PirateQuestData quest = FindQuestFor(identity.Data);
        if (quest == null) return;

        int round = Manager != null ? Manager.CurrentRound : 0;
        bool inRange = (quest.minRound <= 0 || round >= quest.minRound)
                     && (quest.maxRound <= 0 || round <= quest.maxRound);

        // 원작은 "재고 자체가 라운드로 잠긴다"(예: 와포루 토큰은 31라운드에 상점 재고에서
        // 제거된다) — 그래서 판매(소모)가 조건과 무관해도 안전하다, 창 밖에서 애초에 못 들고
        // 있으니까. 우리는 그 재고 게이트를 없애고 게임 시작에 7종 전부를 무상 지급하는 쪽으로
        // 단순화했다(PirateQuestManager.GrantStartingTokens) — 그러면 모든 플레이어가 1라운드부터
        // 항상 "창 밖에 토큰을 들고 있는" 상태가 되고, 라운드 검사가 유일한 방어선이 된다.
        // 그 방어선을 파괴적으로(소모하며) 만들면, 처음 해보는 플레이어가 라운드를 모르고
        // 한 번 넣어봤다가 그 퀘스트를 영영 잃는다 — 되돌릴 방법도, 콘솔 로그 말고는 알려주는
        // 것도 없다. 그래서 라운드가 안 맞으면 소모하지 않고 그냥 돌려보낸다.
        if (!inRange)
        {
            Debug.Log($"[해적단] {quest.questName}: 지금은 발동 가능 라운드({quest.minRound}~{quest.maxRound})가 " +
                      $"아니라({round}라운드) 열리지 않습니다. 토큰은 소모되지 않았습니다.", this);
            return;
        }

        int ownerId = other.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        // 여기서부터는 발동 조건을 다 통과했다 — 매니저가 없어 못 여는 것은 플레이어의 선택이
        // 아니라 우리 배선 오류이므로, 그 경우는 계속 소모한다(원작처럼 "판 것은 사라진다"만
        // 지킨다). 라운드 게이트와 성격이 다르다.
        identity.Consume();

        if (Manager == null)
        {
            Debug.LogWarning($"UnitSellPortal: PirateQuestManager가 없어 {quest.questName} 퀘스트를 시작하지 못했습니다 (유닛은 이미 소모됨).", this);
            return;
        }

        Manager.StartQuest(quest, ownerId);
    }

    PirateQuestData FindQuestFor(UnitData sold)
    {
        if (sold == null) return null;

        foreach (PirateQuestData quest in quests)
        {
            if (quest != null && quest.sellUnit == sold) return quest;
        }
        return null;
    }
}
