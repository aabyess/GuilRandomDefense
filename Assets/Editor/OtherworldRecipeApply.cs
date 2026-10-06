using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 다른세계 9식 재료·칭호 적용(사장님 10-06 확정, 설계표 Docs/design/OTHERWORLD_RECIPES_DESIGN_2026-10-06.md §6). 호출: call OtherworldRecipeApply.Apply (다시 불러도 안전).
///  · 김건부: 전설 신문철 뺌 · 모리야스와코: 비용 엔 10,000 하나(목재 7·토큰 비용 뺌) · 고죠사토루: 제한 김민규 → 전설 김민규(미래를보는눈) · 브로리: 전설 양재모 → 희귀 양재모(상호파의개) + 「임채현 or 이재윤」(RecipeIngredient.alternativeUnit)
///  · 호시노루비: 전설 김정래 → 특별 김정래(프로그래머) — 랜덤전용 1기 칸은 고죠·브로리에 이미 있다.
///  · 9기 모두 unitName을 「이름 칭호」 한 덩어리로(UnitData.DisplayPerson은 다른세계에서 사람 이름을 따로 안 붙인다). 「무면허 라이더」 → 「고태훈 다른세계의주민」, 호시노루비는 「(신)B코마치의멤버」.
/// </summary>
static class OtherworldRecipeApply
{
    static readonly (string asset, string title)[] Titles =
    {
        ("다른세계_나나미_치아키", "나나미치아키 초고교급프로게이머"),
        ("다른세계_무면허_라이더", "고태훈 다른세계의주민"),
        ("다른세계_김건부", "김건부 캐니언"),
        ("다른세계_모리야_스와코", "모리야스와코 땅의신"),
        ("다른세계_고죠_사토루", "고죠사토루 특급주술사"),
        ("다른세계_올마이트", "올마이트 원포올"),
        ("다른세계_한마_유지로", "한마유지로 오거"),
        ("다른세계_브로리", "브로리 사이어인"),
        ("다른세계_호시노_루비", "호시노루비 (신)B코마치의멤버"),
    };

    static UnitData U(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static CombineRecipe R(string n) => AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{n}.asset");

    static int Replace(CombineRecipe r, string oldUnit, UnitData now)
    {
        int n = 0;
        foreach (RecipeIngredient ing in r.ingredients)
            if (ing != null && ing.unit != null && ing.unit.name == oldUnit) { ing.unit = now; n++; }
        return n;
    }

    static string Apply()
    {
        var log = new List<string>();
        CombineRecipe gunbu = R("다른세계_김건부"), moriya = R("다른세계_모리야_스와코"), gojo = R("다른세계_고죠_사토루"), broly = R("다른세계_브로리"), ruby = R("다른세계_호시노_루비");
        if (gunbu == null || moriya == null || gojo == null || broly == null || ruby == null) return "❌ 다른세계 식 에셋을 못 찾음";

        log.Add($"김건부 신문철 {gunbu.ingredients.RemoveAll(i => i != null && i.unit != null && i.unit.name == "전설적인_신문철")}칸 제거");
        moriya.goldCost = 10000; moriya.resourceCosts = new List<RecipeResourceCost>();
        log.Add("모리야스와코 비용 엔 10,000만");
        log.Add($"고죠 김민규 교체 {Replace(gojo, "제한_김민규", U("전설적인_김민규"))}");
        log.Add($"브로리 양재모 교체 {Replace(broly, "전설적인_양재모", U("희귀함_양재모"))}");
        foreach (RecipeIngredient ing in broly.ingredients)
            if (ing != null && ing.unit != null && ing.unit.name == "전설적인_임채현") { ing.alternativeUnit = U("전설적인_이재윤"); log.Add("브로리 임채현 or 이재윤"); }
        log.Add($"호시노루비 김정래 교체 {Replace(ruby, "전설적인_김정래", U("특별함_김정래"))}");
        foreach (CombineRecipe r in new[] { gunbu, moriya, gojo, broly, ruby }) EditorUtility.SetDirty(r);

        foreach ((string asset, string title) in Titles)
        {
            UnitData u = U(asset);
            if (u == null) { log.Add($"❌ {asset} 없음"); continue; }
            u.unitName = title;
            EditorUtility.SetDirty(u);
        }
        AssetDatabase.SaveAssets();
        return "다른세계 식 적용: " + string.Join(" · ", log) + $" · 칭호 {Titles.Length}기";
    }
}
