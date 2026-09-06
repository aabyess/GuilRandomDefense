using UnityEngine;

// 원작 "채팅 코드 입력" 유닛 획득 하나(war3map.j Trig_Eternal_*/Trig_Forever_*/Trig_IM_*
// 대응, Docs/reference/ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv 70행 중 Eternal·Forever·IM
// 47종 + Nika 1종 담당 — Hidden 23종은 채팅이 아니라 재료 조합이라 여기 안 담는다).
//
// ⚠️ 사장님이 유닛별 배정은 나중에 직접 준다고 하셔서 에셋은 만들지 않았다. 여기 스키마도
// "무엇을 담을 수 있는가"만 맞췄다 — 수치는 지어내지 않았다.
public enum ChatUnlockCategory
{
    // ⚠️ 이름은 트리거 이름(Trig_Eternal_*)일 뿐, 실제 등급은 "초월함"이다(전설이 아니다 —
    // upro에 "- 초월함"으로 적혀 있음, 사장님 지적 2026-09-05). "전설 유닛 코드" 같은 UI
    // 문구를 여기서 다시 만들지 말 것 — 등급 이름을 UI에 직접 박으면 계열이 늘 때 또 틀린다.
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

    // Forever 전용 — 원작 "게임 클리어 누적 횟수"(CombineRecipe.requiredSaveCount와 같은 개념,
    // PersistentSave.Data.cumulativeClearCount로 검사한다 — ChatUnlockManager.CanUnlock 참고).
    // 0이면 조건 없음.
    public UnitData result;
    public int requiredSaveCount;

    // ⚠️ 맨 뒤에 추가(2026-09-06, CHAT_UNLOCK_70_SIDE_EFFECT_SWEEP.md, 리서치담당 전수) —
    // 원작 udg_Damage_level_Fixed[플레이어](DamageLevelFixedState 참고, PlayerContext에
    // 부착)에 영구 누적되는 보너스. 채팅언락 70행 전수 결과 이 변수를 건드리는 건 정확히
    // 3개뿐이었다("Eternal 47개가 같은 게이트를 공유하니 더 있을 것"은 기우로 확정됨):
    //   Hidden_Aokiji +2 (HiddenCombineData 쪽, 히든_성탄.asset에 반영)
    //   Eternal_Lucci +2 · IM_dragon +4 (이 필드 — 아직 에셋 없음, 47종 전부 사장님 유닛배정
    //   대기 상태라 원래 없는 게 정상. 에셋이 생기면 이 값을 그대로 채울 것, 지어내지 말 것)
    // 기본값 0 — 다른 44종은 전부 이 값을 안 건드리므로 회귀 없음.
    public int damageLevelFixedBonus;
}
