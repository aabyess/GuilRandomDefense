using UnityEngine;

// 원작 "채팅 코드 입력" 유닛 획득 하나(war3map.j Trig_Eternal_*/Trig_Forever_*/Trig_IM_*
// 대응, Docs/reference/ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv 70행 중 Eternal·Forever·IM
// 47종 + Nika 1종 담당 — Hidden 23종은 채팅이 아니라 재료 조합이라 여기 안 담는다).
//
// ⚠️ 사장님이 유닛별 배정은 나중에 직접 준다고 하셔서 에셋은 만들지 않았다. 여기 스키마도
// "무엇을 담을 수 있는가"만 맞췄다 — 수치는 지어내지 않았다.
public enum ChatUnlockCategory
{
    // 목재0, 47개가 공유하는 1회 게이트(ChatUnlockManager.HasClaimedShared)를 쓴다.
    Eternal,
    // 목재5 + 세이브 누적 조건(requiredSaveCount) + 같은 공유 게이트.
    Forever,
    // 목재10 + 같은 공유 게이트.
    Immortal,
    // Eternal 계열 안의 예외 하나(Trig_Eternal_Nika) — 공유 게이트를 안 쓰고 별도 전제
    // 조건(원작 udg_Nika_Johab_Bool+udg_Nika_Item_Bool, 우리 쪽엔 대응 퀘스트가 없음)을 쓴다.
    Nika,
}

[CreateAssetMenu(fileName = "NewChatUnlockData", menuName = "GuilRandomDefense/Chat Unlock Data")]
public class ChatUnlockData : ScriptableObject
{
    public string displayName;
    public ChatUnlockCategory category;

    // 원작은 영문·한글 문구 둘 다 받는다(CSV "채팅문구" 열이 항상 "영문 / 한글" 쌍이다).
    // 예: "zoro tr" / "최강의검사".
    public string phraseEnglish;
    public string phraseKorean;

    // 원작 실측(war3map.j Trig_*_Conditions/Actions, AdjustPlayerStateBJ 호출 직접 확인):
    // Eternal=0, Forever=5, Immortal=10, Nika=5. 골드 비용은 세 계열 다 0(차감 호출 자체가 없음).
    public int woodCost;

    // Forever 전용 — 원작 "게임 클리어 누적 횟수"(CombineRecipe.requiredSaveCount와 같은 개념·
    // 같은 미구현 상태). 0이면 조건 없음.
    public UnitData result;
    public int requiredSaveCount;
}
