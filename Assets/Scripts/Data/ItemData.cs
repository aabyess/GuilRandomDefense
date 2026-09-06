using UnityEngine;

// 2026-09-06 정정(PM 지시): 위 판단이 뒤집혔다. 그때는 원작 아이템 후보를 3개(고대의 배
// 아이템 I00S·레일리 히든 아이템·항법 선택 시스템)만 알고 있었는데, 리서치담당이
// `Trig_item_Gemble_Actions`(시전유닛 H0BS "메타몽" 판매)를 아이템 도박의 정체로 찾아내며
// 원작 아이템 38종 전체를 전수했다(ITEM_POOL_FULL_CENSUS.md). 이 파일은 그 38종 중 원작에서
// 확정된 필드만 채운다 — 아래 필드 목록 참고. 획득 경로(도박·드랍·해금 배선)는 이번 범위
// 밖이다(PM 지시) — 데이터만 서 있고 아직 아무것도 안 돈다.
//
// (2026-09-06 첫 판단, 참고용으로 남긴다: "이 클래스와 ItemInventory는 죽은 코드가 아니다 —
// CombineSystem.cs가 IngredientKind.SpecificItem 재료를 실제로 소모하는 경로를 갖고 있고,
// itemInventory가 비어 있으면 안전하게 경고만 내고 막는다. 다만 지금은 채울 콘텐츠가 없어
// 비워둔다." 이 결론 중 "죽은 코드가 아니다"는 그대로 유효하다 — 콘텐츠가 없다는 전제만
// 틀렸다. 고대의 배 "유닛" h05Y는 이것과 무관 — ANCIENT_SHIP_SPEC_2026-09-06.md로 이미
// 특수 유닛으로 확정돼 있다.)
[CreateAssetMenu(fileName = "NewItemData", menuName = "GuilRandomDefense/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;

    // ⚠️ 원작 등급 표기가 있는 아이템만 켠다(예: "명검-흑도 슈스이(전설적인)") — 38종 중
    // 상당수("풀 밖" 항목 대부분)는 등급 표기 자체가 없다. hasGrade=false면 grade 값은
    // 의미 없다(UnitGrade.Common 기본값으로 조용히 "흔함"으로 읽히면 안 되므로 별도 플래그로
    // 뗀다) — 등급 없는 아이템에 등급을 지어내지 않는다.
    public bool hasGrade;
    public UnitGrade grade;

    // 원작 툴팁/설명 원문 그대로(ITEM_POOL_FULL_CENSUS.md §②) — 표시 전용이다.
    // ⚠️ 이 텍스트 안의 숫자(예: "공격력 15% 증가")는 게임 로직이 읽는 데이터가 아니다 —
    // 뿌리 ㉒(오늘 스킬 툴팁 10%가 실제 15%였던 사고)와 같은 함정이라, 수치가 필요하면
    // 반드시 원작 원문(트리거·필드)을 따로 확인해서 별도 필드로 넣어야 한다. 이 필드는
    // 사람이 읽는 설명용일 뿐이다.
    public string tooltipText;

    // 영웅 해금/강화형 8종(I003·I004·I00J·I00L·I00M·I00T·I00U·I010) 중 원작에서 구체적인
    // 연결 능력ID까지 확정된 것만 채운다 — 지금은 I010(황준석_ADAP/Kid_Skill_3/_item,
    // ITEM_POOL_FULL_CENSUS.md §④) 하나뿐이다. 나머지 7종은 툴팁에 산문으로만("시키 불멸
    // 보유시 부유물 발동시 50%확률로 폐함대 추가 낙하" 등) 효과가 적혀 있고 원작 능력ID가
    // 아직 안 나와서 비워둔다 — 지어내지 않는다.
    public string linkedAbilityId;

    // 판매 시 목재 보상. 원작에 정확한 수치가 확인된 건 I010(목재 2~3, 범위)뿐이다.
    // I004는 "판매가능"이라고만 나오고 수량이 안 나와 있어(ITEM_POOL_FULL_CENSUS.md §③)
    // 0/0(미확인)으로 비워둔다 — min==max==0은 "판매 불가"가 아니라 "수량 미확인"이다,
    // 혼동하지 말 것(designNote에 개별로 적어둔다).
    public int sellWoodMin;
    public int sellWoodMax;

    // 원작 조사에서 아직 못 밝힌 부분·이 자산 특유의 메모(구조화된 필드로 못 담는 것).
    // 예: "판매 가능하다는 언급은 있으나 수량 미확인", "풀 밖(도박 풀에 안 들어감)".
    public string designNote;
}
