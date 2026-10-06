using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>다른세계 식 실측(10-06 구현담당3) — gameshot: call:OtherworldRecipeProbe.Report (신 모드). 5개 바뀐 식을 재료만 세워 실제 TryCombine(재료 소모·결과 유닛), 브로리 or 재료 3경우, 칭호·DisplayName.</summary>
public static class OtherworldRecipeProbe
{
    static UnitData U(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");
    static int Mine() => UnitIdentity.Active.Count(u => u != null && u.OwnerId == 0 && !u.IsSummon);

    static void Clear()
    {
        foreach (var u in UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && !x.IsSummon).ToList()) u.Consume();
    }

    static string Try(string label, string recipeName, params string[] units)
    {
        Clear();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>($"Assets/Data/Recipes/{recipeName}.asset");
        PlayerContext ctx = PlayerContext.Local;
        ctx.GoldWallet.Add(50000);
        ctx.ResourceWallet.Add(ResourceType.Wood, 20); ctx.ResourceWallet.Add(ResourceType.Token, 5); ctx.ResourceWallet.Add(ResourceType.LuckyToken, 5);
        foreach (string n in units) spawner.Spawn(U(n), LaneMarker.Get(0).TakeSpawnPosition(U(n)), 0);
        int before = Mine(); int gold0 = ctx.GoldWallet.Gold;
        bool ok = Object.FindFirstObjectByType<CombineSystem>().TryCombine(recipe);
        string left = string.Join(",", UnitIdentity.Active.Where(x => x != null && x.OwnerId == 0 && !x.IsSummon && x.Data != null).Select(x => x.Data.name));
        return $"   {label}: TryCombine {ok} · 내 유닛 {before} → {Mine()} · 엔 {gold0 - ctx.GoldWallet.Gold} 소모 · 남은 유닛 [{left}]";
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Try("김건부(신문철 없이)", "다른세계_김건부", "랜덤_김건모", "전설적인_김민준", "전설적인_김건"));
        sb.AppendLine(Try("모리야스와코(엔 10,000만·목재 없이)", "다른세계_모리야_스와코", "랜덤_야사카_카나코", "전설적인_엄태웅", "랜덤_모몬가"));
        sb.AppendLine(Try("고죠사토루(전설 김민규 + 랜덤전용)", "다른세계_고죠_사토루", "랜덤_이타도리_유지", "전설적인_김민규", "희귀함_최현우", "랜덤_손오공"));
        sb.AppendLine(Try("브로리 — 임채현만", "다른세계_브로리", "랜덤_손오공", "전설적인_임채현", "희귀함_양재모", "랜덤_모몬가"));
        sb.AppendLine(Try("브로리 — 이재윤만", "다른세계_브로리", "랜덤_손오공", "전설적인_이재윤", "희귀함_양재모", "랜덤_모몬가"));
        sb.AppendLine(Try("브로리 — 둘 다(임채현 먼저)", "다른세계_브로리", "랜덤_손오공", "전설적인_임채현", "전설적인_이재윤", "희귀함_양재모", "랜덤_모몬가"));
        sb.AppendLine(Try("호시노루비(특별 김정래)", "다른세계_호시노_루비", "랜덤_호시노_아이", "전설적인_양문호", "히든_전주연", "희귀함_이은엽", "특별함_김정래", "특별함_이현빈"));
        Clear();
        sb.AppendLine("   표시 이름: " + string.Join(" | ", new[] { "다른세계_나나미_치아키", "다른세계_무면허_라이더", "다른세계_호시노_루비", "다른세계_브로리" }.Select(n => U(n).DisplayName)));
        var broly = UnityEditor.AssetDatabase.LoadAssetAtPath<CombineRecipe>("Assets/Data/Recipes/다른세계_브로리.asset");
        var cs = Object.FindFirstObjectByType<CombineSystem>();
        sb.AppendLine("   브로리 재료 부족 안내: " + string.Join(" / ", cs.DescribeShortage(broly) ?? new System.Collections.Generic.List<string>()));
        return sb.ToString();
    }
}
