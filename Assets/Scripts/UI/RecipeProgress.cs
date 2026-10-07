using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조합 도우미(tmo build-helper 식) 진행률 계산 — 설계표 Docs/design/RECIPE_HELPER_DESIGN_2026-10-06.md §1.
/// 유닛 U의 진행률 = 「내 보유로 U의 재료 트리를 조합식이 없는 유닛(잎)까지 풀었을 때 채워진 잎 가중치 ÷ 전체 잎 가중치」.
///  · 보유 유닛이 한 자리에 쓰이면 그 아래 트리를 통째로 채운 것으로 친다(위 등급 유닛을 가지고 있으면 그 재료 잎이 다 모인 것).
///  · 유닛마다 **독립**(내 보유를 서로 안 나눠 쓴다 — PM이 tmo로 확인, §11). 한 유닛의 트리 안에서는 같은 보유 1기가 두 자리를 못 채운다(풀에서 뺀다).
///  · 목재·엔·라운드 제한은 %에서 뺀다(호출부가 따로 보여 준다). 「A 또는 B」 재료는 보유 쪽을 쓴다(둘 다 있으면 A).
///  · 같은 결과의 식이 둘이면 큰 쪽.
/// 순수 계산이라 UI·씬과 무관 — 에디터 탐침(RecipeProgressProbe)으로 손계산과 대조한다.
/// </summary>
public class RecipeProgress
{
    readonly Dictionary<UnitData, List<CombineRecipe>> recipesByResult = new Dictionary<UnitData, List<CombineRecipe>>();
    readonly Dictionary<UnitData, int> leafWeight = new Dictionary<UnitData, int>();           // L(u) 메모
    readonly Dictionary<UnitGrade, int> minWeightByGrade = new Dictionary<UnitGrade, int>();   // 와일드카드 칸 가중치(그 등급 유닛 L의 최솟값)
    readonly HashSet<UnitData> inProgress = new HashSet<UnitData>();                           // 순환 방지

    // 계산 중 보유 풀(유닛 · 아이템) — 쓴 만큼 빼고 끝나면 되돌린다(복사 없이).
    Dictionary<UnitData, int> poolUnits;
    Dictionary<ItemData, int> poolItems;
    readonly List<(UnitData unit, ItemData item)> undo = new List<(UnitData, ItemData)>();

    const int MaxDepth = 14;

    /// <summary>조합식 표와 로스터(와일드카드 최솟값 계산용, 없으면 null)로 한 번 만든다. 식은 상수라 메모는 평생 유효.</summary>
    public RecipeProgress(IEnumerable<CombineRecipe> recipes, IEnumerable<UnitData> roster = null)
    {
        foreach (CombineRecipe recipe in recipes)
        {
            if (recipe == null || recipe.result == null || recipe.ingredients == null) continue;
            if (!recipesByResult.TryGetValue(recipe.result, out List<CombineRecipe> list))
                recipesByResult[recipe.result] = list = new List<CombineRecipe>();
            list.Add(recipe);
        }
        if (roster != null)
            foreach (UnitData unit in roster)
            {
                if (unit == null) continue;
                int w = Weight(unit, 0);
                if (!minWeightByGrade.TryGetValue(unit.grade, out int best) || w < best) minWeightByGrade[unit.grade] = w;
            }
    }

    /// <summary>이 유닛의 조합식이 있는가(없으면 잎).</summary>
    public bool HasRecipe(UnitData unit) => unit != null && recipesByResult.ContainsKey(unit);

    /// <summary>잎 가중치 L(u): 잎이면 1, 아니면 식 중 가장 싼 쪽의 Σ(재료 개수 × L(재료)).</summary>
    public int Weight(UnitData unit, int depth = 0)
    {
        if (unit == null) return 1;
        if (leafWeight.TryGetValue(unit, out int cached)) return cached;
        if (!recipesByResult.TryGetValue(unit, out List<CombineRecipe> recipes) || depth > MaxDepth || !inProgress.Add(unit)) return 1;
        int best = int.MaxValue;
        foreach (CombineRecipe recipe in recipes)
        {
            int sum = 0;
            foreach (RecipeIngredient ing in recipe.ingredients) sum += SlotWeight(ing, depth + 1);
            if (sum > 0 && sum < best) best = sum;
        }
        inProgress.Remove(unit);
        int result = best == int.MaxValue ? 1 : best;
        leafWeight[unit] = result;
        return result;
    }

    int SlotWeight(RecipeIngredient ing, int depth)
    {
        if (ing == null) return 0;
        int count = Mathf.Max(1, ing.count);
        switch (ing.kind)
        {
            case IngredientKind.SpecificUnit:
                if (ing.unit == null) return 0;
                int w = Weight(ing.unit, depth);
                if (ing.alternativeUnit != null) w = Mathf.Min(w, Weight(ing.alternativeUnit, depth));
                return count * w;
            case IngredientKind.SpecificItem:
                return count;
            default:
                return count * (minWeightByGrade.TryGetValue(ing.wildcardGrade, out int g) ? g : 1);
        }
    }

