using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 스토리 보상 원작 맞추기 점검(10-06) — gameshot:
//   call:StoryRewardProbe.Setup → call:StoryRewardProbe.Clear8 → call:StoryRewardProbe.Report → call:StoryRewardProbe.Clear9 → call:StoryRewardProbe.Report → call:StoryRewardProbe.Clear11 → call:StoryRewardProbe.Report
//   (Setup: 초월 영웅 1기 + 항법 도움소 잠금 선택 + 도박소 잠금 상태 기록)
static class StoryRewardProbe
{
    static UnitIdentity hero;
    static UnitData rayleigh;
    static GamblingOptionData gamble;
    static GamblingShop shop;

    static StoryData Story(string n) => AssetDatabase.LoadAssetAtPath<StoryData>($"Assets/Data/Stories/{n}.asset");

    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        PlayerContext me = PlayerContext.Local;
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_신문철_AP.asset");
        rayleigh = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_박기찬.asset");
        gamble = AssetDatabase.LoadAssetAtPath<GamblingOptionData>("Assets/Data/Gambling/Gambling_랜덤유닛 도박.asset");
        if (me == null || spawner == null || lane == null || d == null) return "❌ 준비 안 됨";
        hero = spawner.Spawn(d, lane.LaneCenter, 0).GetComponent<UnitIdentity>();
        bool selected = me.NavigationState != null && me.NavigationState.TrySelect(NavigationChoice.SupportLock);
        shop = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.GetComponent<OwnedByPlayer>() != null && s.GetComponent<OwnedByPlayer>().OwnerId == me.PlayerId) ?? Object.FindFirstObjectByType<GamblingShop>();
        return $"세움: 영웅 {hero.Data.DisplayName} · 항법 도움소 잠금 선택 {selected} (지금 {me.NavigationState?.Choice}) · 다른세계 도박 requiresUnlock {gamble.requiresUnlock} · 도박소 {(shop != null ? shop.name : "없음")}";
    }

    static int HeroXp() => (int)typeof(UnitAttacker).GetField("heroXp", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hero.GetComponent<UnitAttacker>());
    static int Count(UnitData u) => PlayerContext.Local.UnitInventory.Units.Count(x => x == u);
    static string Do(string story)
    {
        if (hero == null) return "❌ Setup 먼저";
        int xp0 = HeroXp(), ray0 = Count(rayleigh);
        bool lock0 = PlayerContext.Local.GamblingProgress.IsUnlocked(gamble);
        RewardDistributor.Instance.GrantStoryReward(Story(story));
        return $"{story} 클리어 → 영웅 경험치 {xp0} → {HeroXp()} · 레일리(박기찬) {ray0} → {Count(rayleigh)} · 다른세계 도박 해금 {lock0} → {PlayerContext.Local.GamblingProgress.IsUnlocked(gamble)}";
    }
    static string Clear8() => Do("Story08_사이버넷");
    static string Clear9() => Do("Story09_7탄약창");
    static string Clear11() => Do("Story11_日本");
    static string Clear12() => Do("Story12_코드잇");
    static string Clear13() => Do("Story13_쉬었음");

    static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"   영웅 경험치 {HeroXp()} · 레일리 {Count(rayleigh)}기 · 다른세계 도박 해금 {PlayerContext.Local.GamblingProgress.IsUnlocked(gamble)}");
        if (shop != null)
        {
            var m = typeof(GamblingShop).GetMethod("StockSuffix", BindingFlags.Instance | BindingFlags.NonPublic);
            var t = typeof(GamblingShop).GetMethod("BuildUnitTooltip", BindingFlags.Instance | BindingFlags.NonPublic);
            string suffix = m != null ? (string)m.Invoke(shop, new object[] { gamble }) : "?";
            string tip = t != null ? (string)t.Invoke(shop, new object[] { gamble }) : "?";
            sb.AppendLine($"   칸 접미 「{suffix.Replace("\n", "↵")}」");
            sb.AppendLine($"   툴팁 끝: {tip.Substring(Mathf.Max(0, tip.Length - 80)).Replace("\n", "↵")}");
        }
        return sb.ToString();
    }
}
