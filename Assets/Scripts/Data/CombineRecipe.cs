using System.Collections.Generic;
using UnityEngine;

public enum IngredientKind
{
    SpecificUnit,
    SpecificItem,
    UnitGradeWildcard,
}

[System.Serializable]
public class RecipeIngredient
{
    public IngredientKind kind = IngredientKind.SpecificUnit;

    public UnitData unit;          // kind == SpecificUnit
    public ItemData item;          // kind == SpecificItem
    public UnitGrade wildcardGrade; // kind == UnitGradeWildcard — 이 등급이면 어떤 유닛이든 인정 (예: "랜덤전용유닛 1기")

    public int count = 1;
}

[System.Serializable]
public class RecipeResourceCost
{
    public ResourceType type;
    public int amount;
}

[CreateAssetMenu(fileName = "NewCombineRecipe", menuName = "GuilRandomDefense/Combine Recipe")]
public class CombineRecipe : ScriptableObject
{
    public string commandId;
    public UnitData result;
    public List<RecipeIngredient> ingredients;
    public int goldCost;
    public List<RecipeResourceCost> resourceCosts;

    // 라운드 조건. 0이면 제한 없음. 예: "41라운드 이전에만 조합 가능" → maxRound = 40.
    public int minRound;
    public int maxRound;

    // 영원 등급 전용. Save = 게임 클리어 누적 횟수 (세이브 기능 미구현).
    // TODO: 세이브 시스템 추가되면 CombineSystem에서 이 값을 검사하도록 연결할 것. 지금은 구조만 반영.
    public int requiredSaveCount;
}
