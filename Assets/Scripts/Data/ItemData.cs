using UnityEngine;

// 2026-09-06 조사(PM 지시, APPROXIMATION_LEDGER.md §8): 이 클래스와 ItemInventory는 죽은
// 코드가 아니다 — CombineSystem.cs가 IngredientKind.SpecificItem 재료를 실제로 소모하는
// 경로를 갖고 있고, itemInventory가 비어 있으면 안전하게 경고만 내고 막는다. 다만 지금은
// 채울 콘텐츠가 없어 비워둔다: 원작 아이템 후보 3개(고대의 배 아이템 I00S·레일리 히든
// 아이템·항법 선택 시스템)가 전부 정체 자체가 원작 조사에서 안 밝혀졌다 — 지어내면 추측
// 배선이 된다. (고대의 배 "유닛" h05Y는 이것과 무관 — ANCIENT_SHIP_SPEC_2026-09-06.md로
// 이미 특수 유닛으로 확정돼 있다.) 정체가 밝혀지면 그때 ItemData 에셋을 만들고
// CombineRecipe에 SpecificItem으로 연결하면 된다 — 자리는 이미 있다.
[CreateAssetMenu(fileName = "NewItemData", menuName = "GuilRandomDefense/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
}
