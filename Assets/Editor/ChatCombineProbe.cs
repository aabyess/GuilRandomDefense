using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 10-06 히든·불멸·초월 조합 채팅 코드 점검 — gameshot: call:ChatCombineProbe.Report
// 1) 식 전체: 채팅 전용 식이 버튼 목록(GetRecipesStartingWith)에 하나라도 새는지 · 채팅 문구가 식 사이에 겹치는지
// 2) 히든·불멸·초월 하나씩: 재료를 세워 주고 GameChatBox.TryExecuteCode로 실제 조합(결과 유닛이 늘었나)
static class ChatCombineProbe
{
    static string Report()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        var combine = Object.FindFirstObjectByType<CombineSystem>();
        var box = Object.FindFirstObjectByType<GameChatBox>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        if (combine == null || box == null || spawner == null) return "❌ 조합기·채팅창·스포너 없음";

        var chatOnly = combine.Recipes.Where(CombineSystem.IsChatOnly).ToList();
        int leaked = 0;
        var units = combine.Recipes.Where(r => r != null && r.result != null).SelectMany(r => r.ingredients ?? new System.Collections.Generic.List<RecipeIngredient>())
            .Where(i => i != null && i.unit != null).Select(i => i.unit).Distinct().ToList();
        foreach (UnitData u in units)
            leaked += combine.GetRecipesStartingWith(u).Count(CombineSystem.IsChatOnly);
        sb.AppendLine($"[목록] 채팅 전용 식 {chatOnly.Count}개 (히든 {chatOnly.Count(r => r.result.grade == UnitGrade.Hidden)} · 불멸 {chatOnly.Count(r => r.result.grade == UnitGrade.Immortal)} · 초월 {chatOnly.Count(r => r.result.grade == UnitGrade.Transcendent)}) · 버튼 목록에 샌 것 {leaked}(기대 0)");

        foreach (UnitGrade grade in new[] { UnitGrade.Hidden, UnitGrade.Immortal, UnitGrade.Transcendent })
        {
            CombineRecipe recipe = chatOnly.FirstOrDefault(r => r.result.grade == grade && (r.ingredients ?? new()).All(i => i != null && i.kind == IngredientKind.SpecificUnit && i.unit != null));
            if (recipe == null) { sb.AppendLine($"[{grade}] 시험할 식 없음"); continue; }
            int before = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
            foreach (RecipeIngredient ing in recipe.ingredients)
                for (int k = 0; k < Mathf.Max(1, ing.count); k++)
                    spawner.Spawn(ing.unit, LaneMarker.Get(0) != null ? LaneMarker.Get(0).TakeSpawnPosition(ing.unit) : Vector3.zero, 0);
            string phrase = recipe.commandId.Split('/').Last().Trim();
            string message = box.TryExecuteCode(0, phrase);
            int after = UnitIdentity.Active.Count(u => u != null && u.Data == recipe.result && u.OwnerId == 0);
            sb.AppendLine($"[{grade}] {recipe.name} 「{phrase}」 → {message ?? "(코드 아님)"} · 결과 유닛 {before}→{after} · 모자란 것 {string.Join(" / ", combine.DescribeShortage(recipe))}");
        }
        sb.AppendLine("[엉뚱한 말] " + (box.TryExecuteCode(0, "안녕하세요") ?? "null(기대)"));
        return sb.ToString();
    }
}
