using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 히든 조합식 점검(10-06) — Dump: 지정한 히든 식의 재료 에셋 이름·결과(편집 화면에서도 됨). Chat: 호치킨 재료를 세워 주고 「호치킨 조합」을 실제 채팅창 경로로 쳐서 결과 0→1 확인(플레이 중, 목재·돈 지급 포함).
static class HiddenRecipeProbe
{
    static readonly string[] Names = { "히든_호치킨", "히든_이삭토스트", "히든_미소야", "히든_감탄떡볶이", "히든_맥주만땅" };

    static string Dump()
    {
        var sb = new StringBuilder();
        foreach (string n in Names)
        {
            var r = AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{n}.asset");
            if (r == null) { sb.AppendLine($"{n}: 에셋 없음"); continue; }
            sb.AppendLine($"{n} → 결과 {(r.result != null ? r.result.name + "/" + r.result.unitName : "없음")} · 재료 {r.ingredients.Count}: " +
                string.Join(" + ", r.ingredients.Select(i => i.unit != null ? $"{i.unit.name}({i.unit.grade}){(i.count != 1 ? "×" + i.count : "")}" : "null")) + $" · 코드 {r.commandId}");
        }
        return sb.ToString();
    }

    static string Chat()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var combine = Object.FindFirstObjectByType<CombineSystem>();
        var box = Object.FindFirstObjectByType<GameChatBox>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/히든_호치킨.asset");
        if (combine == null || box == null || spawner == null || recipe == null) return "❌ 조합기·채팅창·스포너·식 없음";
        int before = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
        foreach (RecipeIngredient ing in recipe.ingredients)
            for (int k = 0; k < Mathf.Max(1, ing.count); k++)
                spawner.Spawn(ing.unit, LaneMarker.Get(0) != null ? LaneMarker.Get(0).TakeSpawnPosition(ing.unit) : Vector3.zero, 0);
        bool inList = combine.GetRecipesStartingWith(recipe.ingredients[0].unit).Contains(recipe);
        string message = box.TryExecuteCode(0, "호치킨 조합");
        int after = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
        return $"버튼 목록에 있나 {inList}(기대 False) · 「호치킨 조합」 → {message ?? "(코드 아님)"} · 결과 {recipe.result.unitName} {before}→{after}";
    }
}
