using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 하급도박 보너스 묶음(솔·성탄·뻬꼼·상붕카 10%) 점검(10-06) — Dump: 유니티가 읽은 묶음·확률(편집 화면에서도 됨) · Roll: Fund 뒤 하급도박을 N번 굴려(TryRollUnit 직접) 묶음 유닛이 나온 비율을 센다.
static class LowGambleProbe
{
    const string OptionPath = "Assets/Data/Gambling/Gambling_하급도박.asset";

    static string Dump()
    {
        var o = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(OptionPath);
        if (o == null) return "❌ 하급도박 에셋 없음";
        return $"하급도박 · 묶음 {o.bonusPool?.Count ?? 0}기: {string.Join(", ", (o.bonusPool ?? new System.Collections.Generic.List<UnitData>()).Select(u => u != null ? u.name + "/" + u.unitName : "null"))} · 묶음 확률 {o.bonusPoolChancePercent}% · 성공 {o.successChancePercent}% · 옛 해적선 {(o.bonusUnit != null ? o.bonusUnit.unitName : "없음")} {o.bonusChancePercent}% · 비용 {o.goldCost}엔+{o.costResourceType} {o.cost}";
    }

    static string Roll()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var o = AssetDatabase.LoadAssetAtPath<GamblingOptionData>(OptionPath);
        var shop = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.TryGetComponent(out OwnedByPlayer p) && p.OwnerId == 0);
        var me = PlayerContext.Get(0);
        if (o == null || shop == null || me == null) return "❌ 도박소·플레이어·옵션 없음";
        var method = typeof(GamblingShop).GetMethod("TryRollUnit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        const int N = 100;
        var pool = o.bonusPool.Where(u => u != null).ToList();
        System.Func<UnitData, int> count = u => UnitIdentity.Active.Count(x => x != null && x.Data == u && x.OwnerId == 0);
        var before = pool.ToDictionary(u => u, count);
        int ok = 0, fail = 0;
        for (int i = 0; i < N; i++)
        {
            var args = new object[] { o, me, null };
            if ((bool)method.Invoke(shop, args)) ok++; else fail++;
        }
        var sb = new StringBuilder($"하급도박 {N}번: 호출 성공 {ok} · 막힘 {fail}");
        int bonus = 0;
        foreach (UnitData u in pool)
        {
            int got = count(u) - before[u];
            if (u.unitName != "상붕카") bonus += got;
            sb.Append($" · {u.unitName} +{got}");
        }
        sb.Append($" · 솔·성탄·뻬꼼 합 {bonus}/{N} (기대 약 {N * 0.1f * 0.75f:F1}, 묶음 전체 10%=상붕카 포함 약 {N * 0.1f:F0}+보통 결과)");
        return sb.ToString();
    }
}
