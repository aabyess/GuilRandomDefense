using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>초월 박민수 조합식에 빠져 있던 임채민 채민파리더(희귀함_임채민) 재료 추가(사장님 10-06 원문, PM 확인). 호출: call MinsooRecipeFix.Apply (다시 불러도 안전).</summary>
static class MinsooRecipeFix
{
    static string Apply()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/초월_박민수_AD.asset");
        var chaemin = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_임채민.asset");
        if (recipe == null || chaemin == null) return "❌ 박민수 조합식 또는 희귀함_임채민 없음";
        foreach (RecipeIngredient i in recipe.ingredients) if (i.unit == chaemin) return "이미 들어 있음";
        // 초월위습 앞에 끼워 넣는다(마지막이 늘 초월위습이라는 관례).
        int at = recipe.ingredients.FindIndex(i => i.unit != null && i.unit.grade == UnitGrade.TranscendentWisp);
        var item = new RecipeIngredient { kind = IngredientKind.SpecificUnit, unit = chaemin, count = 1 };
        if (at < 0) recipe.ingredients.Add(item); else recipe.ingredients.Insert(at, item);
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return $"박민수 조합식 재료 {recipe.ingredients.Count}종: " + string.Join(", ", recipe.ingredients.ConvertAll(i => i.unit != null ? i.unit.name : "null"));
    }
}