    /// <summary>진행률 0~1. ownedUnits/ownedItems는 읽기만 한다(계산 도중 바뀌어도 끝나면 그대로 돌려놓는다).</summary>
    public float Compute(UnitData unit, Dictionary<UnitData, int> ownedUnits, Dictionary<ItemData, int> ownedItems)
    {
        if (unit == null) return 0f;
        poolUnits = ownedUnits;
        poolItems = ownedItems;
        undo.Clear();
        try
        {
            if (Owned(unit) > 0) return 1f;
            if (!recipesByResult.TryGetValue(unit, out List<CombineRecipe> recipes)) return 0f;   // 잎인데 없음
            float best = 0f;
            foreach (CombineRecipe recipe in recipes)
            {
                int checkpoint = undo.Count;
                float filled = FillRecipe(recipe, 0);
                Rollback(checkpoint);
                int total = 0;
                foreach (RecipeIngredient ing in recipe.ingredients) total += SlotWeight(ing, 0);
                if (total > 0) best = Mathf.Max(best, Mathf.Clamp01(filled / total));
            }
            return best;
        }
        finally { Rollback(0); poolUnits = null; poolItems = null; }
    }

    int Owned(UnitData unit) => unit != null && poolUnits != null && poolUnits.TryGetValue(unit, out int n) ? n : 0;

    void TakeUnit(UnitData unit) { poolUnits[unit] = poolUnits[unit] - 1; undo.Add((unit, null)); }
    void TakeItem(ItemData item) { poolItems[item] = poolItems[item] - 1; undo.Add((null, item)); }

    void Rollback(int checkpoint)
    {
        for (int i = undo.Count - 1; i >= checkpoint; i--)
        {
            if (undo[i].unit != null) poolUnits[undo[i].unit] = poolUnits[undo[i].unit] + 1;
            else if (undo[i].item != null) poolItems[undo[i].item] = poolItems[undo[i].item] + 1;
        }
        if (undo.Count > checkpoint) undo.RemoveRange(checkpoint, undo.Count - checkpoint);
    }

    // 한 식을 채운 잎 가중치. 큰 칸(가중치 큰 것)부터 처리해 위 등급 보유가 잎 여럿을 먼저 대신하게 한다 — 순서가 고정이라 결과가 재현된다.
    float FillRecipe(CombineRecipe recipe, int depth)
    {
        var slots = new List<RecipeIngredient>(recipe.ingredients.Count);
        foreach (RecipeIngredient ing in recipe.ingredients) if (ing != null) slots.Add(ing);
        slots.Sort((a, b) => SlotWeight(b, depth).CompareTo(SlotWeight(a, depth)));   // List.Sort는 불안정하지만 같은 가중치끼리는 결과가 같다
        float filled = 0f;
        foreach (RecipeIngredient ing in slots)
        {
            int count = Mathf.Max(1, ing.count);
            for (int c = 0; c < count; c++) filled += FillOne(ing, depth);
        }
        return filled;
    }

    // 재료 한 칸(개수 1)을 채운 잎 가중치.
    float FillOne(RecipeIngredient ing, int depth)
    {
        switch (ing.kind)
        {
            case IngredientKind.SpecificItem:
                if (ing.item != null && poolItems != null && poolItems.TryGetValue(ing.item, out int items) && items > 0) { TakeItem(ing.item); return 1f; }
                return 0f;
            case IngredientKind.UnitGradeWildcard:
            {
                if (poolUnits == null) return 0f;
                int w = minWeightByGrade.TryGetValue(ing.wildcardGrade, out int g) ? g : 1;
                UnitData pick = null;
                foreach (var pair in poolUnits) if (pair.Value > 0 && pair.Key != null && pair.Key.grade == ing.wildcardGrade) { pick = pair.Key; break; }
                if (pick == null) return 0f;
                TakeUnit(pick);
                return w;
            }
            default:
                break;
        }
        if (ing.unit == null) return 0f;
        // A가 있으면 A, 없고 B가 있으면 B
        UnitData chosen = Owned(ing.unit) > 0 ? ing.unit : (ing.alternativeUnit != null && Owned(ing.alternativeUnit) > 0 ? ing.alternativeUnit : null);
        if (chosen != null) { TakeUnit(chosen); return Weight(chosen, 0); }
        // 보유가 없으면 그 재료를 조합식으로 더 풀어 본다(A 우선, 없으면 B)
        return FillByRecipe(ing.unit, depth + 1) is float a && a > 0f ? a
            : ing.alternativeUnit != null ? FillByRecipe(ing.alternativeUnit, depth + 1) : 0f;
    }

    float FillByRecipe(UnitData unit, int depth)
    {
        if (depth > MaxDepth || !recipesByResult.TryGetValue(unit, out List<CombineRecipe> recipes)) return 0f;
        float bestFilled = 0f; int bestMark = -1;
        // 식이 여럿이면 가장 많이 채우는 쪽을 쓴다(다른 쪽 시도는 되돌린다).
        foreach (CombineRecipe recipe in recipes)
        {
            int checkpoint = undo.Count;
            float filled = FillRecipe(recipe, depth);
            if (filled > bestFilled) { bestFilled = filled; bestMark = recipes.IndexOf(recipe); }
            Rollback(checkpoint);
        }
        if (bestMark >= 0)
        {
            // 가장 좋은 식을 다시 한 번 적용해 풀에서 실제로 뺀다.
            FillRecipe(recipes[bestMark], depth);
        }
        return bestFilled;
    }
}
