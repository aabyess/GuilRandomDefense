using UnityEngine;

/// <summary>두유찬 조합 보상 금화 실측(10-07) — gameshot call:DuyuGoldProbe.Run : 재료를 세우고 채팅 「꺽다리」 조합 → 금화 전후 · 알림.</summary>
static class DuyuGoldProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        CombineSystem cs = Object.FindFirstObjectByType<CombineSystem>();
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        PlayerContext pc = PlayerContext.Local;
        CombineRecipe recipe = null;
        foreach (CombineRecipe r in cs.Recipes) if (r != null && r.result != null && r.result.name == "초월_두유찬_AD") { recipe = r; break; }
        if (recipe == null) return "❌ 식 없음";
        LaneMarker lane = LaneMarker.Get(0);
        var sb = new System.Text.StringBuilder($"보상 필드 {recipe.acquireGoldReward} · 재료 {recipe.ingredients.Count}");
        int n = 0;
        foreach (RecipeIngredient ing in recipe.ingredients)
        {
            if (ing.kind != IngredientKind.SpecificUnit) { sb.Append($"\n   재료 종류 {ing.kind}"); continue; }
            spawner.Spawn(ing.unit, lane.LaneCenter + Vector3.right * (n++ * 30f), 0);
        }
        int before = pc.GoldWallet.Gold;
        string msg = cs.TryCombineByChat(0, "꺽다리");
        sb.Append($"\n   채팅 결과 「{msg}」 · 금화 {before} → {pc.GoldWallet.Gold}(차 {pc.GoldWallet.Gold - before})");
        return sb.ToString();
    }
}
