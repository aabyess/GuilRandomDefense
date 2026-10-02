using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

// 압살롬 도박(원작 h069) 판 안 점검 — gameshot call:AbsalomProbe.Run → wait → call:AbsalomProbe.Frame → snap:
// 실제 코드 경로만 쓴다: R20 보스 처치 핸들러(HandleBossKilled) → 해금 → 칸 7 → GamblingShop.TryRoll. 유닛은 직접 만들지 않는다.
static class AbsalomProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        GamblingShop shop = Object.FindObjectsByType<GamblingShop>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 0);
        if (shop == null) return "❌ 0번 플레이어 도박소가 없다";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var options = typeof(GamblingShop).GetField("unitOptions", Any).GetValue(shop) as System.Collections.Generic.List<GamblingOptionData>;
        sb.AppendLine($"unitOptions {options.Count}개: {string.Join(" / ", options.Select(o => o != null ? o.optionName : "null"))}");
        GamblingOptionData opt = options.FirstOrDefault(o => o != null && o.optionName == "압살롬 도박");
        if (opt == null) return sb + "❌ 압살롬 도박이 도박소에 없다";
        PlayerContext me = PlayerContext.Get(0);

        // 해금 전
        me.GoldWallet.Add(500); me.ResourceWallet.Add(opt.costResourceType, opt.cost);
        bool before = shop.CanRoll(opt);
        shop.TryRoll(opt, out string reasonBefore);
        sb.AppendLine($"[해금 전] CanRoll={before} (기대 false) · 사유 「{reasonBefore}」 · 칸7 {Describe(shop)}");

        // 다른 레인 보스는 안 열어야 한다(원작: 보스 레인 주인만)
        typeof(GamblingShop).GetMethod("HandleBossKilled", Any).Invoke(shop, new object[] { 20, 1 });
        sb.AppendLine($"[남의 레인 R20 보스] 해금={me.GamblingProgress.IsUnlocked(opt)} (기대 false)");
        // 내 레인 R19 보스는 아직 아님
        typeof(GamblingShop).GetMethod("HandleBossKilled", Any).Invoke(shop, new object[] { 19, 0 });
        sb.AppendLine($"[내 레인 R19 보스] 해금={me.GamblingProgress.IsUnlocked(opt)} (기대 false)");
        typeof(GamblingShop).GetMethod("HandleBossKilled", Any).Invoke(shop, new object[] { 20, 0 });
        sb.AppendLine($"[내 레인 R20 보스] 해금={me.GamblingProgress.IsUnlocked(opt)} (기대 true) · 칸7 {Describe(shop)}");

        int tries = 0, wins = 0;
        int baseCount = Mine().Count();
        while (tries < 80 && wins < 3)
        {
            me.GoldWallet.Add(500); me.ResourceWallet.Add(opt.costResourceType, opt.cost);
            int goldBefore = me.GoldWallet.Gold, woodBefore = me.ResourceWallet.Get(opt.costResourceType);
            int unitsBefore = Mine().Count();
            if (!shop.TryRoll(opt, out string reason)) { sb.AppendLine($"❌ TryRoll 거절: {reason}"); break; }
            tries++;
            int dGold = goldBefore - me.GoldWallet.Gold, dWood = woodBefore - me.ResourceWallet.Get(opt.costResourceType);
            bool won = Mine().Count() > unitsBefore;
            if (won) wins++;
            if (tries <= 3 || won) sb.AppendLine($"  {tries}번째: {(won ? "당첨" : "실패")} · 골드 -{dGold} · {opt.costResourceType} -{dWood}");
        }
        var absalom = Mine().ToList();
        sb.AppendLine($"[결과] {tries}번 중 당첨 {wins} (기대 확률 45%, 표본 작음) · 내 압살롬 {absalom.Count}기 (시작 {baseCount})");
        foreach (UnitIdentity u in absalom.Take(3))
        {
            var rs = u.GetComponentsInChildren<Renderer>();
            string model = u.gameObject.name;
            sb.AppendLine($"  {model} 위치 {u.transform.position:F0} · 렌더러 {rs.Length}개 · 그레이드 {u.Data.grade} · 이름 「{u.Data.unitName}」 · 스킬 {u.Data.skills.Count}");
        }
        return sb.ToString();
    }

    static System.Collections.Generic.IEnumerable<UnitIdentity> Mine() =>
        UnitIdentity.Active.Where(u => u != null && u.Data != null && u.Data.unitName == "압살롬" && u.OwnerId == 0);

    static string Describe(GamblingShop shop)
    {
        LaneShopSlotView v = shop.GetSlotView(7);
        return $"「{v.label.Replace("\n", " ")}」 사용가능={v.available}";
    }

    static string Frame()
    {
        UnitIdentity u = Mine().FirstOrDefault();
        if (u == null) return "❌ 내 압살롬이 없다";
        RtsCameraController cam = Object.FindFirstObjectByType<RtsCameraController>();
        if (cam == null) return "❌ 카메라 없음";
        cam.MoveTo(u.transform.position);
        var rs = u.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return $"카메라 → 압살롬 {u.transform.position:F0} · 경계 크기 {b.size:F1}";
    }
}
