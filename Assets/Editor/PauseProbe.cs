using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 일시정지 점검(10-07) — gameshot: call:ShopSlotProbe.Fund wait:25 call:PauseProbe.Snap0 call:PauseProbe.Pause wait:10 call:PauseProbe.SnapPaused call:PauseProbe.TryBlocked wait:1 call:PauseProbe.Resume wait:3 call:PauseProbe.SnapAfter
static class PauseProbe
{
    static string before;
    static string Snapshot()
    {
        var rm = Object.FindFirstObjectByType<RoundManager>();
        var ctx = PlayerContext.Get(0);
        var enemies = EnemyDummy.Active.Where(e => e != null && !e.IsDead).Take(4).ToList();
        var sb = new StringBuilder();
        sb.Append($"라운드 {rm.CurrentRound} · 준비 {rm.PreRoundTimeLeft:F2} · 라운드 남은 {rm.RoundTimeLeft:F2} · 엔 {ctx.GoldWallet.Gold} · 마나 {ctx.ResourceWallet.Get(ResourceType.Mana)} · 위습 · Time.time {Time.time:F2} timeScale {Time.timeScale} · 적 {EnemyDummy.Active.Count}기");
        foreach (var e in enemies) sb.Append($" · {e.name.Replace("(Clone)", "")} ({e.transform.position.x:F1},{e.transform.position.z:F1})");
        return sb.ToString();
    }
    static string Snap0() { before = Snapshot(); return "[전] " + before; }
    static string Pause() { GamePause.TryToggle(out string r); before = Snapshot(); return $"멈춤 직후 → Paused {GamePause.Paused} {r}\n  {before}"; }
    static string SnapPaused() { string now = Snapshot(); return $"[멈춘 10초 뒤] {now}\n  멈춤 직후와 완전히 같음: {now == before}"; }
    static bool SameIgnoringNothing(string now) => now == before;
    static string TryBlocked()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var sys = Object.FindFirstObjectByType<CombineSystem>();
        var recipe = sys != null ? sys.RecipeAt(0) : null;
        bool combine = recipe != null && sys.TryCombine(recipe);
        var shop = Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.SlotCount >= 8 && s.name.Contains("Lane1_유닛강화소"));
        var hud = Object.FindFirstObjectByType<GameHud>();
        int g0 = PlayerContext.Get(0).GoldWallet.Gold;
        var click = typeof(GameHud).GetMethod("OnShopSlotClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (shop != null) Object.FindFirstObjectByType<SelectionManager>().SelectOnly(shop.GetComponent<Selectable>());
        click.Invoke(hud, new object[] { 0 });
        return $"멈춘 중 조합 시도 {combine}(false여야) · 상점 칸 0 클릭 후 엔 {g0} → {PlayerContext.Get(0).GoldWallet.Gold}(같아야)";
    }
    static string Resume() { GamePause.TryToggle(out string r); return $"재개 → Paused {GamePause.Paused}"; }
    static string SnapAfter() => "[재개 3초 뒤] " + Snapshot();
}
