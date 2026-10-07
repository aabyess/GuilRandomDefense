using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>이름 바꾸기 19기 실측(10-07) — 새 입력말은 코드로 받고 옛 입력말은 안 받나, 초성 검색이 새 이름을 찾나, 정보창용 표시 이름. 호출: call UnitRenameProbe.Chat (플레이 중)</summary>
static class UnitRenameProbe
{
    static string Chat()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var cs = Object.FindFirstObjectByType<CombineSystem>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var sb = new StringBuilder();
        foreach ((string recipeName, string phrase) in new[]
        {
            ("히든_여은서", "푸은서조합"), ("히든_여은서", "여은서조합"), ("히든_여은서", "푸은서 조합"),
            ("히든_석성례", "성성례조합"), ("히든_전주연", "광주연조합"), ("히든_전주연", "전주연조합"),
            ("불멸_이이삭", "김이삭조합"), ("불멸_이이삭", "이이삭조합"), ("영원_이지원", "김지원조합"),
        })
        {
            var recipe = AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{recipeName}.asset");
            if (recipe == null) { sb.AppendLine($"{recipeName}: 식 없음"); continue; }
            int before = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
            LaneMarker lane = LaneMarker.Get(0);
            int n = 0;
            foreach (RecipeIngredient ing in recipe.ingredients)
                if (ing.kind == IngredientKind.SpecificUnit && ing.unit != null)
                    for (int k = 0; k < Mathf.Max(1, ing.count); k++)
                        spawner.Spawn(ing.unit, lane.LaneCenter + Vector3.right * (n++ * 8f), 0);
            string message = cs.TryCombineByChat(0, phrase);
            int after = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
            sb.AppendLine($"「{phrase}」({recipeName}) → {(message ?? "(코드 아님 — 안 받음)").Replace("\n", " / ")} · 결과 {recipe.result.DisplayName} {before}→{after}");
        }
        return sb.ToString();
    }

    // F5 검색(초성 포함)이 새 이름을 찾는지 — HangulSearch.UnitMatches와 같은 판정.
    static string Search()
    {
        var sb = new StringBuilder();
        foreach ((string query, string asset) in new[]
        {
            ("ㅍㅇㅅ", "히든_여은서"), ("푸은서", "히든_여은서"), ("여은서", "히든_여은서"), ("ㄱㅈㅇ", "특별함_이지원"), ("김지원", "영원_이지원"),
            ("ㅂㅎㅈ", "희귀함_배현진"), ("박현진", "희귀함_배현진"), ("ㅍㅋㅅ", "희귀함_정내연"), ("ㅊㅈㅇ", "전설적인_신지우"), ("ㄴㅇㅅ", "히든_최윤서"),
        })
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{asset}.asset");
            string norm = HangulSearch.Normalize(query);
            sb.AppendLine($"「{query}」 → {asset}({unit.DisplayName}) {(HangulSearch.UnitMatches(norm, unit) ? "찾음" : "못 찾음")}");
        }
        return sb.ToString();
    }

    static string Spawn푸은서()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var unit = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/히든_여은서.asset");
        LaneMarker lane = LaneMarker.Get(0);
        spawner.Spawn(unit, lane.LaneCenter, 0);
        return $"✅ {unit.DisplayName} 세움";
    }
}
