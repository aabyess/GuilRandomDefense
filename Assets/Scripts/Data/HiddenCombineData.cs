using System.Collections.Generic;
using UnityEngine;

// 히든 등급 23종 획득(원작 war3map.j Trig_Hidden_*, CSV Docs/reference/
// ORIGINAL_CHAT_UNLOCK_TRIGGERS.csv "Hidden" 계열). Eternal/Forever/Immortal(ChatUnlockData)과
// 채팅 문구를 받는 모양은 같지만 비용이 골드·목재가 아니라 **재료 유닛**이다 — 원작
// `Trig_Hidden_*_Actions`를 전문 확인한 결과, 재료 존재만 확인하는 게 아니라
// `RemoveUnit`으로 실제로 소모한다(2026-09-05, 구현담당3).
//
// ⚠️ 재료 개수는 트리거마다 다르다(2~5개, 4개 고정 아님) — Akainu 하나만 보고 "4종"으로
// 일반화하면 rebecca(2개)·carrot/perona(5개)에서 깨진다.
[System.Serializable]
public class HiddenCombineIngredient
{
    // 원작 유닛ID(예: "h01T") — 참고용 주석 자리다. 우리 로스터 어느 유닛과 대응하는지는
    // 이름 매핑이 이미 "불가능"으로 닫힌 문제라(name-mapping-impossible) 여기서 안 푼다.
    public string originalUnitId;

    // ⚠️ 사장님이 유닛별 배정을 나중에 직접 준다 — 지금은 비워둔다. 비어 있으면
    // HiddenCombineManager가 그 슬롯을 "절대 못 채움"으로 취급해 조합 자체가 항상 실패한다
    // (조용히 다른 유닛을 대신 넣는 일이 없게).
    public UnitData unit;

    public int count = 1;
}

[CreateAssetMenu(fileName = "NewHiddenCombineData", menuName = "GuilRandomDefense/Hidden Combine Data")]
public class HiddenCombineData : ScriptableObject
{
    public string displayName;

    // 원작이 항상 "영문 / 한글" 쌍으로 등록한다(ChatUnlockData와 같은 관례).
    public string phraseEnglish;
    public string phraseKorean;

    // 참고용(원작 CreateNUnitsAtLoc 대상). 실제 지급 대상은 result — 사장님이 채울 때까지 비움.
    public string originalResultUnitId;
    public UnitData result;

    public List<HiddenCombineIngredient> ingredients = new List<HiddenCombineIngredient>();
}
