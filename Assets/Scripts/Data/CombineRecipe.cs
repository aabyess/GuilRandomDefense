using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RecipeIngredient
{
    public UnitData unit;
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

    // 영원 등급 전용. Save = 게임 클리어 누적 횟수 (세이브 기능 미구현).
    // TODO: 세이브 시스템 추가되면 CombineSystem에서 이 값을 검사하도록 연결할 것. 지금은 구조만 반영.
    public int requiredSaveCount;
}
