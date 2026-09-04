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

        int ownerId = other.TryGetComponent(out OwnedByPlayer owner) ? owner.OwnerId : LocalPlayer.LocalPlayerId;

        // 판매(소모)는 발동 가능 여부와 무관하게 확정한다 — 이 유닛이 어느 퀘스트의 토큰인지는
        // 위에서 이미 확인됐다. 원작도 "판매"라는 행위 자체는 트리거 조건과 무관하다(아이템을
        // 팔면 인벤토리에서 즉시 사라지고, 트리거는 그 뒤에 조건을 따로 검사한다) — 라운드가
        // 안 맞아 퀘스트가 안 열려도 토큰은 사라져야 그 재현이 맞다.
        identity.Consume();

        if (!inRange)
        {
            Debug.Log($"[해적단] {quest.questName}: 지금은 발동 가능 라운드({quest.minRound}~{quest.maxRound})가 " +
                      $"아니라({round}라운드) 열리지 않았습니다. 토큰은 소모됐습니다.", this);
            return;
        }

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
