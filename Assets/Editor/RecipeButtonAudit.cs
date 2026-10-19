using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 조합 단추 전수 점검(10-19 사장님 「희귀 김기연으로 전설 양문호 가는데 조합 단추가 없다」) — gameshot call:RecipeButtonAudit.Run (플레이 중).
/// 모든 식 × 첫 재료 유닛: 그 유닛을 골랐을 때 CombineSystem.GetRecipesStartingWith(유닛)에 그 식이 있나(= GameHud.RebuildUnitCommandSlots가 쓰는 같은 함수).
/// 그리고 에셋 폴더의 식이 시스템 목록에 다 들어 있나, 한 유닛의 식 수가 단추 칸(아래 줄 4칸)을 넘지 않나.
/// 결과 CSV: ~/grd/GRD_s2_map/결과_1019/조합단추_점검.csv
/// </summary>
public static class RecipeButtonAudit
{
    public static string Run()
    {
        var sys = Object.FindFirstObjectByType<CombineSystem>();
        if (sys == null) return "CombineSystem 없음";
        var sb = new StringBuilder("식,결과,첫재료,문제\n");
        int missing = 0;
        var inSystem = new HashSet<CombineRecipe>(sys.Recipes.Where(r => r != null));

        foreach (string guid in AssetDatabase.FindAssets("t:CombineRecipe"))
        {
            var r = AssetDatabase.LoadAssetAtPath<CombineRecipe>(AssetDatabase.GUIDToAssetPath(guid));
            if (r == null) continue;
            if (!inSystem.Contains(r)) { sb.Append($"{r.name},{r.result?.name},,시스템 목록에 없음\n"); missing++; }
        }

        var perFirst = new Dictionary<UnitData, int>();
        foreach (CombineRecipe r in sys.Recipes)
        {
            if (r == null || r.result == null) continue;
            UnitData first = null;
            if (r.ingredients != null)
                foreach (var ing in r.ingredients) if (ing != null && ing.kind == IngredientKind.SpecificUnit) { first = ing.unit; break; }
            if (first == null) continue;   // 첫 재료가 아이템·와일드카드뿐인 식은 유닛 단추 대상 아님
            if (CombineSystem.IsChatOnly(r)) continue;   // 채팅 전용(의도)
            if (!sys.GetRecipesStartingWith(first).Contains(r)) { sb.Append($"{r.name},{r.result.name},{first.name},단추 목록에 없음\n"); missing++; }
            perFirst[first] = perFirst.TryGetValue(first, out int c) ? c + 1 : 1;
        }
        foreach (var kv in perFirst.Where(kv => kv.Value > 4))
            { sb.Append($",,{kv.Key.name},식 {kv.Value}개 > 아래 줄 4칸\n"); missing++; }

        string path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "grd/GRD_s2_map/결과_1019/조합단추_점검.csv");
        File.WriteAllText(path, sb.ToString());
        return $"식 {sys.Recipes.Count}개 점검 · 문제 {missing}건 → {path}";
    }
}
