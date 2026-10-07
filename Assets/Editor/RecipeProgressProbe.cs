using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 조합 도우미 진행률 검산(편집 모드, 플레이 없음) — call RecipeProgressProbe.Run
//   ① 보유 0 → 전부 0% · ② 손계산: 재료 3칸(전부 잎) 식에 2기 보유 → 66.7% · ③ 위 등급 유닛 보유가 그 아래 잎을 통째로 채움 · ④ 한 기로 두 칸이 안 채워짐 · ⑤ 풀이 계산 뒤 원래대로 · ⑥ 성능(242유닛 × 보유 400기 풀)
static class RecipeProgressProbe
{
    static string Run()
    {
        var recipes = AssetDatabase.FindAssets("t:CombineRecipe", new[] { "Assets/Data/Recipes" }).Select(g => AssetDatabase.LoadAssetAtPath<CombineRecipe>(AssetDatabase.GUIDToAssetPath(g))).Where(r => r != null).ToList();
        var roster = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).Where(u => u != null).ToList();
        var progress = new RecipeProgress(recipes, roster);
        var sb = new StringBuilder($"조합식 {recipes.Count} · 로스터 {roster.Count}\n");
        var none = new Dictionary<UnitData, int>();
        var noItems = new Dictionary<ItemData, int>();

        // ① 보유 0
        int nonZero = roster.Count(u => progress.Compute(u, none, noItems) > 0f);
        sb.AppendLine($"① 보유 0 → 0%가 아닌 유닛 {nonZero}개 (0이어야 함)");

        // ② 손계산: 재료 3칸이 전부 잎(조합식 없는 유닛, 와일드카드·아이템 없음)이고 개수 1인 식
        CombineRecipe simple = recipes.FirstOrDefault(r => r.ingredients != null && r.ingredients.Count == 3 && r.ingredients.All(i => i.kind == IngredientKind.SpecificUnit && i.unit != null && i.alternativeUnit == null && i.count <= 1 && !progress.HasRecipe(i.unit)) && r.ingredients.Select(i => i.unit).Distinct().Count() == 3);
        if (simple != null)
        {
            var own2 = new Dictionary<UnitData, int> { { simple.ingredients[0].unit, 1 }, { simple.ingredients[1].unit, 1 } };
            float f = progress.Compute(simple.result, own2, noItems);
            sb.AppendLine($"② 손계산 {simple.result.name}: 잎 3칸 중 2기 보유 → {f * 100f:F1}% (기대 66.7) · 풀 되돌림: {string.Join(",", own2.Select(p => p.Key.name + "=" + p.Value))}");
            var own3 = new Dictionary<UnitData, int> { { simple.ingredients[0].unit, 1 }, { simple.ingredients[1].unit, 1 }, { simple.ingredients[2].unit, 1 } };
            sb.AppendLine($"   3기 보유 → {progress.Compute(simple.result, own3, noItems) * 100f:F1}% (기대 100)");
            // ④ 같은 유닛 한 기로 두 칸: 가능하면 같은 재료가 2칸인 식을 못 찾으니 손으로 — 한 잎 1기만 → 정확히 (그 잎 가중치/3)
            var own1 = new Dictionary<UnitData, int> { { simple.ingredients[0].unit, 1 } };
            sb.AppendLine($"   1기 보유 → {progress.Compute(simple.result, own1, noItems) * 100f:F1}% (기대 33.3)");
            // 결과 유닛 자신을 보유 → 100
            sb.AppendLine($"   결과 유닛 자신 보유 → {progress.Compute(simple.result, new Dictionary<UnitData, int> { { simple.result, 1 } }, noItems) * 100f:F0}% (기대 100)");
        }
        else sb.AppendLine("② 손계산용 단순 식을 못 찾음");

        // ③ 위 등급 유닛이 재료에 있는 식: 재료 중 조합식이 있는 유닛 M을 가진 식 R을 찾아 M 하나만 보유 → R 진행률 ≥ L(M)/L(R)
        foreach (CombineRecipe r in recipes)
        {
            RecipeIngredient mid = r.ingredients == null ? null : r.ingredients.FirstOrDefault(i => i != null && i.kind == IngredientKind.SpecificUnit && i.unit != null && progress.HasRecipe(i.unit));
            if (mid == null) continue;
            int lr = progress.Weight(r.result), lm = progress.Weight(mid.unit);
            float f = progress.Compute(r.result, new Dictionary<UnitData, int> { { mid.unit, 1 } }, noItems);
            sb.AppendLine($"③ {r.result.name}(L={lr})에 재료 {mid.unit.name}(L={lm}) 한 기만 보유 → {f * 100f:F1}% (기대 ≥ {100f * lm / lr:F1}, 개수 {mid.count})");
            break;
        }

        // ⑤ 풀 되돌림: 임의 보유로 전 유닛 계산 뒤 풀이 같은지
        var rng = new System.Random(3);
        var pool = new Dictionary<UnitData, int>();
        for (int i = 0; i < 400; i++) { UnitData u = roster[rng.Next(roster.Count)]; pool[u] = (pool.TryGetValue(u, out int n) ? n : 0) + 1; }
        string before = string.Join(",", pool.OrderBy(p => p.Key.name).Select(p => p.Key.name + "=" + p.Value));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float sum = 0f; int hundred = 0;
        foreach (UnitData u in roster) { float f = progress.Compute(u, pool, noItems); sum += f; if (f >= 0.999f) hundred++; }
        sw.Stop();
        string after = string.Join(",", pool.OrderBy(p => p.Key.name).Select(p => p.Key.name + "=" + p.Value));
        sb.AppendLine($"⑤ 보유 400기 무작위 → 전 유닛 계산 {sw.Elapsed.TotalMilliseconds:F1}ms · 평균 {sum / roster.Count * 100f:F1}% · 100% {hundred}개 · 풀 되돌림 {(before == after ? "OK(같음)" : "❌ 달라짐")}");
        // 두 번째 계산이 같은 결과(재현)
        float again = 0f; foreach (UnitData u in roster) again += progress.Compute(u, pool, noItems);
        sb.AppendLine($"   같은 입력 두 번째 합계 {again:F4} vs {sum:F4} → {(Mathf.Approximately(again, sum) ? "재현 OK" : "❌ 다름")}");
        return sb.ToString();
    }
}
